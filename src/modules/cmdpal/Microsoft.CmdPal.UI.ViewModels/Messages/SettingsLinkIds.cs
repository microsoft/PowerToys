// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Messages;

/// <summary>Stable public settings destination IDs; existing values must not be repurposed.</summary>
public static class SettingsLinkIds
{
    public static class General
    {
        public const string Page = "general";
        public const string Activation = "activation";
        public const string ActivationKey = "activation-key";
        public const string AutoGoHome = "auto-go-home";
        public const string KeepPreviousQuery = "keep-previous-query";
        public const string HighlightSearch = "highlight-search";
        public const string AppBehavior = "app-behavior";
        public const string SystemTrayIcon = "system-tray-icon";
        public const string AltF4 = "alt-f4";
        public const string Language = "language";
        public const string ExternalLinks = "external-links";
        public const string ExternalCommandLinks = "external-command-links";
        public const string AboutSection = "about-section";
        public const string About = "about";
        public const string SendFeedback = "send-feedback";
    }

    public static class Appearance
    {
        public const string Page = "appearance";
        public const string Visuals = "appearance-visuals";
        public const string Theme = "appearance-theme";
        public const string Backdrop = "appearance-backdrop";
        public const string Background = "appearance-background";
        public const string DisableAnimations = "disable-animations";
        public const string Layout = "appearance-layout";
        public const string CompactMode = "compact-mode";
        public const string LaunchPosition = "launch-position";
        public const string ToastPosition = "toast-position";
        public const string Interaction = "appearance-interaction";
        public const string SingleClickActivation = "single-click-activation";
        public const string ShowAppDetails = "show-app-details";
        public const string BackspaceGoesBack = "backspace-goes-back";
        public const string EscapeKeyBehavior = "escape-key-behavior";
    }

    public static class Extensions
    {
        public const string Page = "extensions";
        public const string Search = "extensions-search";
        public const string FallbackOrder = "fallback-order";
        public const string More = "extensions-more";
        public const string Providers = "extension-providers";

        public static class Extension
        {
            public const string Page = "extension";
            public const string Enabled = Page + "/enabled";
            public const string SearchWeight = Page + "/search-weight";
            public const string Commands = Page + "/commands";
            public const string Fallbacks = Page + "/fallbacks";
            public const string Settings = Page + "/settings";
        }
    }

    public static class Dock
    {
        public const string Page = "dock";
        public const string Enabled = "dock-enabled";
        public const string AppearanceSection = "dock-appearance";
        public const string Position = "dock-position";
        public const string Size = "dock-size";
        public const string Theme = "dock-theme";
        public const string Backdrop = "dock-backdrop";
        public const string Background = "dock-background";
        public const string BehaviorSection = "dock-behavior";
        public const string AlwaysOnTop = "dock-always-on-top";
        public const string AutoHide = "dock-auto-hide";
        public const string Monitors = "dock-monitors";
    }
}
