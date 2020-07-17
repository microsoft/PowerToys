using ColorPicker.Helpers;
using Microsoft.Win32;
using System;
using System.IO;

namespace ColorPicker.Mouse
{
    public static class CursorManager
    {
        private const string CursorsRegistryPath = @"HKEY_CURRENT_USER\Control Panel\Cursors\";
        private const string ArrowRegistryName = "Arrow";
        private const string IBeamRegistryName = "IBeam";
        private const string CrosshairRegistryName = "Crosshair";
        private const string HandRegistryName = "Hand";
        private const string ColorPickerCursorName = "Resources\\colorPicker.cur";

        private static string _originalArrowCursorPath;
        private static string _originalIBeamCursorPath;
        private static string _originalCrosshairCursorPath;
        private static string _originalHandCursorPath;

        public static void SetColorPickerCursor()
        {
            BackupOriginalCursors();

            var colorPickerCursorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ColorPickerCursorName);
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
                if(curFile != null)
                {
                    Registry.SetValue(CursorsRegistryPath, cursorRegistryName, curFile);
                    Win32Apis.SystemParametersInfo(SPI_SETCURSORS, 0, new IntPtr(0), SPIF_SENDCHANGE);
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

        const int SPI_SETCURSORS = 0x0057;
        const int SPIF_SENDCHANGE = 0x02;
    }
}
