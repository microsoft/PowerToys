
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _SEARCH_ITEM_PERSISTENT_CHANGE
    {
        public _SEARCH_KIND_OF_CHANGE Change;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string URL;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string OldURL;
        public _SEARCH_NOTIFICATION_PRIORITY Priority;
    }
}
