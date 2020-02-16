using System.Collections.Generic;

namespace Tncl.NativeLoader
{
    public class LibraryItem
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public IList<LibraryVersion> OverrideLibraryName { get; } = new List<LibraryVersion>();
    }

    public class LibraryVersion
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public Platform Platform { get; set; }
    }
}
