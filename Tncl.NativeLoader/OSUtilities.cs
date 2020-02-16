using System.Runtime.InteropServices;

namespace Tncl.NativeLoader
{
    internal static class OSUtilities
    {
        public static Platform GetOsPlatform()
        {
            Platform result = default;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = Platform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                result = Platform.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                result = Platform.OSX;
            }

            return result;
        }
    }
}
