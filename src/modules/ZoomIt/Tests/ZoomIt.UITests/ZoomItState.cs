// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.Win32;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal sealed class ZoomItState : IDisposable
{
    internal const string RegistryPath = @"Software\Sysinternals\ZoomIt";

    private readonly Dictionary<string, (object Value, RegistryValueKind Kind)> original;

    internal ZoomItState()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        original = key.GetValueNames().ToDictionary(
            name => name,
            name => (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!, key.GetValueKind(name)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in new Dictionary<string, int>
        {
            ["ToggleKey"] = 0x231,
            ["LiveZoomToggleKey"] = 0x234,
            ["DrawToggleKey"] = 0x232,
            ["BreakTimerKey"] = 0x233,
            ["RecordToggleKey"] = 0x235,
            ["SnipToggleKey"] = 0x236,
            ["DemoTypeToggleKey"] = 0x237,
            ["OptionsShown"] = 1,
            ["EulaAccepted"] = 1,
            ["ShowTrayIcon"] = 1,
            ["AnimnateZoom"] = 0,
            ["ZoominSliderLevel"] = 3,
            ["SmoothImage"] = 0,
            ["PenColor"] = 0x0000ff,
            ["PenWidth"] = 8,
            ["BreakPenColor"] = 0xffffff,
            ["BreakBackgroundColor"] = 1,
            ["BreakTimeout"] = 1,
            ["BreakOpacity"] = 100,
            ["BreakTimerPosition"] = 4,
            ["BreakPlaySoundFile"] = 0,
            ["BreakShowDesktop"] = 0,
            ["BreakShowBackgroundFile"] = 0,
            ["BreakBackgroundStretch"] = 1,
            ["BreakOnSecondary"] = 0,
            ["BreakLockWorkstation"] = 0,
            ["ShowExpiredTime"] = 1,
            ["DemoTypeUserDrivenMode"] = 0,
            ["DemoTypeSpeedSlider"] = 55,
            ["CaptureAudio"] = 0,
            ["CaptureSystemAudio"] = 0,
            ["WebcamOverlay"] = 0,
        })
        {
            key.SetValue(name, value, RegistryValueKind.DWord);
        }

        // LOGFONTW is 92 bytes: five LONGs, eight BYTEs, then 32 UTF-16 characters.
        var font = new byte[92];
        BitConverter.GetBytes(-40).CopyTo(font, 0);
        BitConverter.GetBytes(400).CopyTo(font, 16);
        font[23] = 1;
        Encoding.Unicode.GetBytes("Segoe UI").CopyTo(font, 28);
        key.SetValue("Font", font, RegistryValueKind.Binary);
    }

    internal static object? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue(name);
    }

    public void Dispose()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        foreach (var name in key.GetValueNames().Where(name => !original.ContainsKey(name)))
        {
            key.DeleteValue(name);
        }

        foreach (var (name, entry) in original)
        {
            key.SetValue(name, entry.Value, entry.Kind);
        }
    }
}
