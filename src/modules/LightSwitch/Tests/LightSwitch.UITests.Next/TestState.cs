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

    // Equatorial coordinates in the local time zone avoid polar days and midnight solar boundaries.
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

        using var key = Registry.CurrentUser.CreateSubKey(PersonalizePath);
        foreach (var (name, original) in themeValues)
        {
            if (original.Value is null)
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(name, original.Value, original.Kind);
            }

            Assert.AreEqual(original.Value, key.GetValue(name), $"Could not restore the user's {name}.");
        }

        // LightSwitch resets ColorPrevalence as well as both theme flags. Restore all three and
        // notify existing windows instead of loading the product DLL into the test process.
        SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "ImmersiveColorSet", 0x0002, 1_000, out _);
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
