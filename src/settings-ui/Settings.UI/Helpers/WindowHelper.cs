// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal sealed class WindowHelper
    {
        private static string _placementPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\PowerToys\settings-placement.json");

        public static WINDOWPLACEMENT DeserializePlacementOrDefault(IntPtr handle)
        {
            try
            {
                var json = File.ReadAllText(_placementPath);
                var placement = JsonSerializer.Deserialize<WINDOWPLACEMENT>(json, SourceGenerationContextContext.Default.WINDOWPLACEMENT);

                placement.Length = Marshal.SizeOf<WINDOWPLACEMENT>();
                placement.Flags = 0;
                placement.ShowCmd = (placement.ShowCmd == NativeMethods.SW_SHOWMAXIMIZED) ? NativeMethods.SW_SHOWMAXIMIZED : NativeMethods.SW_SHOWNORMAL;
                return placement;
            }
            catch (Exception)
            {
            }

            _ = NativeMethods.GetWindowPlacement(handle, out var defaultPlacement);
            return defaultPlacement;
        }

        public static void SerializePlacement(IntPtr handle)
        {
            _ = NativeMethods.GetWindowPlacement(handle, out var placement);
            try
            {
                var json = JsonSerializer.Serialize(placement, SourceGenerationContextContext.Default.WINDOWPLACEMENT);
                File.WriteAllText(_placementPath, json);
            }
            catch (Exception)
            {
            }
        }

        public static void SetTheme(Window window, ElementTheme theme)
        {
            if (window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
            }
        }
    }
}
