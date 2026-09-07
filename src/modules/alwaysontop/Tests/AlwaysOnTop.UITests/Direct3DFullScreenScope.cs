// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.AlwaysOnTop.UITests;

internal sealed class Direct3DFullScreenScope : IDisposable
{
    private const uint D3dSdkVersion = 32; // D3D_SDK_VERSION from d3d9.h.
    private const uint D3dCreateFpuPreserve = 0x00000002;
    private const uint D3dCreateSoftwareVertexProcessing = 0x00000020;
    private const uint D3dPresentIntervalImmediate = 0x80000000; // D3DPRESENT_INTERVAL_IMMEDIATE.
    private const int D3dSwapEffectDiscard = 1;
    private const int D3dDevTypeHardware = 1;
    private const int D3dDevTypeReference = 2;

    private readonly TestWindow window;
    private readonly System.Drawing.Rectangle originalBounds;
    private readonly System.Windows.Forms.FormBorderStyle originalBorderStyle;
    private readonly System.Windows.Forms.FormWindowState originalWindowState;
    private IntPtr direct3D;
    private IntPtr device;
    private bool disposed;

    private Direct3DFullScreenScope(TestWindow window)
    {
        this.window = window;
        var originalState = window.Invoke(
            () =>
            {
                var form = window.GetForm();
                return (form.Bounds, form.FormBorderStyle, form.WindowState);
            });
        originalBounds = originalState.Bounds;
        originalBorderStyle = originalState.FormBorderStyle;
        originalWindowState = originalState.WindowState;

        try
        {
            window.Invoke(CreateDevice);
        }
        catch
        {
            window.Invoke(
                () =>
                {
                    ReleaseDevice();
                    RestoreWindow();
                });
            throw;
        }
    }

    internal static Direct3DFullScreenScope Enter(TestWindow window)
    {
        return new Direct3DFullScreenScope(window);
    }

    internal static UserNotificationState QueryUserNotificationState()
    {
        var result = SHQueryUserNotificationState(out var state);
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        return state;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        window.Invoke(
            () =>
            {
                ReleaseDevice();
                RestoreWindow();
            });
        disposed = true;
    }

    [DllImport("d3d9.dll", ExactSpelling = true)]
    private static extern IntPtr Direct3DCreate9(uint sdkVersion);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out UserNotificationState state);

    private void CreateDevice()
    {
        var form = window.GetForm();
        form.WindowState = System.Windows.Forms.FormWindowState.Normal;
        form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

        direct3D = Direct3DCreate9(D3dSdkVersion);
        if (direct3D == IntPtr.Zero)
        {
            throw new InvalidOperationException("Direct3DCreate9 returned a null interface.");
        }

        // IDirect3D9 slot 8: GetAdapterDisplayMode (after the three IUnknown slots).
        var getDisplayMode = GetComMethod<GetAdapterDisplayModeDelegate>(direct3D, 8);
        var modeResult = getDisplayMode(direct3D, 0, out var mode);
        if (modeResult < 0)
        {
            throw new InvalidOperationException($"IDirect3D9::GetAdapterDisplayMode failed with HRESULT 0x{modeResult:X8}.");
        }

        form.Bounds = new System.Drawing.Rectangle(0, 0, (int)mode.Width, (int)mode.Height);
        form.Activate();

        var parameters = new D3dPresentParameters
        {
            BackBufferWidth = mode.Width,
            BackBufferHeight = mode.Height,
            BackBufferFormat = mode.Format,
            BackBufferCount = 1,
            MultiSampleType = 0,
            MultiSampleQuality = 0,
            SwapEffect = D3dSwapEffectDiscard,
            DeviceWindow = form.Handle,
            Windowed = 0,
            EnableAutoDepthStencil = 0,
            AutoDepthStencilFormat = 0,
            Flags = 0,
            FullScreenRefreshRateInHz = mode.RefreshRate,
            PresentationInterval = D3dPresentIntervalImmediate,
        };

        // IDirect3D9 slot 16: CreateDevice.
        var createDevice = GetComMethod<CreateDeviceDelegate>(direct3D, 16);
        var behaviorFlags = D3dCreateFpuPreserve | D3dCreateSoftwareVertexProcessing;
        var hardwareCreateResult = createDevice(
            direct3D,
            0,
            D3dDevTypeHardware,
            form.Handle,
            behaviorFlags,
            ref parameters,
            out device);

        int? referenceCreateResult = null;
        if (hardwareCreateResult < 0)
        {
            referenceCreateResult = createDevice(
                direct3D,
                0,
                D3dDevTypeReference,
                form.Handle,
                behaviorFlags,
                ref parameters,
                out device);
        }

        if ((referenceCreateResult ?? hardwareCreateResult) < 0 || device == IntPtr.Zero)
        {
            var referenceResult = referenceCreateResult.HasValue ? $"0x{referenceCreateResult.Value:X8}" : "not attempted";
            throw new InvalidOperationException(
                $"IDirect3D9::CreateDevice failed. HAL=0x{hardwareCreateResult:X8}; REF={referenceResult}.");
        }

        // IDirect3DDevice9 slot 17: Present.
        var present = GetComMethod<PresentDelegate>(device, 17);
        _ = present(device, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    }

    private void ReleaseDevice()
    {
        if (device != IntPtr.Zero)
        {
            Marshal.Release(device);
            device = IntPtr.Zero;
        }

        if (direct3D != IntPtr.Zero)
        {
            Marshal.Release(direct3D);
            direct3D = IntPtr.Zero;
        }
    }

    private void RestoreWindow()
    {
        var form = window.GetForm();
        form.WindowState = System.Windows.Forms.FormWindowState.Normal;
        form.TopMost = false;
        form.FormBorderStyle = originalBorderStyle;
        form.Bounds = originalBounds;
        form.WindowState = originalWindowState;
    }

    private static T GetComMethod<T>(IntPtr instance, int index)
        where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(instance);
        var method = Marshal.ReadIntPtr(vtable, index * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAdapterDisplayModeDelegate(IntPtr instance, uint adapter, out D3dDisplayMode mode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateDeviceDelegate(
        IntPtr instance,
        uint adapter,
        int deviceType,
        IntPtr focusWindow,
        uint behaviorFlags,
        ref D3dPresentParameters presentationParameters,
        out IntPtr returnedDeviceInterface);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PresentDelegate(
        IntPtr instance,
        IntPtr sourceRectangle,
        IntPtr destinationRectangle,
        IntPtr destinationWindowOverride,
        IntPtr dirtyRegion);

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dDisplayMode
    {
        internal uint Width;
        internal uint Height;
        internal uint RefreshRate;
        internal int Format;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3dPresentParameters
    {
        internal uint BackBufferWidth;
        internal uint BackBufferHeight;
        internal int BackBufferFormat;
        internal uint BackBufferCount;
        internal int MultiSampleType;
        internal uint MultiSampleQuality;
        internal int SwapEffect;
        internal IntPtr DeviceWindow;
        internal int Windowed;
        internal int EnableAutoDepthStencil;
        internal int AutoDepthStencilFormat;
        internal uint Flags;
        internal uint FullScreenRefreshRateInHz;
        internal uint PresentationInterval;
    }

    internal enum UserNotificationState
    {
        NotPresent = 1,
        Busy = 2,
        RunningDirect3DFullScreen = 3,
        PresentationMode = 4,
        AcceptsNotifications = 5,
        QuietTime = 6,
        App = 7,
    }
}
