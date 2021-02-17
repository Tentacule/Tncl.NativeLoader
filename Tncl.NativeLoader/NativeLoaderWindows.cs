using Microsoft.Extensions.Logging;
using System;

namespace Tncl.NativeLoader
{
    internal class NativeLoaderWindows : NativeLoaderBase
    {
        private NativeLoaderWindowsOptions _options;

        internal NativeLoaderWindows(ILogger logger, NativeLoaderWindowsOptions options) : base(logger)
        {
            _options = options;
        }

        internal override string GetOSLibraryName(string fileName, string version)
        {
            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return fileName + ".dll";
            return fileName;
        }
    }
}