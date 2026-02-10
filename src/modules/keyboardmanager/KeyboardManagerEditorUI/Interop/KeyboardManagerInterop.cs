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

        // Configuration Management
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

        // Get Mapping Functions
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

        // Add Mapping Functions
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddSingleKeyRemap(IntPtr config, int originalKey, int targetKey);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddSingleKeyToTextRemap(IntPtr config, int originalKey, [MarshalAs(UnmanagedType.LPWStr)] string targetText);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddSingleKeyToShortcutRemap(IntPtr config, int originalKey, [MarshalAs(UnmanagedType.LPWStr)] string targetKeys);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddShortcutRemap(
            IntPtr config,
            [MarshalAs(UnmanagedType.LPWStr)] string originalKeys,
            [MarshalAs(UnmanagedType.LPWStr)] string targetKeys,
            [MarshalAs(UnmanagedType.LPWStr)] string targetApp,
            int operationType = 0,
            [MarshalAs(UnmanagedType.LPWStr)] string appPathOrUri = "",
            [MarshalAs(UnmanagedType.LPWStr)] string? args = null,
            [MarshalAs(UnmanagedType.LPWStr)] string? startDirectory = null,
            int elevation = 0,
            int ifRunningAction = 0,
            int visibility = 0);

        // Delete Mapping Functions
        [DllImport(DllName)]
        internal static extern bool DeleteSingleKeyRemap(IntPtr mappingConfiguration, int originalKey);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteSingleKeyToTextRemap(IntPtr config, int originalKey);

        [DllImport(DllName)]
        internal static extern bool DeleteShortcutRemap(IntPtr mappingConfiguration, [MarshalAs(UnmanagedType.LPWStr)] string originalKeys, [MarshalAs(UnmanagedType.LPWStr)] string targetApp);

        // Key Utility Functions
        [DllImport(DllName)]
        internal static extern int GetKeyCodeFromName([MarshalAs(UnmanagedType.LPWStr)] string keyName);

        [DllImport(DllName, CharSet = CharSet.Unicode)]
        internal static extern void GetKeyDisplayName(int keyCode, [Out] StringBuilder keyName, int maxLength);

        [DllImport(DllName)]
        internal static extern int GetKeyType(int keyCode);

        // Validation Functions
        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsShortcutIllegal([MarshalAs(UnmanagedType.LPWStr)] string shortcutKeys);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AreShortcutsEqual([MarshalAs(UnmanagedType.LPWStr)] string lShort, [MarshalAs(UnmanagedType.LPWStr)] string rShortcut);

        // String Management Functions
        [DllImport(DllName)]
        internal static extern void FreeString(IntPtr str);

        // Mouse Button Remap Functions
        [DllImport(DllName)]
        internal static extern int GetMouseButtonRemapCount(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMouseButtonRemap(IntPtr config, int index, ref MouseButtonMapping mapping);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddMouseButtonRemap(
            IntPtr config,
            int originalButton,
            [MarshalAs(UnmanagedType.LPWStr)] string targetKeys,
            [MarshalAs(UnmanagedType.LPWStr)] string targetApp,
            int targetType,
            [MarshalAs(UnmanagedType.LPWStr)] string targetText,
            [MarshalAs(UnmanagedType.LPWStr)] string programPath,
            [MarshalAs(UnmanagedType.LPWStr)] string programArgs,
            [MarshalAs(UnmanagedType.LPWStr)] string uriToOpen);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteMouseButtonRemap(IntPtr config, int originalButton, [MarshalAs(UnmanagedType.LPWStr)] string targetApp);

        // Key to Mouse Remap Functions
        [DllImport(DllName)]
        internal static extern int GetKeyToMouseRemapCount(IntPtr config);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetKeyToMouseRemap(IntPtr config, int index, ref KeyToMouseMappingInterop mapping);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddKeyToMouseRemap(IntPtr config, int originalKey, int targetMouseButton, [MarshalAs(UnmanagedType.LPWStr)] string targetApp);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteKeyToMouseRemap(IntPtr config, int originalKey, [MarshalAs(UnmanagedType.LPWStr)] string targetApp);

        // Mouse Button Utility Functions
        [DllImport(DllName, CharSet = CharSet.Unicode)]
        internal static extern void GetMouseButtonName(int buttonCode, [Out] StringBuilder buttonName, int maxLength);

        [DllImport(DllName)]
        internal static extern int GetMouseButtonFromName([MarshalAs(UnmanagedType.LPWStr)] string buttonName);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseButtonMapping
    {
        public int OriginalButton;      // MouseButton enum value (0-6)
        public IntPtr TargetKeys;       // Target key/shortcut string
        public IntPtr TargetApp;        // Empty for global, app name for app-specific
        public int TargetType;          // 0=Key, 1=Shortcut, 2=Text, 3=RunProgram, 4=OpenUri
        public IntPtr TargetText;       // For text mappings
        public IntPtr ProgramPath;      // For RunProgram
        public IntPtr ProgramArgs;      // For RunProgram
        public IntPtr UriToOpen;        // For OpenUri
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyToMouseMappingInterop
    {
        public int OriginalKey;         // Original key code (DWORD)
        public int TargetMouseButton;   // MouseButton enum value (0-6)
        public IntPtr TargetApp;        // Empty for global, app name for app-specific
    }

    /// <summary>
    /// Mouse button enum values matching the C++ MouseButton enum.
    /// </summary>
    public enum MouseButtonCode
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        X1 = 3,      // Back button
        X2 = 4,      // Forward button
        ScrollUp = 5,
        ScrollDown = 6,
    }
}
