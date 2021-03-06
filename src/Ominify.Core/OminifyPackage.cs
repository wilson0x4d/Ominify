using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Caching;
using System.Text;

namespace Ominify
{
    public abstract class OminifyPackage
    {
        static readonly MemoryCache cache = new MemoryCache("OminifierPackageCache");
        static readonly string rootFileSystemPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        static readonly object syncLock = new object();

        readonly List<string> filePaths = new List<string>();
        readonly List<string> fileSystemPaths = new List<string>();
        readonly string packagePath;

        bool isLocked;

        protected OminifyPackage(string packagePath)
        {
            this.packagePath = packagePath;
        }

        public string PackagePath
        {
            get { return packagePath; }
        }

        public abstract string GetContentType();

        public abstract string GetHtmlElement(string url);

        public void AddFilePaths(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                AddFilePath(path);
            }
        }

        public void AddFilePath(string path)
        {
            if (isLocked)
                throw new InvalidOperationException("The package is locked for further additions.");

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("The path must not be null or empty.", "path");

            if (!path.StartsWith("/"))
                throw new ArgumentException("The path must start with a forward slash ('/').", "path");

            if (filePaths.Contains(path))
                throw new ArgumentException("The same path can't be added more than once.", "path");

            var fileSystemPath = GetFileSystemPath(path);

            filePaths.Add(path);
            fileSystemPaths.Add(fileSystemPath);
        }

        public string GetContent(OminifyOptions options)
        {
            var contentItem = GetOrLoadContentItem(options);
            return contentItem.Content;
        }

        public DateTime GetLastModifiedUtc(OminifyOptions options)
        {
            var contentItem = GetOrLoadContentItem(options);
            return contentItem.LastModifiedUtc;
        }

        public IEnumerable<string> GetFilePaths()
        {
            return filePaths.AsReadOnly();
        }

        protected virtual string ReadFileContent(string fileSystemPath, bool minify)
        {
            return File.ReadAllText(fileSystemPath);
        }

        protected virtual DateTime ReadFileLastModifiedUtc(string fileSystemPath)
        {
            return File.GetLastWriteTimeUtc(fileSystemPath);
        }

        PackageContentItem GetOrLoadContentItem(OminifyOptions options)
        {
            isLocked = true;

            var content = cache.Get(packagePath) as PackageContentItem;

            if (content == null)
            {
                lock (syncLock)
                {
                    content = CreateContent(options);

                    var cacheItemPolicy = new CacheItemPolicy();

                    if (options.AutoRefreshOnFileChanges)
                    {
                        cacheItemPolicy.ChangeMonitors.Add(new HostFileChangeMonitor(fileSystemPaths));
                    }

                    cache.Set(packagePath, content, cacheItemPolicy);
                }
            }

            return content;
        }

        PackageContentItem CreateContent(OminifyOptions options)
        {
            var topLastModified = DateTime.MinValue;
            var sb = new StringBuilder();

            foreach (var fileSystemPath in fileSystemPaths)
            {
                var content = ReadFileContent(fileSystemPath, options.MinifyBundles);

                sb.AppendLine(content);

                var lastModified = ReadFileLastModifiedUtc(fileSystemPath);
                if (topLastModified < lastModified)
                {
                    topLastModified = lastModified;
                }
            }

            return new PackageContentItem(sb.ToString(), topLastModified);
        }

		static string NormalizeContentPath(string path)
		{
			return path
				.Replace('/', Path.DirectorySeparatorChar)
				.Replace('\\', Path.DirectorySeparatorChar)
				.TrimStart(Path.DirectorySeparatorChar);
		}
		static string NormalizeBasePath(string path)
		{
			return path
				.Replace('/', Path.DirectorySeparatorChar)
				.Replace('\\', Path.DirectorySeparatorChar)
				.TrimEnd(Path.DirectorySeparatorChar);
		}
		static string GetFileSystemPath(string path)
        {
			return Path.GetFullPath(Path.Combine(
				NormalizeBasePath(rootFileSystemPath),
				NormalizeContentPath(path)));
        }

        public class PackageContentItem
        {
            public PackageContentItem(string content, DateTime lastModifiedUtc)
            {
                Content = content;
                LastModifiedUtc = lastModifiedUtc;
            }

            public string Content { get; private set; }

            public DateTime LastModifiedUtc { get; private set; }
        }
    }
}