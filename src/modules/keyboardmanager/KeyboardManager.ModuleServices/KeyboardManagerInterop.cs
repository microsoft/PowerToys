// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace KeyboardManager.ModuleServices;

internal static class KeyboardManagerInterop
{
    private const string DllName = "Powertoys.KeyboardManagerEditorLibraryWrapper.dll";
    private const CallingConvention Convention = CallingConvention.Cdecl;

    [DllImport(DllName, CallingConvention = Convention)]
    internal static extern IntPtr CreateMappingConfiguration();

    [DllImport(DllName, CallingConvention = Convention)]
    internal static extern void DestroyMappingConfiguration(IntPtr config);

    [DllImport(DllName, CallingConvention = Convention)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LoadMappingSettings(IntPtr config);

    [DllImport(DllName, CallingConvention = Convention)]
    internal static extern int GetSingleKeyRemapCount(IntPtr config);

    [DllImport(DllName, CallingConvention = Convention)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSingleKeyRemap(IntPtr config, int index, ref NativeSingleKeyMapping mapping);

    [DllImport(DllName, CallingConvention = Convention)]
    internal static extern int GetSingleKeyToTextRemapCount(IntPtr config);

    [DllImport(DllName, CallingConvention = Convention)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSingleKeyToTextRemap(IntPtr config, int index, ref NativeKeyboardTextMapping mapping);

    [DllImport(DllName, CallingConvention = Convention)]
    internal static extern int GetShortcutRemapCount(IntPtr config);

    [DllImport(DllName, CallingConvention = Convention)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetShortcutRemap(IntPtr config, int index, ref NativeShortcutMapping mapping);

    [DllImport(DllName, CallingConvention = Convention, CharSet = CharSet.Unicode)]
    internal static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

    [DllImport(DllName, CallingConvention = Convention)]
    internal static extern void FreeString(IntPtr str);

    internal static string GetStringAndFree(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        var result = Marshal.PtrToStringUni(handle) ?? string.Empty;
        FreeString(handle);
        return result;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeSingleKeyMapping
{
    public int OriginalKey;
    public IntPtr TargetKey;
    [MarshalAs(UnmanagedType.Bool)]
    public bool IsShortcut;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeKeyboardTextMapping
{
    public int OriginalKey;
    public IntPtr TargetText;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeShortcutMapping
{
    public IntPtr OriginalKeys;
    public IntPtr TargetKeys;
    public IntPtr TargetApp;
    public int OperationType;
    public IntPtr TargetText;
    public IntPtr ProgramPath;
    public IntPtr ProgramArgs;
    public IntPtr StartInDirectory;
    public int Elevation;
    public int IfRunningAction;
    public int Visibility;
    public IntPtr UriToOpen;
}
