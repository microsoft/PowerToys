// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Toolkit.Win32.UI.XamlHost;

namespace Microsoft.PowerToys.Settings.UI
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
    internal interface ICoreWindowInterop
    {
        System.IntPtr WindowHandle { get; }

        void MessageHandled(bool value);
    }

    internal static class Interop
    {
        public static ICoreWindowInterop GetInterop(this Windows.UI.Core.CoreWindow @this)
        {
            var unkIntPtr = Marshal.GetIUnknownForObject(@this);
            try
            {
                var interopObj = Marshal.GetTypedObjectForIUnknown(unkIntPtr, typeof(ICoreWindowInterop)) as ICoreWindowInterop;
                return interopObj;
            }
            finally
            {
                Marshal.Release(unkIntPtr);
                unkIntPtr = System.IntPtr.Zero;
            }
        }


        [DllImport("user32.dll")]
        public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

        public const int SW_HIDE = 0;
    }

    public sealed partial class App : XamlApplication
    {
        public App()
        {
            Initialize();

            // Hide the Xaml Island window
            var coreWindow = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            var coreWindowInterop = Interop.GetInterop(coreWindow);
            Interop.ShowWindow(coreWindowInterop.WindowHandle, Interop.SW_HIDE);
        }
    }
}
