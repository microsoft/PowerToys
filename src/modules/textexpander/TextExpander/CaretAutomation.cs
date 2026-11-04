// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Asks UI Automation where the caret is in the focused element, for apps that draw their own: TextPattern2's caret
/// range, or else TextPattern's selection, and the element's bounds. It runs in a multithreaded apartment on a thread
/// of its own, with UI Automation's own timeouts cut short, and the caller waits <see cref="TimeoutMs"/> at most.
/// COM is called through each interface's function table, at the slots UIAutomationClient.h gives its methods.
/// </summary>
internal static unsafe class CaretAutomation
{
    /// <summary>How long the caller waits for UI Automation, and how long UI Automation waits for the app.</summary>
    internal const int TimeoutMs = 400;

    // IUIAutomation, and IUIAutomation2 after it.
    private const int GetFocusedElementSlot = 8;
    private const int PutConnectionTimeoutSlot = 61;
    private const int PutTransactionTimeoutSlot = 63;

    // IUIAutomationElement.
    private const int GetCurrentPatternAsSlot = 14;
    private const int GetCurrentBoundingRectangleSlot = 43;

    // IUIAutomationTextPattern, and IUIAutomationTextPattern2 after it.
    private const int GetSelectionSlot = 5;
    private const int GetCaretRangeSlot = 10;

    // IUIAutomationTextRangeArray.
    private const int GetLengthSlot = 3;
    private const int GetElementSlot = 4;

    // IUIAutomationTextRange.
    private const int CloneSlot = 3;
    private const int GetBoundingRectanglesSlot = 10;
    private const int MoveEndpointByUnitSlot = 14;
    private const int MoveEndpointByRangeSlot = 15;

    private static readonly Guid CUIAutomation8 = new("e22ad333-b25f-460c-83d0-0581107395c9");
    private static readonly Guid CUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
    private static readonly Guid IUIAutomation = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
    private static readonly Guid IUIAutomation2 = new("34723aff-0c9d-49d0-9896-7ab52df8cd8a");
    private static readonly Guid IUIAutomationTextPattern = new("32eba289-3583-42c9-9c59-3b6d9a1e9b6a");
    private static readonly Guid IUIAutomationTextPattern2 = new("506a921a-fcc9-409f-b23b-37eb74106872");

    private static readonly TimedWorker Worker = new(
        "PowerToys Text Expander caret",
        ApartmentState.MTA,
        static () => _ = Native.CoInitializeEx(IntPtr.Zero, Native.COINIT_MULTITHREADED));

    // Made, and used, on the worker's thread only.
    private static IntPtr _automation;

    /// <summary>Gets what UI Automation finds in the focused element, or null when it can't tell in time.</summary>
    public static CaretLocator.Focus? Find() => Worker.TryRun(Query, TimeoutMs, out CaretLocator.Focus? focus) ? focus : null;

    private static CaretLocator.Focus? Query()
    {
        IntPtr automation = Automation();
        if (automation == IntPtr.Zero)
        {
            return null;
        }

        IntPtr element = IntPtr.Zero;
        try
        {
            if (Failed(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(automation, GetFocusedElementSlot))(automation, &element)) || element == IntPtr.Zero)
            {
                return null;
            }

            Native.RECT bounds = default;
            Native.RECT? elementBounds = Succeeded(((delegate* unmanaged[Stdcall]<IntPtr, Native.RECT*, int>)Slot(element, GetCurrentBoundingRectangleSlot))(element, &bounds))
                ? bounds
                : null;

            // TextPattern2 is a TextPattern too, so its selection is there when its caret range isn't.
            IntPtr textPattern2 = Pattern(element, Native.UIA_TextPattern2Id, IUIAutomationTextPattern2);
            IntPtr textPattern = textPattern2 != IntPtr.Zero ? textPattern2 : Pattern(element, Native.UIA_TextPatternId, IUIAutomationTextPattern);
            try
            {
                Native.RECT? caret = textPattern2 != IntPtr.Zero ? CaretRange(textPattern2) : null;
                Native.RECT? selection = caret is null && textPattern != IntPtr.Zero ? Selection(textPattern) : null;
                return new CaretLocator.Focus(caret, selection, elementBounds);
            }
            finally
            {
                Release(textPattern);
            }
        }
        finally
        {
            Release(element);
        }
    }

    private static IntPtr Automation()
    {
        if (_automation != IntPtr.Zero)
        {
            return _automation;
        }

        if (Native.CoCreateInstance(CUIAutomation8, IntPtr.Zero, Native.CLSCTX_INPROC_SERVER, IUIAutomation, out IntPtr automation) < 0
            && Native.CoCreateInstance(CUIAutomation, IntPtr.Zero, Native.CLSCTX_INPROC_SERVER, IUIAutomation, out automation) < 0)
        {
            return IntPtr.Zero;
        }

        // An app that's hung or busy is given up on quickly, rather than after UI Automation's default 20 seconds.
        if (Marshal.QueryInterface(automation, in IUIAutomation2, out IntPtr automation2) >= 0)
        {
            _ = ((delegate* unmanaged[Stdcall]<IntPtr, uint, int>)Slot(automation2, PutConnectionTimeoutSlot))(automation2, TimeoutMs);
            _ = ((delegate* unmanaged[Stdcall]<IntPtr, uint, int>)Slot(automation2, PutTransactionTimeoutSlot))(automation2, TimeoutMs);
            Release(automation2);
        }

        _automation = automation;
        return automation;
    }

    private static IntPtr Pattern(IntPtr element, int patternId, Guid iid)
    {
        IntPtr pattern = IntPtr.Zero;
        return Succeeded(((delegate* unmanaged[Stdcall]<IntPtr, int, Guid*, IntPtr*, int>)Slot(element, GetCurrentPatternAsSlot))(element, patternId, &iid, &pattern))
            ? pattern
            : IntPtr.Zero;
    }

    // TextPattern2's caret range is empty, at the caret.
    private static Native.RECT? CaretRange(IntPtr textPattern2)
    {
        int active = 0;
        IntPtr range = IntPtr.Zero;
        if (Failed(((delegate* unmanaged[Stdcall]<IntPtr, int*, IntPtr*, int>)Slot(textPattern2, GetCaretRangeSlot))(textPattern2, &active, &range)) || range == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return CaretIn(range, atEnd: false);
        }
        finally
        {
            Release(range);
        }
    }

    // After typing, the selection is empty, at the caret; one that isn't has the caret at its end.
    private static Native.RECT? Selection(IntPtr textPattern)
    {
        IntPtr ranges = IntPtr.Zero;
        if (Failed(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(textPattern, GetSelectionSlot))(textPattern, &ranges)) || ranges == IntPtr.Zero)
        {
            return null;
        }

        IntPtr range = IntPtr.Zero;
        try
        {
            int length = 0;
            if (Failed(((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Slot(ranges, GetLengthSlot))(ranges, &length)) || length < 1
                || Failed(((delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr*, int>)Slot(ranges, GetElementSlot))(ranges, length - 1, &range)) || range == IntPtr.Zero)
            {
                return null;
            }

            return CaretIn(range, atEnd: true);
        }
        finally
        {
            Release(range);
            Release(ranges);
        }
    }

    private static Native.RECT? CaretIn(IntPtr range, bool atEnd)
        => CaretLocator.CaretInRange(() => Bounds(range), () => Character(range, atEnd, after: true), () => Character(range, atEnd, after: false), atEnd);

    // The bounds of the character after the caret's end of the range, or of the one before it; none at either end of
    // the text. ExpandToEnclosingUnit would give the last character instead of none after the end.
    private static double[]? Character(IntPtr range, bool atEnd, bool after)
    {
        IntPtr clone = IntPtr.Zero;
        if (Failed(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(range, CloneSlot))(range, &clone)) || clone == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            int collapse = atEnd
                ? ((delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int, int>)Slot(clone, MoveEndpointByRangeSlot))(clone, Native.TextPatternRangeEndpoint_Start, clone, Native.TextPatternRangeEndpoint_End)
                : ((delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int, int>)Slot(clone, MoveEndpointByRangeSlot))(clone, Native.TextPatternRangeEndpoint_End, clone, Native.TextPatternRangeEndpoint_Start);
            int moved = 0;
            int result = after
                ? ((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, int*, int>)Slot(clone, MoveEndpointByUnitSlot))(clone, Native.TextPatternRangeEndpoint_End, Native.TextUnit_Character, 1, &moved)
                : ((delegate* unmanaged[Stdcall]<IntPtr, int, int, int, int*, int>)Slot(clone, MoveEndpointByUnitSlot))(clone, Native.TextPatternRangeEndpoint_Start, Native.TextUnit_Character, -1, &moved);
            return Succeeded(collapse) && Succeeded(result) && moved != 0 ? Bounds(clone) : null;
        }
        finally
        {
            Release(clone);
        }
    }

    private static double[]? Bounds(IntPtr range)
    {
        IntPtr array = IntPtr.Zero;
        return Succeeded(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(range, GetBoundingRectanglesSlot))(range, &array))
            ? ReadDoubles(array)
            : null;
    }

    // A SAFEARRAY of doubles, which is destroyed once read.
    private static double[]? ReadDoubles(IntPtr array)
    {
        if (array == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (Native.SafeArrayGetVartype(array, out ushort type) < 0 || type != Native.VT_R8
                || Native.SafeArrayGetLBound(array, 1, out int lower) < 0 || Native.SafeArrayGetUBound(array, 1, out int upper) < 0)
            {
                return null;
            }

            int count = upper - lower + 1;
            if (count <= 0)
            {
                return [];
            }

            if (Native.SafeArrayAccessData(array, out IntPtr data) < 0)
            {
                return null;
            }

            try
            {
                var values = new double[count];
                Marshal.Copy(data, values, 0, count);
                return values;
            }
            finally
            {
                _ = Native.SafeArrayUnaccessData(array);
            }
        }
        finally
        {
            _ = Native.SafeArrayDestroy(array);
        }
    }

    private static void* Slot(IntPtr instance, int slot) => (*(void***)instance)[slot];

    private static void Release(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            _ = Marshal.Release(instance);
        }
    }

    private static bool Succeeded(int result) => result >= 0;

    private static bool Failed(int result) => result < 0;
}
