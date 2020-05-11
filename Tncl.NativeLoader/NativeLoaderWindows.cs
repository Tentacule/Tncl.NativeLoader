using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Tncl.NativeLoader
{
    internal class NativeLoaderWindows : NativeLoaderBase
    {
        private ILogger _logger;
        private NativeLoaderWindowsOptions _options;

        internal NativeLoaderWindows(ILogger logger, NativeLoaderWindowsOptions options) : base(logger)
        {
            _logger = logger;
            _options = options;
        }

        internal override string GetOSLibraryName(string fileName, string version)
        {
            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return fileName + ".dll";
            return fileName;
        }

        protected override IntPtr PlatformLoadLibrary(string fileName)
        {
            if (_options.UseSetDllDirectory)
            {
                SetDllDirectory(Path.GetDirectoryName(fileName));
            }

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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

    }
}