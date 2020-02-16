using System;
using System.Runtime.InteropServices;

namespace Tncl.NativeLoader
{
    [ComVisible(true)]
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class NativeLoaderOverrideAttribute : Attribute
    {
        public Platform Platform { get; set; }
        public string LibraryName { get; set; }
        public string LibraryVersion { get; set; }
    }
}
