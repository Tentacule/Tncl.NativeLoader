using Microsoft.Extensions.Logging;

namespace Tncl.NativeLoader
{
    internal class NativeLoaderOSX : NativeLoaderBase
    {
        internal NativeLoaderOSX(ILogger logger) : base(logger)
        {
        }

        internal override string GetOSLibraryName(string fileName, string version)
        {
            if (!string.IsNullOrEmpty(version))
            {
                version = $".{version}";
            }

            return $"lib{fileName}{version}.dylib";
        }
    }
}