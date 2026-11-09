// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CopilotKeyRegistration : IDisposable
{
    public const uint MessageId = 0x8001;

    // Keep this value in sync with SingleTap.MessageWParam in both package manifests.
    public const nuint SingleTap = 0;

    private nint _hwnd;

    public CopilotKeyRegistration(nint hwnd)
    {
        var value = default(PROPVARIANT);
        value.Anonymous.Anonymous.vt = VARENUM.VT_UINT;
        value.Anonymous.Anonymous.Anonymous.uintVal = MessageId;
        SetWindowProperty(hwnd, value);
        _hwnd = hwnd;
    }

    public void Dispose()
    {
        if (_hwnd == 0)
        {
            return;
        }

        try
        {
            // VT_EMPTY removes the property before the window is destroyed.
            SetWindowProperty(_hwnd, default);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to unregister Copilot key fast-path activation", ex);
        }
        finally
        {
            _hwnd = 0;
        }
    }

    private static unsafe void SetWindowProperty(nint hwnd, PROPVARIANT value)
    {
        var iid = IPropertyStore.IID_Guid;
        IPropertyStore* propertyStore = null;
        PInvoke.SHGetPropertyStoreForWindow((HWND)hwnd, &iid, (void**)&propertyStore).ThrowOnFailure();
        try
        {
            // https://learn.microsoft.com/windows/apps/develop/windows-integration/copilot-key-state
            var key = new PROPERTYKEY
            {
                fmtid = new Guid("38652BCA-4329-4E74-86F9-39CF29345EEA"),
                pid = 2,
            };
            propertyStore->SetValue(&key, &value);
        }
        finally
        {
            propertyStore->Release();
        }
    }
}
