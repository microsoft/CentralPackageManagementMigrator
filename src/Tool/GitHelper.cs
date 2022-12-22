using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tool
{
    internal class GitHelper
    {
        public static string? LocateRepositoryRoot(string repoPath, bool enforceGit = false)
        {
            Log.Logger.Verbose("Getting respository root directory from input {RepoPath}", repoPath);
            
            if (!Directory.Exists(repoPath))
            {
                throw new ArgumentException($"Argument has a directory that doesn't exist: {repoPath}", nameof(repoPath));
            }

            var startingRepoPath = repoPath;
            string? checkRepoPath = repoPath;

            do
            {
                var testPath = Path.Combine(checkRepoPath, ".git");
                if (Directory.Exists(testPath))
                {
                    Log.Logger.Debug("Located .git directory in {TestPath}", testPath);
                    return checkRepoPath;
                }

                try
                {
                    // Walk up to parent directory
                    checkRepoPath = Directory.GetParent(checkRepoPath)?.FullName;
                }
                catch (DirectoryNotFoundException)
                {
                    checkRepoPath = null;
                }

            } while (checkRepoPath != null);

            if (enforceGit)
            {
                throw new ArgumentException($"{startingRepoPath} argument wasn't a valid GIT repository");
            }

            return null;
        }
    }
}
