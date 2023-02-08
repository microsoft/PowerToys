// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal class Utils
    {
        private static string _placementPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\PowerToys\settings-placement.json");

        public static WINDOWPLACEMENT DeserializePlacementOrDefault(IntPtr handle)
        {
            try
            {
                var json = File.ReadAllText(_placementPath);
                var placement = JsonSerializer.Deserialize<WINDOWPLACEMENT>(json);

                placement.Length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
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
                var json = JsonSerializer.Serialize(placement);
                File.WriteAllText(_placementPath, json);
            }
            catch (Exception)
            {
            }
        }

        public static void BecomeForegroundWindow(IntPtr hWnd)
        {
            NativeKeyboardHelper.INPUT input = new NativeKeyboardHelper.INPUT { type = NativeKeyboardHelper.INPUTTYPE.INPUT_MOUSE, data = { } };
            NativeKeyboardHelper.INPUT[] inputs = new NativeKeyboardHelper.INPUT[] { input };
            _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);
            NativeMethods.SetForegroundWindow(hWnd);
        }
    }
}
