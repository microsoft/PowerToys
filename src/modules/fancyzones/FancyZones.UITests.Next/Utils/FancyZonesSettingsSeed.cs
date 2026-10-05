// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;

namespace FancyZones.UITests.Utils;

/// <summary>
/// Seeds <c>%LocalAppData%\Microsoft\PowerToys\FancyZones\settings.json</c> before the runner starts.
/// </summary>
/// <remarks>
/// The legacy suite configured every zone-behaviour option by scrolling the Settings page a fixed
/// number of notches and clicking checkboxes by their localized labels. That choreography is the
/// single largest source of flakiness in those tests (and breaks outright in a non-English UI), so the
/// port seeds the same options straight into the module's settings file and restarts the scope. The
/// UI path is still exercised where it is the behaviour under test (the module enable toggle and the
/// layout editor).
/// </remarks>
public sealed class FancyZonesSettingsSeed
{
    /// <summary>Module key in the global settings.json <c>enabled</c> section, and its settings folder name.</summary>
    public const string ModuleName = "FancyZones";

    private const string DefaultSettings = """
        {
          "name": "FancyZones",
          "properties": {},
          "version": "1.0"
        }
        """;

    private readonly Dictionary<string, JsonNode?> values = new(StringComparer.Ordinal);

    public FancyZonesSettingsSeed Set(string property, bool value)
    {
        values[property] = new JsonObject { ["value"] = value };
        return this;
    }

    public FancyZonesSettingsSeed Set(string property, int value)
    {
        values[property] = new JsonObject { ["value"] = value };
        return this;
    }

    public FancyZonesSettingsSeed Set(string property, string value)
    {
        values[property] = new JsonObject { ["value"] = value };
        return this;
    }

    /// <summary>Write the accumulated properties into the module's settings file.</summary>
    public void Apply()
    {
        SettingsConfigHelper.UpdateModuleSettings(ModuleName, DefaultSettings, settings =>
        {
            if (settings["properties"] is not JsonObject properties)
            {
                properties = new JsonObject();
                settings["properties"] = properties;
            }

            foreach (var (property, value) in values)
            {
                properties[property] = value?.DeepClone();
            }
        });
    }

    /// <summary>Value of <paramref name="property"/> as the module's settings file currently holds it.</summary>
    public static string ReadCurrent(string property)
    {
        var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, ModuleName, "settings.json");
        if (!File.Exists(path))
        {
            return "<no settings file>";
        }

        var properties = JsonNode.Parse(File.ReadAllText(path))?["properties"];
        return properties?[property]?["value"]?.ToJsonString() ?? "<unset>";
    }

    /// <summary>
    /// Write the properties and give the running module time to pick them up.
    /// </summary>
    /// <remarks>
    /// FancyZones watches its settings file and reloads on change (<c>FancyZonesSettings::LoadSettings</c>),
    /// so a test does not need to relaunch PowerToys to change zone behaviour — which matters, because
    /// a kill-and-relaunch of the runner costs well over a minute on a loaded machine and would double
    /// the suite's runtime. There is no observable ready signal for the reload, so this is a bounded
    /// settle; every caller then performs a much longer editor round-trip before the behaviour is
    /// exercised.
    /// </remarks>
    public void ApplyAndLetModuleReload(int settleMs = 2500)
    {
        Apply();
        Thread.Sleep(settleMs);
    }

    /// <summary>Property names from <c>FZConfigProperties</c> (the settings.json contract).</summary>
    public static class Setting
    {
        public const string ShiftDrag = "fancyzones_shiftDrag";
        public const string MouseSwitch = "fancyzones_mouseSwitch";
        public const string MakeDraggedWindowTransparent = "fancyzones_makeDraggedWindowTransparent";
        public const string ShowZoneNumber = "fancyzones_showZoneNumber";
        public const string SystemTheme = "fancyzones_systemTheme";
        public const string HighlightOpacity = "fancyzones_highlight_opacity";
        public const string ZoneHighlightColor = "fancyzones_zoneHighlightColor";
        public const string ZoneInActiveColor = "fancyzones_zoneColor";
        public const string ZoneBorderColor = "fancyzones_zoneBorderColor";
        public const string WindowSwitching = "fancyzones_windowSwitching";
        public const string QuickLayoutSwitch = "fancyzones_quickLayoutSwitch";
        public const string FlashZonesOnQuickSwitch = "fancyzones_flashZonesOnQuickSwitch";
        public const string ZoneSetChangeMoveWindows = "fancyzones_zoneSetChange_moveWindows";
        public const string AppLastZoneMoveWindows = "fancyzones_appLastZone_moveWindows";
        public const string ExcludedApps = "fancyzones_excluded_apps";
        public const string OverrideSnapHotkeys = "fancyzones_overrideSnapHotkeys";
        public const string MoveWindowsBasedOnPosition = "fancyzones_moveWindowsBasedOnPosition";
        public const string MoveWindowAcrossMonitors = "fancyzones_moveWindowAcrossMonitors";
        public const string AllowChildWindowSnap = "fancyzones_allowChildWindowSnap";
        public const string AllowPopupWindowSnap = "fancyzones_allowPopupWindowSnap";
    }
}
