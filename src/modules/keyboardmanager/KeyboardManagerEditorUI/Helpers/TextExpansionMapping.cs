// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;

namespace KeyboardManagerEditorUI.Helpers
{
    public sealed class TextExpansionMapping : IToggleableShortcut
    {
        public string Id { get; set; } = string.Empty;

        public string SourceText { get; set; } = string.Empty;

        public List<int> ActivationKeys { get; set; } = new();

        public List<string> ActivationKeyNames { get; set; } = new();

        public string ReplacementText { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public List<string> Shortcut
        {
            get => ActivationKeyNames;
            set => ActivationKeyNames = value;
        }

        public bool IsActive
        {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        public string AppName { get; set; } = string.Empty;

        public bool IsAllApps { get; set; } = true;

        public IReadOnlyList<int> TriggerKeyCodes
        {
            get => ActivationKeys;
            set => ActivationKeys = value is List<int> keys ? keys : new List<int>(value);
        }

        public string SearchableText { get; set; } = string.Empty;

        public string EnabledAutomationName => FormatAutomationName("TextExpansionEnabledToggle_AutomationName");

        public string MenuAutomationName => FormatAutomationName("TextExpansionMenuButton_AutomationName");

        private string FormatAutomationName(string resourceKey)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                ResourceHelper.GetString(resourceKey),
                SourceText,
                string.Join(" + ", ActivationKeyNames),
                ReplacementText);
        }
    }
}
