using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Tool
{
    internal static class NuGetConfigHelper
    {
        public static void AddPackageSourceIfMissing(string nugetConfigPath)
        {
            Log.Logger.Information("Adding Package Source Mapping to {NugetConfigPath}", nugetConfigPath);
            var nugetConfigFile = new FileInfo(nugetConfigPath);
            var nugetConfigDocument = GetNuGetConfigXml(nugetConfigFile);
            var sourceMappings = GetPackageSourceMappings(nugetConfigDocument);
            if (sourceMappings == null || sourceMappings.Count == 0)
            {
                Log.Logger.Information("Detected Package Source Mapping needs to be added");
                var configurationRoot = nugetConfigDocument.SelectSingleNode("/configuration");
                if (configurationRoot == null)
                {
                    throw new InvalidOperationException("NuGet config missing configuration root");
                }

                Log.Logger.Debug("Creating packageSourceMapping element in XML");
                var packageSourceMappingElement = nugetConfigDocument.CreateElement("packageSourceMapping");
                configurationRoot.AppendChild(packageSourceMappingElement);

                Log.Logger.Debug("Creating packageSource element in XML");
                var packageSourceElement = nugetConfigDocument.CreateElement("packageSource");
                packageSourceMappingElement.AppendChild(packageSourceElement);

                Log.Logger.Debug("Getting existing package key in XML");
                var existingPackageSource = GetPackageSource(nugetConfigDocument);
                var existingPackageSourceKey = existingPackageSource?.Attributes?["key"]?.Value;

                Log.Logger.Debug("Creating key attribute for packageSource in XML");
                var packageSourceKeyAttribute = nugetConfigDocument.CreateAttribute("key");
                packageSourceKeyAttribute.Value = existingPackageSourceKey;
                packageSourceElement.Attributes.Append(packageSourceKeyAttribute);

                Log.Logger.Debug("Creating package element in XML");
                var packageElement = nugetConfigDocument.CreateElement("package");
                packageSourceElement.AppendChild(packageElement);

                Log.Logger.Debug("Creating pattern attribute for package in XML");
                var patternAttribute = nugetConfigDocument.CreateAttribute("pattern");
                patternAttribute.Value = "*";
                packageElement.Attributes.Append(patternAttribute);

                Log.Logger.Debug("Saving NuGet.config");
                nugetConfigDocument.Save(nugetConfigPath);
                Log.Logger.Information("{NugetConfigPath} was updated with Package Source Mapping", nugetConfigPath);
            }
            else
            {
                Log.Logger.Information("Package Source Mapping already exists in {NugetConfigPath}", nugetConfigPath);
            }
        }

        private static XmlDocument GetNuGetConfigXml(FileInfo nugetConfigFile)
        {
            Log.Logger.Information("Getting the NuGet.config document from {FullName}", nugetConfigFile.FullName);
            var nugetConfigDocument = new XmlDocument();
            Log.Logger.Debug("Reading XML file {FullName}", nugetConfigFile.FullName);
            using (var fileStream = nugetConfigFile.OpenRead())
            {
                nugetConfigDocument.Load(fileStream);
            }

            return nugetConfigDocument;
        }

        private static XmlNode? GetPackageSource(XmlDocument nugetConfigDocument)
        {
            Log.Logger.Debug("Getting packageSources in XML");
            return nugetConfigDocument.SelectSingleNode("/configuration/packageSources/add");
        }

        private static XmlNodeList? GetPackageSourceMappings(XmlDocument nugetConfigDocument)
        {
            Log.Logger.Debug("Getting packageSourceMappings in XML");
            return nugetConfigDocument.SelectNodes("//packageSourceMapping/packageSource");
        }
    }
}
