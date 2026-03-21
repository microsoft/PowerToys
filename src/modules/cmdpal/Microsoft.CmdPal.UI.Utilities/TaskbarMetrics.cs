// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>
/// Measures the taskbar button area and tray area widths using
/// UI Automation with raw COM pointers (no runtime marshalling).
/// AOT-compatible.
/// </summary>
public sealed unsafe class TaskbarMetrics : IDisposable
{
    // CUIAutomation CLSID: {FF48DBA4-60EF-4201-AA87-54103EEF594E}
    private static readonly Guid CUIAutomationClsid =
        new(0xFF48DBA4, 0x60EF, 0x4201, 0xAA, 0x87, 0x54, 0x10, 0x3E, 0xEF, 0x59, 0x4E);

    private IUIAutomation* _automation;
    private IUIAutomationCondition* _trueCondition;
    private bool _disposed;

    /// <summary>Width of the taskbar buttons area in physical pixels.</summary>
    public int ButtonsWidthInPixels { get; private set; }

    /// <summary>Width of the notification/tray area in physical pixels.</summary>
    public int TrayWidthInPixels { get; private set; }

    /// <summary>Number of buttons found on the taskbar.</summary>
    public int ButtonCount { get; private set; }

    /// <summary>
    /// Re-measures the primary taskbar. Returns true if any value changed.
    /// </summary>
    public bool Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var taskbarHwnd = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd.IsNull)
        {
            return false;
        }

        var newButtons = MeasureButtons(taskbarHwnd, out var newCount);
        var newTray = MeasureTray(taskbarHwnd);

        // Skip transient error states (e.g. right-click context menu open)
        if (newCount == 0 && ButtonCount > 0)
        {
            return false;
        }

        if (newButtons == ButtonsWidthInPixels &&
            newTray == TrayWidthInPixels &&
            newCount == ButtonCount)
        {
            return false;
        }

        ButtonsWidthInPixels = newButtons;
        TrayWidthInPixels = newTray;
        ButtonCount = newCount;
        return true;
    }

    private int MeasureButtons(HWND taskbarHwnd, out int buttonCount)
    {
        buttonCount = 0;
        EnsureAutomation();

        if (_automation == null)
        {
            return 0;
        }

        IUIAutomationElement* root = null;
        IUIAutomationElementArray* children = null;
        IUIAutomationElementArray* descendants = null;
        try
        {
            var hr = _automation->ElementFromHandle(taskbarHwnd, &root);
            if (hr.Failed || root == null)
            {
                return 0;
            }

            // Determine the right boundary: buttons stop where the tray begins.
            PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
            var trayHwnd = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "TrayNotifyWnd", null);
            var rightBoundary = taskbarRect.right;
            if (!trayHwnd.IsNull)
            {
                PInvoke.GetWindowRect(trayHwnd, out var trayRect);
                rightBoundary = trayRect.left;
            }

            // Enumerate all UIA descendants. On Win11, buttons live inside
            // a XAML Islands window, not as direct children of MSTaskListWClass.
            // Filter to Button control type (50000) within the button area.
            hr = root->FindAll(TreeScope.TreeScope_Descendants, _trueCondition, &descendants);
            int descCount = 0;
            if (hr.Succeeded && descendants != null)
            {
                descendants->get_Length(&descCount);
            }

            if (descCount == 0)
            {
                return 0;
            }

            var maxRight = int.MinValue;

            for (var i = 0; i < descCount; i++)
            {
                IUIAutomationElement* desc = null;
                try
                {
                    hr = descendants->GetElement(i, &desc);
                    if (hr.Failed || desc == null)
                    {
                        continue;
                    }

                    // Only count buttons (UIA_ButtonControlTypeId = 50000)
                    VARIANT varType = default;
                    desc->GetCurrentPropertyValue(UIA_PROPERTY_ID.UIA_ControlTypePropertyId, &varType);
                    var typeId = 0;
                    if (varType.Anonymous.Anonymous.vt == VARENUM.VT_I4)
                    {
                        typeId = varType.Anonymous.Anonymous.Anonymous.lVal;
                    }

                    PInvoke.VariantClear(&varType);

                    if (typeId != 50000)
                    {
                        continue;
                    }

                    VARIANT varRect = default;
                    hr = desc->GetCurrentPropertyValue(
                        UIA_PROPERTY_ID.UIA_BoundingRectanglePropertyId, &varRect);

                    if (hr.Succeeded &&
                        varRect.Anonymous.Anonymous.vt == (VARENUM.VT_R8 | VARENUM.VT_ARRAY))
                    {
                        var psa = varRect.Anonymous.Anonymous.Anonymous.parray;
                        if (psa != null)
                        {
                            double x = 0, w = 0;
                            int idx = 0;
                            PInvoke.SafeArrayGetElement(psa, &idx, &x);
                            idx = 2;
                            PInvoke.SafeArrayGetElement(psa, &idx, &w);

                            var btnLeft = (int)x;
                            var btnRight = (int)(x + w);

                            // Count buttons to the left of the tray area
                            if (btnLeft < rightBoundary)
                            {
                                if (btnRight > maxRight)
                                {
                                    maxRight = btnRight;
                                }

                                buttonCount++;
                            }
                        }
                    }

                    PInvoke.VariantClear(&varRect);
                }
                finally
                {
                    if (desc != null)
                    {
                        ((IUnknown*)desc)->Release();
                    }
                }
            }

            if (buttonCount == 0)
            {
                return 0;
            }

            return maxRight > taskbarRect.left ? maxRight - taskbarRect.left : 0;
        }
        finally
        {
            if (descendants != null)
            {
                ((IUnknown*)descendants)->Release();
            }

            if (children != null)
            {
                ((IUnknown*)children)->Release();
            }

            if (root != null)
            {
                ((IUnknown*)root)->Release();
            }
        }
    }

    private static int MeasureTray(HWND taskbarHwnd)
    {
        var tray = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "TrayNotifyWnd", null);
        if (tray.IsNull)
        {
            return 0;
        }

        PInvoke.GetWindowRect(tray, out var rect);
        return rect.Width;
    }

    private void EnsureAutomation()
    {
        if (_automation != null)
        {
            return;
        }

        IUIAutomation* automation;
        var hr = PInvoke.CoCreateInstance(
            CUIAutomationClsid,
            (IUnknown*)null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            out automation);

        if (hr.Failed || automation == null)
        {
            return;
        }

        _automation = automation;

        IUIAutomationCondition* condition;
        hr = _automation->CreateTrueCondition(&condition);
        if (hr.Succeeded)
        {
            _trueCondition = condition;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_trueCondition != null)
            {
                ((IUnknown*)_trueCondition)->Release();
                _trueCondition = null;
            }

            if (_automation != null)
            {
                ((IUnknown*)_automation)->Release();
                _automation = null;
            }

            _disposed = true;
        }
    }
}
