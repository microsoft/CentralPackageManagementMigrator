using Buildalyzer;
using Serilog;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Tool
{
    internal sealed class MigratorCommand : AsyncCommand<MigratorCommand.Settings>
    {
        public sealed class Settings : LogSettings
        {
            [Description("Path to search. Defaults to current directory.")]
            [CommandArgument(0, "[searchPath]")]
            public string? SearchPath { get; set; }

            [Description("Directories to exclude in search.")]
            [CommandOption("-e|--exclude-directory")]
            public string? ExcludeDirectory { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            LoggingUtility.SetupLogger(settings);
            Log.Logger.Debug("Called with settings {@Settings}", settings);

            if (string.IsNullOrWhiteSpace(settings.SearchPath))
            {
                settings.SearchPath = Directory.GetCurrentDirectory();
                Log.Logger.Debug("Search Path set to currect directory");
            }

            Log.Logger.Information("Adding Central Package Management under search path: {SearchPath}", settings.SearchPath);

            Log.Logger.Debug("Loading file assets");
            var assets = AssetLoader.GetAssets();

            var repoRoot = GitHelper.LocateRepositoryRoot(settings.SearchPath) ?? settings.SearchPath;

            //var nugetConfigPath = Path.Combine(repoRoot, "NuGet.config");
            var allNuGetConfigs = Directory.GetFiles(repoRoot, "NuGet.config", SearchOption.AllDirectories);
            if (!allNuGetConfigs.Any())
            {
                Log.Logger.Warning("NuGet.config missing from codebase, unable to add source mapping");
            }
            else
            {
                foreach (var nugetConfig in allNuGetConfigs)
                {
                    NuGetConfigHelper.AddPackageSourceIfMissing(nugetConfig);
                }
            }

            var directoryPackagesPropsPath = Path.Combine(repoRoot, "Directory.Packages.props");
            if (File.Exists(directoryPackagesPropsPath))
            {
                Log.Logger.Warning("{DirectoryPackagesPropsPath} already exists, Central Packagement Managemen may already be setup", directoryPackagesPropsPath);
            }
            else
            {
                // Create Directory.Packages.props file
                Log.Logger.Debug("Creating Directory.Packages.props");
                var directoryPackagesPropsAsset = assets.First(a => a.Name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase));
                directoryPackagesPropsAsset.CopyToDirectory(repoRoot);
                Log.Logger.Information("Created {DirectoryPackagesPropsPath} file", directoryPackagesPropsPath);
            }

            var packagesPropsPath = Path.Combine(repoRoot, "Packages.props");
            if (File.Exists(packagesPropsPath))
            {
                Log.Logger.Warning("{PackagesPropsPath} already exists, central package versioning sdk already setup", packagesPropsPath);
                // TODO fork path to do conversion
                Log.Logger.Error("Tool doesn't support converting from Central Packages SDK to Central Package Management.");
                return 1;
            }

            Log.Logger.Information("Getting all *.*proj files under: {SearchPath}", settings.SearchPath);
            var allProjects = Directory.GetFiles(settings.SearchPath, "*.*proj", SearchOption.AllDirectories);
            Log.Logger.Debug("Found {Length} *.*proj files", allProjects.Length);

            var latestPackage = new Dictionary<string, NuGetPackageInfo>();

            var projectPackageLookup = new Dictionary<FileInfo, Dictionary<string, NuGetPackageInfo>>();

            Log.Logger.Debug("Getting exclude directories");
            var excludeDirectories = string.IsNullOrEmpty(settings.ExcludeDirectory) ? Array.Empty<string>() : settings.ExcludeDirectory.Split(';');

            Log.Logger.Debug("Getting FileInfo for projects and filtering based on exclusions");
            var allProjectFileInfos = allProjects.Select(a => new FileInfo(a)).Where(a => !excludeDirectories.Any(e => a.DirectoryName?.StartsWith(e) ?? false)).ToList();
            Log.Logger.Debug("After filter, {Count} FileInfo(s) loaded", allProjectFileInfos.Count);

            bool packagesReferenceContainsNoVersion = false;
            Log.Logger.Information("Reading project files");
            foreach (var projectFile in allProjectFileInfos)
            {
                Log.Logger.Information("Reading project {FullName} for PackageReferences", projectFile.FullName);
                var projectDocument = new XmlDocument();
                using var fileStream = projectFile.OpenRead();
                projectDocument.Load(fileStream);
                Log.Logger.Debug("Project XML loaded");

                Log.Logger.Debug("Checking for legacy XML namespace");
                var msbuildXmlQueryHelper = new MSBuildXmlNamespaceQueryHelper(projectDocument);
                var legacyNamespace = msbuildXmlQueryHelper.SelectSingleNode(projectDocument, "Project", true) != null;
                Log.Logger.Debug("Legacy namespace used {LegacyNamespace}", legacyNamespace);

                Log.Logger.Debug("Checking for PackageReferences");
                var packageReferences = msbuildXmlQueryHelper.SelectNodes(projectDocument, "//PackageReference", legacyNamespace);
                Log.Logger.Information("Found {Count} PackageReferences", packageReferences?.Count ?? 0);

                IProjectAnalyzer? project = null;
                IAnalyzerResult? firstBuildResult = null;

                var packagesInProject = new Dictionary<string, NuGetPackageInfo>();
                if (packageReferences == null || packageReferences.Count < 1)
                {
                    Log.Logger.Debug("No PackageReferences found in {FullName}", projectFile.FullName);
                    continue;
                }

                foreach (XmlNode packageReference in packageReferences)
                {
                    Log.Logger.Debug("Checking Include and Version attributes on PackageReference");
                    var packageName = packageReference.Attributes?["Include"]?.Value;
                    var packageVersion = packageReference.Attributes?["Version"]?.Value;
                    if (string.IsNullOrEmpty(packageVersion))
                    {
                        Log.Logger.Warning("Detected null package version");
                        packagesReferenceContainsNoVersion = true;
                        continue;
                    }

                    var variablesToReplace = Regex.Matches(packageVersion, @"\$\(.*\)");
                    foreach (Match replaceVariable in variablesToReplace)
                    {
                        if (firstBuildResult == null)
                        {
                            try
                            {
                                firstBuildResult = DesignTimeBuildHelper.ExecuteDesignTimeBuild(settings, projectFile, out project);
                            }
                            catch (Exception ex)
                            {
                                Log.Logger.Error(ex, "Failed to execute design-time build on: {FullName}", projectFile.FullName);
                                throw;
                            }
                        }

                        Log.Logger.Debug("\tResolving MSBuild property: {Value}", replaceVariable.Value);
                        var propertyName = replaceVariable.Value.Substring(2, replaceVariable.Value.Length - 3); // Skip first 2 chars, extract length minus first 2 chars and last char = 3
                        var resolvedVariable = firstBuildResult.GetProperty(propertyName);
                        Log.Logger.Debug("\tResolved {PropertyName} to: {ResolvedVariable}", propertyName, resolvedVariable);
                        packageVersion = packageVersion.Replace(replaceVariable.Value, resolvedVariable);
                    }

                    if (packageName == null || packageVersion == null)
                    {
                        Log.Logger.Warning("Null package name or version detected: {PackageReference}", packageReference);
                        continue;
                    }

                    var packageInfo = new NuGetPackageInfo(packageName, packageVersion);
                    Log.Logger.Debug("Nuget package found in {FullName}, package ID {PackageName} and version {PackageVersion}", projectFile.FullName, packageName, packageVersion);

                    packagesInProject.Add(packageName, packageInfo);
                    Log.Logger.Debug("Package added for lookup inside project");

                    if (latestPackage.ContainsKey(packageName))
                    {
                        Log.Logger.Debug("Latest package lookup contains package {PackageName}", packageName);
                        var latestPackageVersion = latestPackage[packageName];
                        if (packageInfo.CompareTo(latestPackageVersion) == 1)
                        {
                            // package has a higher version then latestPackageVersion
                            latestPackage[packageName] = packageInfo;
                            Log.Logger.Debug("Updated latest package version for {PackageName} to version {PackageVersion}", packageName, packageVersion);
                        }
                        else
                        {
                            Log.Logger.Debug("Package had version equal to or less than latest version");
                        }
                    }
                    else
                    {
                        latestPackage.Add(packageName, packageInfo);
                        Log.Logger.Debug("Added new package {PackageName} as latest package with version {PackageVersion}", packageName, packageVersion);
                    }
                }

                if (packagesReferenceContainsNoVersion)
                {
                    Log.Logger.Warning("Project {FullName} contained PackageReferences without Version attributes, Central Package Versioning SDK or Central Package Management could be setup", projectFile.FullName);
                    packagesReferenceContainsNoVersion = false;
                    continue;
                }

                if (packagesInProject.Any())
                {
                    projectPackageLookup.Add(projectFile, packagesInProject);
                    Log.Logger.Debug("Added project file and packages for lookup on file {FullName}", projectFile.FullName);
                }
            }

            if (latestPackage.Any())
            {
                // Update Directory.Packages.props
                var directoryPackagesPropsDocument = new XmlDocument();
                var editedDirectoryPackagesProps = false;
                Log.Logger.Information("Updating {DirectoryPackagesPropsPath} with latest package versions", directoryPackagesPropsPath);
                Log.Logger.Debug("Reading XML {DirectoryPackagesPropsPath} file", directoryPackagesPropsPath);
                using (var fileStream = new FileInfo(directoryPackagesPropsPath).OpenRead())
                {
                    directoryPackagesPropsDocument.Load(fileStream);
                    Log.Logger.Debug("Loaded XML {DirectoryPackagesPropsPath} file", directoryPackagesPropsPath);

                    var msbuildXmlQueryHelper = new MSBuildXmlNamespaceQueryHelper(directoryPackagesPropsDocument) { RequireNamespace = true };

                    Log.Logger.Debug("Finding first ItemGroup in file");
                    var firstItemGroup = msbuildXmlQueryHelper.SelectNodes(directoryPackagesPropsDocument, "//ItemGroup")?.OfType<XmlNode>()?.First();
                    if (firstItemGroup == null)
                    {
                        throw new NotImplementedException("assets\\Directory.Packages.props is missing ItemGroup");
                    }

                    foreach (var packageInfo in latestPackage.Values)
                    {
                        Log.Logger.Debug("Finding PackageVersion with Include == {Id}", packageInfo.Id);
                        var existingEntry = msbuildXmlQueryHelper.SelectSingleNode(directoryPackagesPropsDocument, $"//PackageVersion[@Include='{packageInfo.Id}']");
                        Log.Logger.Debug("Existing PackageVersion = {@ExistingEntry}", existingEntry);

                        if (existingEntry != null)
                        {
                            // TODO handle when version contains various formats of version that mean same thing, such as [X.X.X] == X.X.X == (X.X.X, ), etc
                            if (!existingEntry.Attributes?["Version"]?.Value?.Equals(packageInfo.Version, StringComparison.OrdinalIgnoreCase) ?? false)
                            {
                                editedDirectoryPackagesProps = true;
                                // TODO: Handle when Version attribute is missing
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                existingEntry.Attributes["Version"].Value = packageInfo.Version;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                Log.Logger.Information("Updated {Id} package to version {Version} in Directory.Packages.props", packageInfo.Id, packageInfo.Version);
                            }
                            else
                            {
                                Log.Logger.Debug("Package {Id} had correct targed version {Version}", packageInfo.Id, packageInfo.Version);
                            }
                        }
                        else
                        {
                            Log.Logger.Debug("Creating new PackageVersion element for package {Id}", packageInfo.Id);
                            var newPackageXmlNode = directoryPackagesPropsDocument.CreateElement("PackageVersion", MSBuildXmlNamespaceQueryHelper.MSBuildXmlNamespace);
                            newPackageXmlNode.SetAttribute("Include", packageInfo.Id);
                            newPackageXmlNode.SetAttribute("Version", packageInfo.Version);
                            firstItemGroup.AppendChild(newPackageXmlNode);
                            editedDirectoryPackagesProps = true;
                            Log.Logger.Information("Added new PackageVersion for {Id} package and version {Version}", packageInfo.Id, packageInfo.Version);
                        }
                    }
                }

                if (editedDirectoryPackagesProps)
                {
                    Log.Logger.Debug("Saving XML {DirectoryPackagesPropsPath} file", directoryPackagesPropsPath);
                    directoryPackagesPropsDocument.Save(directoryPackagesPropsPath);
                    Log.Logger.Information("Updated {DirectoryPackagesPropsPath} file", directoryPackagesPropsPath);
                }
                else
                {
                    Log.Logger.Information("{DirectoryPackagesPropsPath} file required no edits, conatained correct package information", directoryPackagesPropsPath);
                }

                // Update project files
                Log.Logger.Information("Updating project files PackageReference elements");
                foreach (var projectWithPackages in projectPackageLookup)
                {
                    Log.Logger.Information("Editing {FullName} project file", projectWithPackages.Key.FullName);

                    var projectDoc = new XmlDocument();
                    Log.Logger.Debug("Reading XML file {FullName}", projectWithPackages.Key.FullName);
                    using (var fileStream = projectWithPackages.Key.OpenRead())
                    {
                        projectDoc.Load(fileStream);
                        Log.Logger.Debug("Loaded XML {FullName} file", projectWithPackages.Key.FullName);
                    }

                    var msbuildXmlQueryHelper = new MSBuildXmlNamespaceQueryHelper(projectDoc);
                    Log.Logger.Debug("Checking if file {FullName} is using legacy MSBuild namespace", projectWithPackages.Key.FullName);
                    var isLegacyProject = msbuildXmlQueryHelper.IsUsingLegacyNamespace(updateRequireNamespace: true);
                    Log.Logger.Debug("IsLegacy namespace = {IsLegacyProject}", isLegacyProject);

                    foreach (var packageInfo in projectWithPackages.Value.Values)
                    {
                        Log.Logger.Debug("Checking if package {Id} is latest version used in codebase", packageInfo.Id);
                        var isLatestPackage = latestPackage[packageInfo.Id] == packageInfo;
                        Log.Logger.Debug("IsLatestPackage = {IsLatestPackage}", isLatestPackage);

                        Log.Logger.Debug("Locating PackageReference with Include == {Id}", packageInfo.Id);
                        var packageReferenceElement = msbuildXmlQueryHelper.SelectSingleNode(projectDoc, $"//PackageReference[@Include='{packageInfo.Id}']");
                        Log.Logger.Debug("PackageReference element found, {@PackageReferenceElement}", packageReferenceElement);

                        // Either way you are removing Version attribute
                        packageReferenceElement?.Attributes?.Remove(packageReferenceElement.Attributes["Version"]);
                        Log.Logger.Information("Removed Version attribute on package {Id} in {FullName}", packageInfo.Id, projectWithPackages.Key.FullName);

                        if (!isLatestPackage)
                        {
                            // Switch to VersionOverride
                            var versionOverrideAttribute = projectDoc.CreateAttribute("VersionOverride", isLegacyProject ? MSBuildXmlNamespaceQueryHelper.MSBuildXmlNamespace : null);
                            versionOverrideAttribute.Value = packageInfo.Version;
                            packageReferenceElement?.Attributes?.Append(versionOverrideAttribute);
                            Log.Logger.Information("Added VersionOverride on package {Id} for version {Version} in {FullName}", packageInfo.Id, packageInfo.Version, projectWithPackages.Key.FullName);
                        }
                    }

                    Log.Logger.Debug("Saving XML {FullName} file", projectWithPackages.Key.FullName);
                    projectDoc.Save(projectWithPackages.Key.FullName);
                    Log.Logger.Information("Saved PackageReference updates to {FullName}", projectWithPackages.Key.FullName);
                }
            }
            else
            {
                Log.Logger.Information("Skipped editing Directory.Packages.props because packages contained no versions or no packages were found in codebase");
            }

            Log.Logger.Information("Setup of Central Packaging Management under {SearchPath} is complete", settings.SearchPath);

            return await Task.FromResult(0);
        }
    }
}
