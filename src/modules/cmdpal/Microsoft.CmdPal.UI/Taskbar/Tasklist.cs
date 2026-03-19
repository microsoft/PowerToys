// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ManagedCsWin32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Taskbar;

/// <summary>
/// Provides functionality to interact with and retrieve information about Windows taskbar buttons.
/// </summary>
public sealed partial class Tasklist : IDisposable
{
    private IUIAutomation? _automation;
    private IUIAutomationElement? _element;
    private IUIAutomationElement? _shellTrayElement;
    private IUIAutomationCondition? _trueCondition;
    private bool _disposed;

    /// <summary>
    /// Updates the internal references to the Windows taskbar.
    /// </summary>
    public void Update()
    {
        ThrowIfDisposed();

        // Get HWND of the tasklist by walking the window hierarchy
        var shellTrayHwnd = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (shellTrayHwnd.IsNull)
        {
            return;
        }

        // Initialize UI Automation if not already done
        if (_automation == null)
        {
            _automation = ComHelper.CreateComInstance<IUIAutomation>(
                ref Unsafe.AsRef(in UIAutomationClsids.CUIAutomation),
                CLSCTX.InProcServer);
            _ = _automation.CreateTrueCondition(out _trueCondition);
        }

        // Always get the Shell_TrayWnd element as a fallback
        _ = _automation.ElementFromHandle((nint)shellTrayHwnd, out _shellTrayElement);

        // Try to navigate to MSTaskListWClass (Windows 10 / older Win11 path)
        var tasklistHwnd = PInvoke.FindWindowEx(shellTrayHwnd, HWND.Null, "ReBarWindow32", null);
        if (!tasklistHwnd.IsNull)
        {
            tasklistHwnd = PInvoke.FindWindowEx(tasklistHwnd, HWND.Null, "MSTaskSwWClass", null);
        }

        if (!tasklistHwnd.IsNull)
        {
            tasklistHwnd = PInvoke.FindWindowEx(tasklistHwnd, HWND.Null, "MSTaskListWClass", null);
        }

        if (!tasklistHwnd.IsNull)
        {
            _ = _automation.ElementFromHandle((nint)tasklistHwnd, out _element);
        }
        else
        {
            _element = null;
        }
    }

    /// <summary>
    /// Updates the provided list with current taskbar buttons.
    /// </summary>
    /// <param name="buttons">The list to populate with taskbar buttons.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool UpdateButtons(List<TasklistButton> buttons)
    {
        ThrowIfDisposed();

        if (_automation == null || _trueCondition == null)
        {
            return false;
        }

        try
        {
            List<TasklistButton>? foundButtons = null;

            // First try: MSTaskListWClass children (Windows 10 / older Win11)
            if (_element != null)
            {
                foundButtons = FindButtonsFromElement(_element, (int)TreeScope.TreeScope_Children);

                // If no direct children, try descendants (buttons may be nested)
                if (foundButtons.Count == 0)
                {
                    foundButtons = FindButtonsFromElement(_element, (int)TreeScope.TreeScope_Descendants);
                }
            }

            //// Fallback: Shell_TrayWnd descendants (Windows 11 XAML-based taskbar)
            // if ((foundButtons == null || foundButtons.Count == 0) && _shellTrayElement != null)
            // {
            //    foundButtons = FindButtonsFromElement(_shellTrayElement, (int)TreeScope.TreeScope_Descendants);
            // }
            if (foundButtons == null || foundButtons.Count == 0)
            {
                return false;
            }

            // Assign key numbers and filter buttons
            AssignKeyNumbers(foundButtons, buttons);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds taskbar buttons from the given UIA element using the specified tree scope.
    /// </summary>
    private List<TasklistButton> FindButtonsFromElement(IUIAutomationElement element, int treeScope)
    {
        var hr = element.FindAll(treeScope, _trueCondition!, out var elements);
        if (hr != 0 || elements == null)
        {
            return new List<TasklistButton>();
        }

        hr = elements.get_Length(out var count);
        if (hr != 0)
        {
            return new List<TasklistButton>();
        }

        List<TasklistButton> foundButtons = new(count);

        for (var i = 0; i < count; i++)
        {
            hr = elements.GetElement(i, out var child);
            if (hr != 0 || child == null)
            {
                continue;
            }

            var button = CreateTasklistButton(child);
            if (button != null)
            {
                foundButtons.Add(button);
            }
        }

        return foundButtons;
    }

    /// <summary>
    /// Gets the current taskbar buttons.
    /// </summary>
    /// <returns>A list of taskbar buttons.</returns>
    public List<TasklistButton> GetButtons()
    {
        List<TasklistButton> buttons = new();
        UpdateButtons(buttons);
        return buttons;
    }

    /// <summary>
    /// Creates a TasklistButton from a UI automation element.
    /// </summary>
    /// <param name="element">The UI automation element.</param>
    /// <returns>A TasklistButton if successful, null otherwise.</returns>
    private static TasklistButton? CreateTasklistButton(IUIAutomationElement element)
    {
        try
        {
            //// Filter by control type: only accept Button elements (50000 = UIA_ButtonControlTypeId)
            // var hr = element.get_CurrentControlType(out var controlType);
            // if (hr != 0 || controlType != 50000)
            // {
            //    return null;
            // }

            // Get bounding rectangle
            hr = element.GetCurrentPropertyValue(
                (int)UIA_PROPERTY_ID.UIA_BoundingRectanglePropertyId,
                out var boundingRect);
            if (hr != 0)
            {
                return null;
            }

            var rectArray = NativeVariantHelper.ExtractDoubleArray(ref boundingRect);
            NativeVariantHelper.Clear(ref boundingRect);
            if (rectArray is null)
            {
                return null;
            }

            if (rectArray.Length < 4)
            {
                return null;
            }

            // Get automation ID (name)
            hr = element.get_CurrentAutomationId(out var automationId);
            if (hr != 0)
            {
                return null;
            }

            return new TasklistButton
            {
                Name = automationId ?? string.Empty,
                X = (int)rectArray[0],
                Y = (int)rectArray[1],
                Width = (int)rectArray[2],
                Height = (int)rectArray[3],
                KeyNum = 0, // Will be assigned later
            };
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>
    /// Assigns key numbers to buttons and filters the result.
    /// </summary>
    /// <param name="foundButtons">The buttons found via automation.</param>
    /// <param name="buttons">The output list to populate.</param>
    private static void AssignKeyNumbers(List<TasklistButton> foundButtons, List<TasklistButton> buttons)
    {
        buttons.Clear();

        foreach (var button in foundButtons)
        {
            if (buttons.Count == 0)
            {
                buttons.Add(button with { KeyNum = 1 });
            }
            else
            {
                var lastButton = buttons[^1];

                // Skip buttons on second row (lower Y coordinate or significantly left of previous)
                if (button.X < lastButton.X || button.Y < lastButton.Y)
                {
                    break;
                }

                // Skip buttons from the same app (same name)
                if (button.Name == lastButton.Name)
                {
                    continue;
                }

                var nextKeyNum = lastButton.KeyNum + 1;
                buttons.Add(button with { KeyNum = nextKeyNum });

                // Limit to 10 buttons
                if (nextKeyNum == 10)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the object has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Releases the COM objects used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // ComWrappers-based objects are released by the GC finalizer.
            // Marshal.ReleaseComObject is not compatible with StrategyBasedComWrappers.
            _trueCondition = null;
            _element = null;
            _shellTrayElement = null;
            _automation = null;

            _disposed = true;
        }
    }
}
