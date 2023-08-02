// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using ManagedCommon;
using Microsoft.Win32;

namespace ColorPicker.Mouse
{
    public static class CursorManager
    {
        private const string CursorsRegistryPath = @"HKEY_CURRENT_USER\Control Panel\Cursors\";
        private const string ArrowRegistryName = "Arrow";
        private const string IBeamRegistryName = "IBeam";
        private const string CrosshairRegistryName = "Crosshair";
        private const string HandRegistryName = "Hand";
        private const string ColorPickerCursorName = "Assets\\ColorPicker\\colorPicker.cur";

        private static string _originalArrowCursorPath;
        private static string _originalIBeamCursorPath;
        private static string _originalCrosshairCursorPath;
        private static string _originalHandCursorPath;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Interop object")]
        private const int SPI_SETCURSORS = 0x0057;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Interop object")]
        private const int SPIF_SENDCHANGE = 0x02;

        private static readonly IFileSystem _fileSystem = new FileSystem();

        public static void SetColorPickerCursor()
        {
            BackupOriginalCursors();

            var colorPickerCursorPath = _fileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ColorPickerCursorName);
            ChangeCursor(colorPickerCursorPath, ArrowRegistryName);
            ChangeCursor(colorPickerCursorPath, IBeamRegistryName);
        }

        public static void RestoreOriginalCursors()
        {
            ChangeCursor(_originalArrowCursorPath, ArrowRegistryName);
            ChangeCursor(_originalIBeamCursorPath, IBeamRegistryName);
        }

        private static void ChangeCursor(string curFile, string cursorRegistryName)
        {
            try
            {
                if (curFile != null)
                {
                    Registry.SetValue(CursorsRegistryPath, cursorRegistryName, curFile);
                    NativeMethods.SystemParametersInfo(SPI_SETCURSORS, 0, new IntPtr(0), SPIF_SENDCHANGE);
                }
                else
                {
                    Logger.LogInfo("Cursor file path was null");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to change cursor", ex);
            }
        }

        private static void BackupOriginalCursors()
        {
            if (string.IsNullOrEmpty(_originalArrowCursorPath))
            {
                _originalArrowCursorPath = (string)Registry.GetValue(CursorsRegistryPath, ArrowRegistryName, string.Empty);
            }

            if (string.IsNullOrEmpty(_originalIBeamCursorPath))
            {
                _originalIBeamCursorPath = (string)Registry.GetValue(CursorsRegistryPath, IBeamRegistryName, string.Empty);
            }

            if (string.IsNullOrEmpty(_originalCrosshairCursorPath))
            {
                _originalCrosshairCursorPath = (string)Registry.GetValue(CursorsRegistryPath, CrosshairRegistryName, string.Empty);
            }

            if (string.IsNullOrEmpty(_originalHandCursorPath))
            {
                _originalHandCursorPath = (string)Registry.GetValue(CursorsRegistryPath, HandRegistryName, string.Empty);
            }
        }
    }
}
