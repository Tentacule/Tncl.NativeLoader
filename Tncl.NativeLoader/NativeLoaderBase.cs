using System;
using System.Runtime.InteropServices;
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
            return NativeLibrary.Load(fileName);
        }

        internal void Free(IntPtr handle)
        {
            NativeLibrary.Free(handle);
        }

        internal IntPtr GetProcAddress(IntPtr handle, string name)
        {
            return NativeLibrary.GetExport(handle, name);
        }

        internal abstract string GetOSLibraryName(string fileName, string version);

    }
}