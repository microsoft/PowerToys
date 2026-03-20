// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Accessibility;

namespace TaskbarMonitor;

/// <summary>
/// Polls the Windows taskbar using P/Invoke and UI Automation
/// (preserveSig / no-marshalling) for AOT compatibility.
/// Call <see cref="SetDpiAwareness"/> once at process start.
/// </summary>
public sealed unsafe class TaskbarPoller : IDisposable
{
    // CUIAutomation CLSID: {FF48DBA4-60EF-4201-AA87-54103EEF594E}
    private static readonly Guid CLSID_CUIAutomation =
        new(0xFF48DBA4, 0x60EF, 0x4201, 0xAA, 0x87, 0x54, 0x10, 0x3E, 0xEF, 0x59, 0x4E);

    private IUIAutomation* _automation;
    private IUIAutomationCondition* _trueCondition;
    private bool _disposed;

    public static void SetDpiAwareness()
    {
        PInvoke.SetProcessDpiAwarenessContext(
            new Windows.Win32.UI.HiDpi.DPI_AWARENESS_CONTEXT((void*)-4));
    }

    public List<TaskbarSnapshot> PollAll(TextWriter? log = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureAutomation(log);
        var results = new List<TaskbarSnapshot>();

        var primary = PInvoke.FindWindow("Shell_TrayWnd", null);
        log?.WriteLine($"[poll] Shell_TrayWnd = 0x{(nint)primary.Value:X}");
        if (!primary.IsNull)
        {
            results.Add(SnapshotTaskbar(primary, isPrimary: true, log));
        }

        var secondary = HWND.Null;
        while (true)
        {
            secondary = PInvoke.FindWindowEx(
                HWND.Null, secondary, "Shell_SecondaryTrayWnd", null);
            if (secondary.IsNull)
            {
                break;
            }

            log?.WriteLine($"[poll] Shell_SecondaryTrayWnd = 0x{(nint)secondary.Value:X}");
            results.Add(SnapshotTaskbar(secondary, isPrimary: false, log));
        }

        return results;
    }

    private TaskbarSnapshot SnapshotTaskbar(HWND taskbarHwnd, bool isPrimary, TextWriter? log)
    {
        PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
        var dpi = PInvoke.GetDpiForWindow(taskbarHwnd);
        var isBottom = taskbarRect.Width > taskbarRect.Height;

        log?.WriteLine($"[snap] rect=({taskbarRect.left},{taskbarRect.top},{taskbarRect.right},{taskbarRect.bottom}) dpi={dpi} isBottom={isBottom}");

        int buttonsWidth = 0;
        int trayWidth = 0;
        int buttonCount = 0;

        if (isBottom)
        {
            buttonsWidth = MeasureTaskbarButtons(taskbarHwnd, out buttonCount, log);
            trayWidth = MeasureTray(taskbarHwnd);
            log?.WriteLine($"[snap] buttonsWidth={buttonsWidth} buttonCount={buttonCount} trayWidth={trayWidth}");
        }

        return new TaskbarSnapshot
        {
            IsPrimary = isPrimary,
            TaskbarWidth = taskbarRect.Width,
            TaskbarHeight = taskbarRect.Height,
            IsBottom = isBottom,
            ButtonsWidth = buttonsWidth,
            TrayWidth = trayWidth,
            Dpi = dpi,
            ButtonCount = buttonCount,
        };
    }

    private int MeasureTaskbarButtons(HWND taskbarHwnd, out int buttonCount, TextWriter? log)
    {
        buttonCount = 0;

        if (_automation == null)
        {
            log?.WriteLine("[buttons] _automation is null");
            return 0;
        }

        IUIAutomationElement* root = null;
        IUIAutomationElementArray* children = null;
        IUIAutomationElementArray* descendants = null;
        try
        {
            var hr = _automation->ElementFromHandle(taskbarHwnd, &root);
            log?.WriteLine($"[buttons] ElementFromHandle(taskbar) hr=0x{hr.Value:X8} root={(nint)root:X}");
            if (hr.Failed || root == null)
            {
                return 0;
            }

            // Step 1: Find the MSTaskSwWClass or MSTaskListWClass child to
            // determine the clip region for the running-app buttons area.
            hr = root->FindAll(TreeScope.TreeScope_Children, _trueCondition, &children);
            int childCount = 0;
            if (hr.Succeeded && children != null)
            {
                children->get_Length(&childCount);
            }

            double clipX = 0, clipY = 0, clipW = 0, clipH = 0;
            var foundClip = false;

            for (var i = 0; i < childCount; i++)
            {
                IUIAutomationElement* child = null;
                try
                {
                    hr = children->GetElement(i, &child);
                    if (hr.Failed || child == null)
                    {
                        continue;
                    }

                    VARIANT varClass = default;
                    child->GetCurrentPropertyValue(UIA_PROPERTY_ID.UIA_ClassNamePropertyId, &varClass);
                    var isTaskArea = false;
                    if (varClass.Anonymous.Anonymous.vt == VARENUM.VT_BSTR &&
                        varClass.Anonymous.Anonymous.Anonymous.bstrVal.Value != null)
                    {
                        var className = new string(varClass.Anonymous.Anonymous.Anonymous.bstrVal.Value);
                        log?.WriteLine($"[buttons]   child[{i}] class={className}");
                        isTaskArea = className is "MSTaskSwWClass" or "MSTaskListWClass";
                    }

                    PInvoke.VariantClear(&varClass);

                    if (isTaskArea && !foundClip)
                    {
                        VARIANT varRect = default;
                        child->GetCurrentPropertyValue(UIA_PROPERTY_ID.UIA_BoundingRectanglePropertyId, &varRect);
                        if (varRect.Anonymous.Anonymous.vt == (VARENUM.VT_R8 | VARENUM.VT_ARRAY))
                        {
                            var psa = varRect.Anonymous.Anonymous.Anonymous.parray;
                            if (psa != null)
                            {
                                int idx = 0;
                                PInvoke.SafeArrayGetElement(psa, &idx, &clipX);
                                idx = 1; PInvoke.SafeArrayGetElement(psa, &idx, &clipY);
                                idx = 2; PInvoke.SafeArrayGetElement(psa, &idx, &clipW);
                                idx = 3; PInvoke.SafeArrayGetElement(psa, &idx, &clipH);
                                foundClip = true;
                                log?.WriteLine($"[buttons]   clip region: x={clipX} y={clipY} w={clipW} h={clipH}");
                            }
                        }

                        PInvoke.VariantClear(&varRect);
                    }
                }
                finally
                {
                    if (child != null)
                    {
                        ((IUnknown*)child)->Release();
                    }
                }
            }

            if (!foundClip)
            {
                log?.WriteLine("[buttons] No task area child found");
                return 0;
            }

            // Step 2: Enumerate ALL descendants of the entire taskbar.
            // The buttons live inside the XAML Islands window, not as
            // direct UIA children of MSTaskSwWClass. Filter by position:
            // buttons start at clipLeft and go until the tray area.
            hr = root->FindAll(TreeScope.TreeScope_Descendants, _trueCondition, &descendants);
            int descCount = 0;
            if (hr.Succeeded && descendants != null)
            {
                descendants->get_Length(&descCount);
            }

            log?.WriteLine($"[buttons] Descendants count={descCount}");

            // The right boundary is the tray area's left edge (if present),
            // otherwise the taskbar's right edge.
            PInvoke.GetWindowRect(taskbarHwnd, out var taskbarRect);
            var trayHwnd = PInvoke.FindWindowEx(taskbarHwnd, HWND.Null, "TrayNotifyWnd", null);
            int rightBoundary = taskbarRect.right;
            if (!trayHwnd.IsNull)
            {
                PInvoke.GetWindowRect(trayHwnd, out var trayRect);
                rightBoundary = trayRect.left;
            }

            var clipLeft = (int)clipX;
            log?.WriteLine($"[buttons] clipLeft={clipLeft} rightBoundary={rightBoundary}");
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

                    // Only count buttons (ControlType = 50000)
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
                            double x = 0, y = 0, w = 0, h = 0;
                            int idx = 0;
                            PInvoke.SafeArrayGetElement(psa, &idx, &x);
                            idx = 1; PInvoke.SafeArrayGetElement(psa, &idx, &y);
                            idx = 2; PInvoke.SafeArrayGetElement(psa, &idx, &w);
                            idx = 3; PInvoke.SafeArrayGetElement(psa, &idx, &h);

                            var btnLeft = (int)x;
                            var btnRight = (int)(x + w);

                            // Count buttons to the left of the tray area
                            if (btnLeft < rightBoundary)
                            {
                                if (buttonCount < 3)
                                {
                                    log?.WriteLine($"[buttons]   btn[{buttonCount}] x={x} y={y} w={w} h={h} right={btnRight}");
                                }

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

            log?.WriteLine($"[buttons] maxRight={maxRight} taskbar.left={taskbarRect.left} buttonCount={buttonCount}");

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

    private void EnsureAutomation(TextWriter? log = null)
    {
        if (_automation != null)
        {
            return;
        }

        IUIAutomation* automation;
        var hr = PInvoke.CoCreateInstance(
            CLSID_CUIAutomation,
            (IUnknown*)null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            out automation);

        log?.WriteLine($"[uia] CoCreateInstance hr=0x{hr.Value:X8} ptr={(nint)automation:X}");

        if (hr.Failed || automation == null)
        {
            return;
        }

        _automation = automation;

        IUIAutomationCondition* condition;
        hr = _automation->CreateTrueCondition(&condition);
        log?.WriteLine($"[uia] CreateTrueCondition hr=0x{hr.Value:X8} ptr={(nint)condition:X}");
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
