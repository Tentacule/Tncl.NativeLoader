using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Tncl.NativeLoader
{
    internal class NativeLoaderUnix : NativeLoaderBase
    {
        internal NativeLoaderUnix(ILogger logger) : base(logger)
        {
        }

        internal override string GetOSLibraryName(string fileName, string version)
        {
            if (!string.IsNullOrEmpty(version))
            {
                version = $".{version}";
            }

            return $"lib{fileName}.so{version}";
        }

        protected override IntPtr PlatformLoadLibrary(string fileName)
        {
            return UnixLoadLibrary(fileName, RTLD_NOW);
        }

        protected override bool PlatformFreeLibrary(IntPtr handle)
        {
            return UnixFreeLibrary(handle) != 0;
        }

        protected override IntPtr PlatformGetProcAddress(IntPtr handle, string functionName)
        {
            var functionHandle = UnixGetProcAddress(handle, functionName);
            var errorPointer = UnixGetLastError();

            if (errorPointer != IntPtr.Zero)
                throw new Exception($"dlsym error: {Marshal.PtrToStringAnsi(errorPointer)}");

            return functionHandle;
        }

        private const int RTLD_NOW = 2;

        [DllImport("libdl.so", EntryPoint = "dlopen")]
        private static extern IntPtr UnixLoadLibrary(string fileName, int flags);

        [DllImport("libdl.so", EntryPoint = "dlclose", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int UnixFreeLibrary(IntPtr handle);

        [DllImport("libdl.so", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr UnixGetProcAddress(IntPtr handle, string symbol);

        [DllImport("libdl.so", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr UnixGetLastError();
    }
}