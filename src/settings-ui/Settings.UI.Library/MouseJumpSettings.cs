// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using MouseJump.Common.Helpers;
using MouseJump.Common.Models.Settings;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseJumpSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "MouseJump";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public MouseJumpProperties Properties { get; set; }

        public MouseJumpSettings()
        {
            Name = ModuleName;
            Properties = new MouseJumpProperties();
            Version = "1.1";
        }

        public void Save(SettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = _serializerOptions;

            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.MouseJump;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "MouseUtils_MouseJump_ActivationShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            /*
                v1.0 - initial version

                * DefaultActivationShortcut
                * activation_shortcut
                * thumbnail_size
                * name
                * version
            */
            var upgraded = false;

            if (this.Version == "1.0")
            {
                /*
                    v1.1 - added preview style settings

                     * preview_type
                     * background_color_1
                     * background_color_2
                     * border_thickness
                     * border_color
                     * border_3d_depth
                     * border_padding
                     * bezel_thickness
                     * bezel_color
                     * bezel_3d_depth
                     * screen_margin
                     * screen_color_1
                     * screen_color_2
                */
                this.Version = "1.1";

                // note - there's an issue where ITwoWayPipeMessageIPCManagedMethods.Send overwrites
                // the settings file version as "1.0" regardless of the actual version. as a result,
                // the UpgradeSettingsConfiguration can get triggered even if the config has already
                // been upgraded, so we need to do an additional check to make sure values haven't
                // already been upgraded before we overwrite them with default values.
                if (string.IsNullOrEmpty(this.Properties.PreviewType))
                {
                    // set default values for custom preview style
                    var previewStyle = StyleHelper.BezelledPreviewStyle;
                    this.Properties.PreviewType = PreviewType.Bezelled.ToString();
                    this.Properties.BackgroundColor1 = ConfigHelper.SerializeToConfigColorString(
                       ConfigHelper.ToUnnamedColor(previewStyle.CanvasStyle.BackgroundStyle.Color1));
                    this.Properties.BackgroundColor2 = ConfigHelper.SerializeToConfigColorString(
                        ConfigHelper.ToUnnamedColor(previewStyle.CanvasStyle.BackgroundStyle.Color2));
                    this.Properties.BorderThickness = (int)previewStyle.CanvasStyle.BorderStyle.Top;
                    this.Properties.BorderColor = ConfigHelper.SerializeToConfigColorString(
                        ConfigHelper.ToUnnamedColor(previewStyle.CanvasStyle.BorderStyle.Color));
                    this.Properties.Border3dDepth = (int)previewStyle.CanvasStyle.BorderStyle.Depth;
                    this.Properties.BorderPadding = (int)previewStyle.CanvasStyle.PaddingStyle.Top;
                    this.Properties.BezelThickness = (int)previewStyle.ScreenStyle.BorderStyle.Top;
                    this.Properties.BezelColor = ConfigHelper.SerializeToConfigColorString(
                        ConfigHelper.ToUnnamedColor(previewStyle.ScreenStyle.BorderStyle.Color));
                    this.Properties.Bezel3dDepth = (int)previewStyle.ScreenStyle.BorderStyle.Depth;
                    this.Properties.ScreenMargin = (int)previewStyle.ScreenStyle.MarginStyle.Top;
                    this.Properties.ScreenColor1 = ConfigHelper.SerializeToConfigColorString(
                        ConfigHelper.ToUnnamedColor(previewStyle.ScreenStyle.BackgroundStyle.Color1));
                    this.Properties.ScreenColor2 = ConfigHelper.SerializeToConfigColorString(
                        ConfigHelper.ToUnnamedColor(previewStyle.ScreenStyle.BackgroundStyle.Color2));
                }

                // we still need to flag the settings as "upgraded" so that the new version gets written
                // back to the config file, even if we didn't actually change and setting values
                upgraded = true;
            }

            return upgraded;
        }
    }
}
