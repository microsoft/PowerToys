// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// A sample page that displays the current Host Settings values.
/// This demonstrates how extensions can access and display the Command Palette's global settings.
/// </summary>
internal sealed partial class HostSettingsPage : ListPage
{
    public HostSettingsPage()
    {
        Icon = new IconInfo("\uE713"); // Settings icon
        Name = "Host Settings";
        Title = "Current Host Settings";

        // Subscribe to settings changes to refresh the page when settings are updated
        HostSettingsManager.SettingsChanged += OnSettingsChanged;
        ExtensionHost.LogMessage($"[HostSettingsPage] Constructor called, subscribed to SettingsChanged");
    }

    private void OnSettingsChanged()
    {
        ExtensionHost.LogMessage($"[HostSettingsPage] OnSettingsChanged called, invoking RaiseItemsChanged");

        // Notify the UI to refresh the items list
        RaiseItemsChanged();
        ExtensionHost.LogMessage($"[HostSettingsPage] RaiseItemsChanged completed");
    }

    public override IListItem[] GetItems()
    {
        ExtensionHost.LogMessage($"[HostSettingsPage] GetItems called");
        var settings = HostSettingsManager.Current;

        if (settings == null)
        {
            return [
                new ListItem(new NoOpCommand())
                {
                    Title = "Host Settings not available",
                    Subtitle = "Settings have not been received from the host yet",
                    Icon = new IconInfo("\uE7BA"), // Warning icon
                },
            ];
        }

        return [
            CreateSettingItem("Hotkey", settings.Hotkey, "\uE765"), // Keyboard icon
            CreateSettingItem("Show App Details", settings.ShowAppDetails, "\uE946"), // View icon
            CreateSettingItem("Hotkey Goes Home", settings.HotkeyGoesHome, "\uE80F"), // Home icon
            CreateSettingItem("Backspace Goes Back", settings.BackspaceGoesBack, "\uE72B"), // Back icon
            CreateSettingItem("Single Click Activates", settings.SingleClickActivates, "\uE8B0"), // Mouse icon
            CreateSettingItem("Highlight Search On Activate", settings.HighlightSearchOnActivate, "\uE8D6"), // Highlight icon
            CreateSettingItem("Show System Tray Icon", settings.ShowSystemTrayIcon, "\uE8A5"), // System icon
            CreateSettingItem("Ignore Shortcut When Fullscreen", settings.IgnoreShortcutWhenFullscreen, "\uE740"), // Fullscreen icon
            CreateSettingItem("Disable Animations", settings.DisableAnimations, "\uE916"), // Play icon
            CreateSettingItem("Summon On", settings.SummonOn.ToString(), "\uE7C4"), // Position icon
        ];
    }

    private static ListItem CreateSettingItem(string name, object value, string iconGlyph)
    {
        var displayValue = value switch
        {
            bool b => b ? "Enabled" : "Disabled",
            string s when string.IsNullOrEmpty(s) => "(not set)",
            _ => value?.ToString() ?? "null",
        };

        return new ListItem(new NoOpCommand())
        {
            Title = name,
            Subtitle = displayValue,
            Icon = new IconInfo(iconGlyph),
        };
    }
}
