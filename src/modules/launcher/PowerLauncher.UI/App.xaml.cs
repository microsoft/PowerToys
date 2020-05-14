using System.Runtime.InteropServices;

namespace PowerLauncher.UI
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

    public sealed partial class App : Microsoft.Toolkit.Win32.UI.XamlHost.XamlApplication
    {
        public App()
        {
            this.Initialize();

            // Hide the Xaml Island window
            var coreWindow = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            var coreWindowInterop = Interop.GetInterop(coreWindow);
            Interop.ShowWindow(coreWindowInterop.WindowHandle, Interop.SW_HIDE);
        }
    }
}