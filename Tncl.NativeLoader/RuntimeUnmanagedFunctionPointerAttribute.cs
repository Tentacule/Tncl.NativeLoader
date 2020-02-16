using System;
using System.Runtime.InteropServices;

namespace Tncl.NativeLoader
{
    [ComVisible(true)]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class RuntimeUnmanagedFunctionPointerAttribute : Attribute
    {
        public string EntryPoint { get; set; }
        public CallingConvention CallingConvention { get; set; }
        public CharSet CharSet { get; set; }
        public bool SetLastError { get; set; }
        public bool BestFitMapping { get; set; }
        public bool ThrowOnUnmappableChar { get; set; }
        public string LibraryName { get; set; }
        public string LibraryVersion { get; set; }
    }
}
