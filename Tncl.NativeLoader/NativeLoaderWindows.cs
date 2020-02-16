using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Tncl.NativeLoader
{
    internal class NativeLoaderWindows : NativeLoaderBase
    {
        internal NativeLoaderWindows(ILogger logger) : base(logger)
        {
        }

        internal override string GetOSLibraryName(string fileName, string version)
        {
            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return fileName + ".dll";
            return fileName;
        }

        protected override IntPtr PlatformLoadLibrary(string fileName)
        {
            return WindowsLoadLibrary(fileName);
        }

        protected override bool PlatformFreeLibrary(IntPtr handle)
        {
            return WindowsFreeLibrary(handle);
        }

        protected override IntPtr PlatformGetProcAddress(IntPtr handle, string functionName)
        {
            var result = WindowsGetProcAddress(handle, functionName);
            var lastErrorCode = Marshal.GetLastWin32Error();

            if (lastErrorCode != 0)
                throw new Exception($"GetProcAddress error: {lastErrorCode}");

            return result;
        }

        [DllImport("kernel32", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr WindowsLoadLibrary(string dllPath);

        [DllImport("kernel32", EntryPoint = "FreeLibrary", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern bool WindowsFreeLibrary(IntPtr handle);

        [DllImport("kernel32", EntryPoint = "GetProcAddress", CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern IntPtr WindowsGetProcAddress(IntPtr handle, string procedureName);

    }
}