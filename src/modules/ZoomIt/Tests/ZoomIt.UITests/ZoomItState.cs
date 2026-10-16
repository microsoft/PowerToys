// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal sealed class ZoomItState : IDisposable
{
    internal const string RegistryPath = @"Software\Sysinternals\ZoomIt";

    private static readonly JsonSerializerOptions BackupJsonOptions = new() { WriteIndented = true };
    private static ZoomItState? pending;
    private readonly Dictionary<string, (object Value, RegistryValueKind Kind)> original;
    private readonly string registryPath;
    private readonly bool keyExisted;
    private bool disposed;

    private ZoomItState(string backupPath, string registryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);
        this.registryPath = registryPath;
        using var existing = Registry.CurrentUser.OpenSubKey(registryPath);
        keyExisted = existing is not null;
        original = existing?.GetValueNames().ToDictionary(
            name => name,
            name => (existing.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!, existing.GetValueKind(name)),
            StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase);

        using (var backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            var snapshot = new
            {
                RegistryPath = registryPath,
                KeyExisted = keyExisted,
                Values = original.Select(entry => new { Name = entry.Key, Kind = entry.Value.Kind.ToString(), entry.Value.Value }),
            };
            JsonSerializer.Serialize(backup, snapshot, BackupJsonOptions);
            backup.Flush(flushToDisk: true);
        }

        try
        {
            ApplyBaseline();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal static ZoomItState CaptureForTest(string backupPath, string registryPath = RegistryPath)
    {
        // A failed TestInitialize may have skipped the previous instance's cleanup.
        RestorePending();
        var state = new ZoomItState(backupPath, registryPath);
        pending = state;
        return state;
    }

    internal static void RestorePending()
    {
        pending?.Dispose();
        pending = null;
    }

    private void ApplyBaseline()
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
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
        font[23] = 1; // lfCharSet = DEFAULT_CHARSET.
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
        if (disposed)
        {
            return;
        }

        if (!keyExisted)
        {
            Registry.CurrentUser.DeleteSubKey(registryPath, throwOnMissingSubKey: false);
            disposed = true;
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        foreach (var name in key.GetValueNames().Where(name => !original.ContainsKey(name)))
        {
            key.DeleteValue(name);
        }

        foreach (var (name, entry) in original)
        {
            key.SetValue(name, entry.Value, entry.Kind);
        }

        disposed = true;
    }
}
