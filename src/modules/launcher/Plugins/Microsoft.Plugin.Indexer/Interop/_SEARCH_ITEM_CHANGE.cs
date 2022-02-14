
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _SEARCH_ITEM_CHANGE
    {
        public _SEARCH_KIND_OF_CHANGE Change;
        public _SEARCH_NOTIFICATION_PRIORITY Priority;
        [ComConversionLoss]
        public IntPtr pUserData;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpwszURL;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpwszOldURL;
    }
}
