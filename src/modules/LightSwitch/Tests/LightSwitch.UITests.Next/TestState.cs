// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.LightSwitch.UITests;

internal sealed class TestState
{
    private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly string[] ThemeValueNames = ["SystemUsesLightTheme", "AppsUseLightTheme", "ColorPrevalence"];
    private readonly byte[]? settings;
    private readonly Dictionary<string, (object? Value, RegistryValueKind Kind)> themeValues = new();

    public TestState()
    {
        settings = File.Exists(SettingsPath) ? File.ReadAllBytes(SettingsPath) : null;
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath);
        foreach (string name in ThemeValueNames)
        {
            object? value = key?.GetValue(name);
            themeValues[name] = (value, value is null ? RegistryValueKind.DWord : key!.GetValueKind(name));
        }
    }

    public static string SettingsPath => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "LightSwitch", "settings.json");

    // An approximate local meridian is sufficient: +/-165 keeps the fixture away from midnight
    // even in UTC+14, and assertions use the calculated times rather than assumed 06:00/18:00.
    public static int LocalLongitude => Math.Clamp((int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalHours * 15, -165, 165);

    public static void SeedSettings()
    {
        var properties = new JsonObject
        {
            ["scheduleMode"] = Value("Off"),
            ["changeSystem"] = Value(true),
            ["changeApps"] = Value(true),
            ["lightTime"] = Value(360),
            ["darkTime"] = Value(1080),
            ["sunrise_offset"] = Value(0),
            ["sunset_offset"] = Value(0),
            ["latitude"] = Value("1"),
            ["longitude"] = Value(LocalLongitude.ToString(CultureInfo.InvariantCulture)),
            ["enableDarkModeProfile"] = Value(false),
            ["enableLightModeProfile"] = Value(false),
            ["toggle-theme-hotkey"] = new JsonObject
            {
                ["value"] = new JsonObject
                {
                    ["win"] = true,
                    ["ctrl"] = true,
                    ["alt"] = false,
                    ["shift"] = true,
                    ["code"] = 68,
                },
            },
        };
        var root = new JsonObject { ["name"] = "LightSwitch", ["version"] = "1.0", ["properties"] = properties };
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static ThemeState ReadTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath);
        return new ThemeState(ReadThemeValue(key, "SystemUsesLightTheme"), ReadThemeValue(key, "AppsUseLightTheme"));
    }

    public void Restore()
    {
        try
        {
            if (settings is null)
            {
                if (File.Exists(SettingsPath))
                {
                    File.Delete(SettingsPath);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllBytes(SettingsPath, settings);
            }
        }
        finally
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizePath);
            RestoreThemeValues(
                themeValues,
                (name, value, kind) =>
                {
                    if (value is null)
                    {
                        key.DeleteValue(name, throwOnMissingValue: false);
                    }
                    else
                    {
                        key.SetValue(name, value, kind);
                    }
                },
                name => key.GetValue(name),
                () =>
                {
                    // LightSwitch resets ColorPrevalence as well as both theme flags.
                    SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "ImmersiveColorSet", 0x0002, 1_000, out _);
                });
        }
    }

    internal static void RestoreThemeValues(
        IReadOnlyDictionary<string, (object? Value, RegistryValueKind Kind)> originals,
        Action<string, object?, RegistryValueKind> write,
        Func<string, object?> read,
        Action broadcast)
    {
        var failures = new List<string>();
        try
        {
            foreach (var (name, original) in originals)
            {
                try
                {
                    write(name, original.Value, original.Kind);
                    if (!Equals(original.Value, read(name)))
                    {
                        failures.Add($"{name} did not match its original value.");
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    failures.Add($"{name}: {ex.Message}");
                }
            }
        }
        finally
        {
            broadcast();
        }

        Assert.HasCount(0, failures, $"Could not completely restore the user's theme: {string.Join("; ", failures)}");
    }

    private static JsonObject Value<T>(T value) => new() { ["value"] = JsonValue.Create(value) };

    private static int ReadThemeValue(RegistryKey? key, string name)
    {
        object value = key?.GetValue(name) ?? 1;
        Assert.IsInstanceOfType<int>(value, $"{name} must be a DWORD.");
        int flag = (int)value;
        Assert.IsTrue(flag is 0 or 1, $"{name} must be 0 or 1, not {flag}.");
        return flag;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out UIntPtr result);
}

internal readonly record struct ThemeState(int System, int Apps);
