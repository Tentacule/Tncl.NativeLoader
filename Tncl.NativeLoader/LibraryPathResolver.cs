using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Tncl.NativeLoader
{
    public interface ILibraryPathResolver
    {
        bool FixupLibraryName { get; set; }
        IEnumerable<string> GetProbePaths(string name);
    }

    public class LibraryPathResolver : ILibraryPathResolver
    {
        public static ILibraryPathResolver DefaultLibraryPathResolver { get; } = new LibraryPathResolver();

        public bool FixupLibraryName { get; set; } = true;

        public IEnumerable<string> GetProbePaths(string name)
        {
            var platformName = Environment.Is64BitProcess ? "x64" : "x86";
            var baseDirectories = new List<string>();
            var executingAssemblyDirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            foreach (var platformSubFolder in new[] { "", platformName })
            {
                if (executingAssemblyDirectoryName != null)
                    baseDirectories.Add(Path.Combine(executingAssemblyDirectoryName, platformSubFolder));

                baseDirectories.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, platformSubFolder));
                baseDirectories.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", platformSubFolder));
                baseDirectories.Add(Path.Combine(Environment.CurrentDirectory, platformSubFolder));
            }

            var result = 
                from baseDirectory in baseDirectories
                let fullPath = Path.Combine(baseDirectory, name)
                where !File.Exists(fullPath)
                select fullPath;

            return result;
        }
    }
}
