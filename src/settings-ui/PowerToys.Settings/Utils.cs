// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using PowerToys.Settings.Helpers;

namespace PowerToys.Settings
{
    internal class Utils
    {
        private static string _placementPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\PowerToys\settings-placement.json");

        public static void FitToScreen(Window window)
        {
            if (SystemParameters.WorkArea.Width < window.Width)
            {
                window.Width = SystemParameters.WorkArea.Width;
            }

            if (SystemParameters.WorkArea.Height < window.Height)
            {
                window.Height = SystemParameters.WorkArea.Height;
            }
        }

        public static void CenterToScreen(Window window)
        {
            if (SystemParameters.WorkArea.Height <= window.Height)
            {
                window.Top = 0;
            }
            else
            {
                window.Top = (SystemParameters.WorkArea.Height - window.Height) / 2;
            }

            if (SystemParameters.WorkArea.Width <= window.Width)
            {
                window.Left = 0;
            }
            else
            {
                window.Left = (SystemParameters.WorkArea.Width - window.Width) / 2;
            }
        }

        public static void ShowHide(Window window)
        {
            // To limit the visual flickering, show the window with a size of 0,0
            // and don't show it in the taskbar
            var originalHeight = window.Height;
            var originalWidth = window.Width;
            var originalMinHeight = window.MinHeight;
            var originalMinWidth = window.MinWidth;

            window.MinHeight = 0;
            window.MinWidth = 0;
            window.Height = 0;
            window.Width = 0;
            window.ShowInTaskbar = false;

            window.Show();
            window.Hide();

            window.Height = originalHeight;
            window.Width = originalWidth;
            window.MinHeight = originalMinHeight;
            window.MinWidth = originalMinWidth;
            window.ShowInTaskbar = true;
        }

        public static WINDOWPLACEMENT DeserializePlacementOrDefault(IntPtr handle)
        {
            if (File.Exists(_placementPath))
            {
                try
                {
                    var json = File.ReadAllText(_placementPath);
                    var placement = JsonSerializer.Deserialize<WINDOWPLACEMENT>(json);

                    placement.Length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                    placement.Flags = 0;
                    placement.ShowCmd = placement.ShowCmd == NativeMethods.SW_SHOWMAXIMIZED ? NativeMethods.SW_SHOWMAXIMIZED : NativeMethods.SW_SHOWNORMAL;
                    return placement;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                }
            }

            _ = NativeMethods.GetWindowPlacement(handle, out var defaultPlacement);
            return defaultPlacement;
        }

        public static void SerializePlacement(IntPtr handle)
        {
            _ = NativeMethods.GetWindowPlacement(handle, out var placement);
            try
            {
                var json = JsonSerializer.Serialize(placement);
                File.WriteAllText(_placementPath, json);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
            }
        }
    }
}
