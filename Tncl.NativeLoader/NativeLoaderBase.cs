using System;
using Microsoft.Extensions.Logging;

namespace Tncl.NativeLoader
{
    internal abstract class NativeLoaderBase
    {
        protected ILogger _logger;

        internal NativeLoaderBase(ILogger logger)
        {
            _logger = logger;
        }

        internal IntPtr Load(string fileName)
        {
            var libraryHandle = IntPtr.Zero;

            try
            {
                libraryHandle = PlatformLoadLibrary(fileName);
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Failed to load {fileName}", e);
            }

            return libraryHandle;
        }

        internal bool Free(IntPtr handle)
        {
            return PlatformFreeLibrary(handle);
        }

        internal IntPtr GetProcAddress(IntPtr libraryHandle, string functionName)
        {
            return PlatformGetProcAddress(libraryHandle, functionName);
        }

        internal abstract string GetOSLibraryName(string fileName, string version);

        protected abstract IntPtr PlatformLoadLibrary(string fileName);

        protected abstract bool PlatformFreeLibrary(IntPtr handle);

        protected abstract IntPtr PlatformGetProcAddress(IntPtr handle, string functionName);

    }
}