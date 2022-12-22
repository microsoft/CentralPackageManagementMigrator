using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tool
{
    internal static class AssetLoader
    {
        public const string AssetsDirectory = "\\assets";

        public static IEnumerable<AssetFile> GetAssets()
        {
            // https://gist.github.com/dradovic/0548310e623391145cfb0c04bd2db772
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/file-providers?view=aspnetcore-3.1
            var provider = new ManifestEmbeddedFileProvider(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());
            var files = new List<AssetFile>();
            GetFiles(provider, "\\assets", files);
            return files;
        }

        private static void GetFiles(IFileProvider provider, string path, ICollection<AssetFile> files)
        {
            foreach (var content in provider.GetDirectoryContents(path))
            {
                var filePath = path + "\\" + content.Name;
                if (!content.IsDirectory)
                {
                    files.Add(new AssetFile(content, filePath));
                }
                else
                {
                    GetFiles(provider, filePath, files);
                }
            }
        }
    }

    internal class AssetFile
    {
        public string Name { get; }

        public string Content { get; }

        public string FilePath { get; }

        public AssetFile(IFileInfo fileInfo, string filePath)
        {
            this.Name = fileInfo.Name;
            this.FilePath = filePath.Substring(AssetLoader.AssetsDirectory.Length + 1); // remove assets directory from name

            using (var reader = fileInfo.CreateReadStream())
            {
                // Read content of file
                using (var streamReader = new StreamReader(reader))
                {
                    this.Content = streamReader.ReadToEnd();
                }
            }
        }

        public void CopyToDirectory(string directory, AssetTemplate? template = null, Encoding? encoding = null)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var outputFilePath = Path.Join(directory, this.Name);
            var content = this.Content;

            if (template != null)
            {
                foreach (var replaceWith in template.Data)
                {
                    content = content.Replace($"@@{replaceWith.Key}@@", replaceWith.Value);
                }
            }

            File.WriteAllText(outputFilePath, content, encoding ?? Encoding.UTF8);
        }
    }
}
