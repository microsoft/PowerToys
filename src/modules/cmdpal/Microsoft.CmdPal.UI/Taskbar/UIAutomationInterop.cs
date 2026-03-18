// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.UI.Taskbar;

#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// CLSID for the CUIAutomation COM class.
/// </summary>
internal static class UIAutomationClsids
{
    internal static readonly Guid CUIAutomation = new("ff48dba4-60ef-4201-aa87-54103eef594e");
}

/// <summary>
/// Blittable representation of a COM VARIANT.
/// 16 bytes on x86, 24 bytes on x64 (the union includes BRECORD which is two pointers).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeVariant
{
    public ushort Vt;
    public ushort WReserved1;
    public ushort WReserved2;
    public ushort WReserved3;
    public nint Data1;
    public nint Data2;

    /// <summary>Gets the first data field as a pointer (e.g. SAFEARRAY*, BSTR, IUnknown*).</summary>
    public readonly nint PointerValue => Data1;
}

/// <summary>
/// Helper methods for extracting data from <see cref="NativeVariant"/> values.
/// </summary>
internal static partial class NativeVariantHelper
{
    private const ushort VTARRAY = 0x2000;
    private const ushort VTR8 = 5;

    [LibraryImport("oleaut32.dll")]
    private static partial int VariantClear(ref NativeVariant pvarg);

    [LibraryImport("oleaut32.dll")]
    private static partial int SafeArrayAccessData(nint psa, out nint ppvData);

    [LibraryImport("oleaut32.dll")]
    private static partial int SafeArrayUnaccessData(nint psa);

    [LibraryImport("oleaut32.dll")]
    private static partial int SafeArrayGetLBound(nint psa, uint nDim, out int plLbound);

    [LibraryImport("oleaut32.dll")]
    private static partial int SafeArrayGetUBound(nint psa, uint nDim, out int plUbound);

    /// <summary>
    /// Clears a <see cref="NativeVariant"/>, releasing any owned resources.
    /// </summary>
    public static void Clear(ref NativeVariant variant)
    {
        VariantClear(ref variant);
        variant = default;
    }

    /// <summary>
    /// Extracts a double array from a VT_ARRAY|VT_R8 variant.
    /// </summary>
    public static double[]? ExtractDoubleArray(ref NativeVariant variant)
    {
        if (variant.Vt != (VTARRAY | VTR8))
        {
            return null;
        }

        var psa = variant.PointerValue;
        if (psa == 0)
        {
            return null;
        }

        SafeArrayGetLBound(psa, 1, out var lBound);
        SafeArrayGetUBound(psa, 1, out var uBound);
        var count = uBound - lBound + 1;

        if (count <= 0)
        {
            return null;
        }

        if (SafeArrayAccessData(psa, out var dataPtr) != 0)
        {
            return null;
        }

        try
        {
            var result = new double[count];
            Marshal.Copy(dataPtr, result, 0, count);
            return result;
        }
        finally
        {
            _ = SafeArrayUnaccessData(psa);
        }
    }
}

/// <summary>
/// AOT-safe version of IUIAutomationCondition.
/// </summary>
[GeneratedComInterface]
[Guid("352FFBA8-0973-437C-A61F-F64CAFD81DF9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUIAutomationCondition
{
}

/// <summary>
/// AOT-safe version of IUIAutomationElementArray.
/// </summary>
[GeneratedComInterface]
[Guid("14314595-B4BC-4055-95F2-58F2E42C9855")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUIAutomationElementArray
{
    [PreserveSig]
    int get_Length(out int length);

    [PreserveSig]
    int GetElement(int index, out IUIAutomationElement? element);
}

/// <summary>
/// AOT-safe version of IUIAutomationElement.
/// All vtable slots from 0 to 26 are declared to preserve correct offsets.
/// Only FindAll (3), GetCurrentPropertyValue (7), and get_CurrentAutomationId (26) are used.
/// </summary>
[GeneratedComInterface]
[Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUIAutomationElement
{
    // Slot 0: SetFocus
    [PreserveSig]
    int SetFocus();

    // Slot 1: GetRuntimeId — returns SAFEARRAY*, use nint stub
    [PreserveSig]
    int GetRuntimeId(out nint runtimeId);

    // Slot 2: FindFirst
    [PreserveSig]
    int FindFirst(int scope, nint condition, out nint found);

    // Slot 3: FindAll
    [PreserveSig]
    int FindAll(int scope, IUIAutomationCondition condition, out IUIAutomationElementArray? found);

    // Slot 4: FindFirstBuildCache
    [PreserveSig]
    int FindFirstBuildCache(int scope, nint condition, nint cacheRequest, out nint found);

    // Slot 5: FindAllBuildCache
    [PreserveSig]
    int FindAllBuildCache(int scope, nint condition, nint cacheRequest, out nint found);

    // Slot 6: BuildUpdatedCache
    [PreserveSig]
    int BuildUpdatedCache(nint cacheRequest, out nint updatedElement);

    // Slot 7: GetCurrentPropertyValue — returns VARIANT
    [PreserveSig]
    int GetCurrentPropertyValue(int propertyId, out NativeVariant retVal);

    // Slot 8: GetCurrentPropertyValueEx
    [PreserveSig]
    int GetCurrentPropertyValueEx(int propertyId, int ignoreDefault, out NativeVariant retVal);

    // Slot 9: GetCachedPropertyValue
    [PreserveSig]
    int GetCachedPropertyValue(int propertyId, out NativeVariant retVal);

    // Slot 10: GetCachedPropertyValueEx
    [PreserveSig]
    int GetCachedPropertyValueEx(int propertyId, int ignoreDefault, out NativeVariant retVal);

    // Slot 11: GetCurrentPatternAs
    [PreserveSig]
    int GetCurrentPatternAs(int patternId, nint riid, out nint patternObject);

    // Slot 12: GetCachedPatternAs
    [PreserveSig]
    int GetCachedPatternAs(int patternId, nint riid, out nint patternObject);

    // Slot 13: GetCurrentPattern
    [PreserveSig]
    int GetCurrentPattern(int patternId, out nint patternObject);

    // Slot 14: GetCachedPattern
    [PreserveSig]
    int GetCachedPattern(int patternId, out nint patternObject);

    // Slot 15: GetCachedParent
    [PreserveSig]
    int GetCachedParent(out nint parent);

    // Slot 16: GetCachedChildren
    [PreserveSig]
    int GetCachedChildren(out nint children);

    // Slot 17: get_CurrentProcessId
    [PreserveSig]
    int get_CurrentProcessId(out int retVal);

    // Slot 18: get_CurrentControlType
    [PreserveSig]
    int get_CurrentControlType(out int retVal);

    // Slot 19: get_CurrentLocalizedControlType
    [PreserveSig]
    int get_CurrentLocalizedControlType([MarshalAs(UnmanagedType.BStr)] out string? retVal);

    // Slot 20: get_CurrentName
    [PreserveSig]
    int get_CurrentName([MarshalAs(UnmanagedType.BStr)] out string? retVal);

    // Slot 21: get_CurrentAcceleratorKey
    [PreserveSig]
    int get_CurrentAcceleratorKey([MarshalAs(UnmanagedType.BStr)] out string? retVal);

    // Slot 22: get_CurrentAccessKey
    [PreserveSig]
    int get_CurrentAccessKey([MarshalAs(UnmanagedType.BStr)] out string? retVal);

    // Slot 23: get_CurrentHasKeyboardFocus
    [PreserveSig]
    int get_CurrentHasKeyboardFocus(out int retVal);

    // Slot 24: get_CurrentIsKeyboardFocusable
    [PreserveSig]
    int get_CurrentIsKeyboardFocusable(out int retVal);

    // Slot 25: get_CurrentIsEnabled
    [PreserveSig]
    int get_CurrentIsEnabled(out int retVal);

    // Slot 26: get_CurrentAutomationId
    [PreserveSig]
    int get_CurrentAutomationId([MarshalAs(UnmanagedType.BStr)] out string? retVal);
}

/// <summary>
/// AOT-safe version of IUIAutomation.
/// All vtable slots from 0 to 18 are declared to preserve correct offsets.
/// Only ElementFromHandle (3) and CreateTrueCondition (18) are used.
/// </summary>
[GeneratedComInterface]
[Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUIAutomation
{
    // Slot 0: CompareElements
    [PreserveSig]
    int CompareElements(nint el1, nint el2, out int areSame);

    // Slot 1: CompareRuntimeIds — SAFEARRAY* params, use nint stubs
    [PreserveSig]
    int CompareRuntimeIds(nint runtimeId1, nint runtimeId2, out int areSame);

    // Slot 2: GetRootElement
    [PreserveSig]
    int GetRootElement(out nint root);

    // Slot 3: ElementFromHandle
    [PreserveSig]
    int ElementFromHandle(nint hwnd, out IUIAutomationElement? element);

    // Slot 4: ElementFromPoint — POINT is 8 bytes, use long as size-compatible stub
    [PreserveSig]
    int ElementFromPoint(long pt, out nint element);

    // Slot 5: GetFocusedElement
    [PreserveSig]
    int GetFocusedElement(out nint element);

    // Slot 6: GetRootElementBuildCache
    [PreserveSig]
    int GetRootElementBuildCache(nint cacheRequest, out nint root);

    // Slot 7: ElementFromHandleBuildCache
    [PreserveSig]
    int ElementFromHandleBuildCache(nint hwnd, nint cacheRequest, out nint element);

    // Slot 8: ElementFromPointBuildCache
    [PreserveSig]
    int ElementFromPointBuildCache(long pt, nint cacheRequest, out nint element);

    // Slot 9: GetFocusedElementBuildCache
    [PreserveSig]
    int GetFocusedElementBuildCache(nint cacheRequest, out nint element);

    // Slot 10: CreateTreeWalker
    [PreserveSig]
    int CreateTreeWalker(nint pCondition, out nint walker);

    // Slot 11: get_ControlViewWalker
    [PreserveSig]
    int get_ControlViewWalker(out nint walker);

    // Slot 12: get_ContentViewWalker
    [PreserveSig]
    int get_ContentViewWalker(out nint walker);

    // Slot 13: get_RawViewWalker
    [PreserveSig]
    int get_RawViewWalker(out nint walker);

    // Slot 14: get_RawViewCondition
    [PreserveSig]
    int get_RawViewCondition(out nint condition);

    // Slot 15: get_ControlViewCondition
    [PreserveSig]
    int get_ControlViewCondition(out nint condition);

    // Slot 16: get_ContentViewCondition
    [PreserveSig]
    int get_ContentViewCondition(out nint condition);

    // Slot 17: CreateCacheRequest
    [PreserveSig]
    int CreateCacheRequest(out nint cacheRequest);

    // Slot 18: CreateTrueCondition
    [PreserveSig]
    int CreateTrueCondition(out IUIAutomationCondition? condition);
}

#pragma warning restore SA1300 // Element should begin with upper-case letter

#pragma warning restore SA1649 // File name should match first type name

#pragma warning restore SA1402 // File may only contain a single type
