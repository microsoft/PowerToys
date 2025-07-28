// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShortcutGuideProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultOpenShortcutGuide => new HotkeySettings(true, false, false, true, 0xBF);

        public ShortcutGuideProperties()
        {
            Theme = new StringProperty("system");
            DisabledApps = new StringProperty();
            OpenShortcutGuide = DefaultOpenShortcutGuide;
            FirstRun = new BoolProperty(true);
        }

        [JsonPropertyName("open_shortcutguide")]
        public HotkeySettings OpenShortcutGuide { get; set; }

        [JsonPropertyName("theme")]
        public StringProperty Theme { get; set; }

        [JsonPropertyName("disabled_apps")]
        public StringProperty DisabledApps { get; set; }

        [JsonPropertyName("first_run")]
        public BoolProperty FirstRun { get; set; }
    }
}
