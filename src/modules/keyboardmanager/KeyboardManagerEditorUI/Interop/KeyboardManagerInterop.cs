// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Interop
{
    public static class KeyboardManagerInterop
    {
        private const string DllName = "Powertoys.KeyboardManagerEditorLibraryWrapper.dll";

        [DllImport(DllName)]
        internal static extern IntPtr CreateMappingConfiguration();

        [DllImport(DllName)]
        internal static extern void DestroyMappingConfiguration(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LoadMappingSettings(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SaveMappingSettings(IntPtr config);

        [DllImport(DllName)]
        internal static extern int GetSingleKeyRemapCount(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSingleKeyRemap(IntPtr config, int index, ref SingleKeyMapping mapping);

        [DllImport(DllName)]
        internal static extern int GetSingleKeyToTextRemapCount(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSingleKeyToTextRemap(IntPtr config, int index, ref KeyboardTextMapping mapping);

        [DllImport(DllName)]
        internal static extern int GetShortcutRemapCount(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetShortcutRemap(IntPtr config, int index, ref ShortcutMapping mapping);

        [DllImport(DllName)]
        internal static extern int GetShortcutRemapCountByType(IntPtr config, int operationType);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetShortcutRemapByType(IntPtr config, int operationType, int index, ref ShortcutMapping mapping);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddSingleKeyRemap(IntPtr config, int originalKey, int targetKey);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddSingleKeyToShortcutRemap(IntPtr config, int originalKey, [MarshalAs(UnmanagedType.LPWStr)] string targetKeys);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddShortcutRemap(
            IntPtr config,
            [MarshalAs(UnmanagedType.LPWStr)] string originalKeys,
            [MarshalAs(UnmanagedType.LPWStr)] string targetKeys,
            [MarshalAs(UnmanagedType.LPWStr)] string targetApp);

        [DllImport(DllName)]
        internal static extern int GetKeyCodeFromName([MarshalAs(UnmanagedType.LPWStr)] string keyName);

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        internal static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        [DllImport(DllName)]
        internal static extern void FreeString(IntPtr str);

        [DllImport(DllName)]
        internal static extern bool DeleteSingleKeyRemap(IntPtr mappingConfiguration, int originalKey);

        [DllImport(DllName)]
        internal static extern bool DeleteShortcutRemap(IntPtr mappingConfiguration, [MarshalAs(UnmanagedType.LPWStr)] string originalKeys, [MarshalAs(UnmanagedType.LPWStr)] string targetApp);

        public static string GetStringAndFree(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            string? result = Marshal.PtrToStringUni(handle);
            FreeString(handle);
            return result ?? string.Empty;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SingleKeyMapping
    {
        public int OriginalKey;
        public IntPtr TargetKey;
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsShortcut;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardTextMapping
    {
        public int OriginalKey;
        public IntPtr TargetText;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShortcutMapping
    {
        public IntPtr OriginalKeys;
        public IntPtr TargetKeys;
        public IntPtr TargetApp;
        public int OperationType;
        public IntPtr TargetText;
        public IntPtr ProgramPath;
        public IntPtr ProgramArgs;
        public IntPtr UriToOpen;
    }
}
