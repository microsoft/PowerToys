// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>Identifies which screen edge the taskbar is docked to.</summary>
public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right,
}

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

    /// <summary>Which screen edge the taskbar is docked to.</summary>
    public TaskbarEdge Edge { get; private set; }

    /// <summary>True when the taskbar is on the top or bottom edge.</summary>
    public bool IsHorizontal => Edge is TaskbarEdge.Bottom or TaskbarEdge.Top;

    /// <summary>
    /// Re-measures the primary taskbar. Returns true if any value changed.
    /// Thread-safe — can be called from any thread.
    /// </summary>
    public bool Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var taskbarHwnd = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd.IsNull)
        {
            return false;
        }

        DetectEdge(taskbarHwnd);

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

    /// <summary>
    /// Re-measures the primary taskbar on a background thread.
    /// Returns true if any value changed.
    /// </summary>
    public Task<bool> UpdateAsync() => Task.Run(Update);

    private int MeasureButtons(HWND taskbarHwnd, out int buttonCount)
    {
        buttonCount = 0;
        EnsureAutomation();

        if (_automation == null)
        {
            return 0;
        }

        IUIAutomationElement* root = null;
        IUIAutomationElementArray* descendants = null;
        try
        {
            var hr = _automation->ElementFromHandle(taskbarHwnd, &root);
            if (hr.Failed || root == null)
            {
                return 0;
            }

            // Determine the boundary: buttons stop where the tray begins.
            // For horizontal taskbars this is the tray's left edge;
            // for vertical taskbars it's the tray's top edge.
            PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
            var trayHwnd = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "TrayNotifyWnd", null);
            int boundary;
            if (IsHorizontal)
            {
                boundary = taskbarRect.right;
                if (!trayHwnd.IsNull)
                {
                    PInvoke.GetWindowRect(trayHwnd, out var trayRect);
                    boundary = trayRect.left;
                }
            }
            else
            {
                boundary = taskbarRect.bottom;
                if (!trayHwnd.IsNull)
                {
                    PInvoke.GetWindowRect(trayHwnd, out var trayRect);
                    boundary = trayRect.top;
                }
            }

            // Enumerate all UIA descendants of the taskbar.
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
                            // Horizontal: X (0) + Width (2)
                            // Vertical:   Y (1) + Height (3)
                            int posIdx = IsHorizontal ? 0 : 1;
                            int sizeIdx = IsHorizontal ? 2 : 3;
                            double pos = 0, size = 0;
                            PInvoke.SafeArrayGetElement(psa, &posIdx, &pos);
                            PInvoke.SafeArrayGetElement(psa, &sizeIdx, &size);

                            var btnStart = (int)pos;
                            var btnEnd = (int)(pos + size);

                            // Count buttons before the tray area.
                            // CmdPal's own controls are UserControls, not
                            // UIA buttons, so the typeId==50000 filter above
                            // already excludes them.
                            if (btnStart < boundary)
                            {
                                if (btnEnd > maxRight)
                                {
                                    maxRight = btnEnd;
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

            var taskbarStart = IsHorizontal ? taskbarRect.left : taskbarRect.top;
            return maxRight > taskbarStart ? maxRight - taskbarStart : 0;
        }
        finally
        {
            if (descendants != null)
            {
                ((IUnknown*)descendants)->Release();
            }

            if (root != null)
            {
                ((IUnknown*)root)->Release();
            }
        }
    }

    private int MeasureTray(HWND taskbarHwnd)
    {
        var tray = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "TrayNotifyWnd", null);
        if (tray.IsNull)
        {
            return 0;
        }

        // Measure from the tray's near edge to the taskbar's far edge.
        // For horizontal: tray left → taskbar right (width).
        // For vertical:   tray top  → taskbar bottom (height).
        PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
        PInvoke.GetWindowRect(tray, out var trayRect);
        var result = IsHorizontal
            ? taskbarRect.right - trayRect.left
            : taskbarRect.bottom - trayRect.top;
        System.Diagnostics.Debug.WriteLine($"MeasureTray: taskbar=({taskbarRect.left},{taskbarRect.top},{taskbarRect.right},{taskbarRect.bottom}) tray=({trayRect.left},{trayRect.top},{trayRect.right},{trayRect.bottom}) edge={Edge} RESULT={result}");
        return result;
    }

    private void DetectEdge(HWND taskbarHwnd)
    {
        PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
        var monitor = PInvoke.MonitorFromWindow(taskbarHwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(monitor, ref monitorInfo);
        var screen = monitorInfo.rcMonitor;

        if (taskbarRect.Width >= screen.Width)
        {
            Edge = taskbarRect.top <= screen.top ? TaskbarEdge.Top : TaskbarEdge.Bottom;
        }
        else
        {
            Edge = taskbarRect.left <= screen.left ? TaskbarEdge.Left : TaskbarEdge.Right;
        }
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
