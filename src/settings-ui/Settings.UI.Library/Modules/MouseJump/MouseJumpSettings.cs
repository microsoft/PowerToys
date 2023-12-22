// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.V2_0;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump;

public partial class MouseJumpSettings : BasePTModuleSettings, ISettingsConfig
{
    public const string ModuleName = "MouseJump";

    public MouseJumpSettings()
    {
        this.Name = MouseJumpSettings.ModuleName;
        this.Version = "2.0";
        this.Properties = new MouseJumpProperties();
    }

    [JsonConstructor]
    public MouseJumpSettings(string version, MouseJumpProperties properties)
    {
        this.Name = MouseJumpSettings.ModuleName;
        this.Version = version;
        this.Properties = properties;
    }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MouseJumpProperties Properties
    {
        get;
        set;
    }

    public void Save(ISettingsUtils settingsUtils)
    {
        // Save settings to file
        ArgumentNullException.ThrowIfNull(settingsUtils);

        settingsUtils.SaveSettings(this.ToJsonString(), ModuleName);
    }

    public string GetModuleName()
    {
        return this.Name;
    }

    // This can be utilized in the future if the settings.json file is to be modified/deleted.
    public bool UpgradeSettingsConfiguration()
    {
        // upgrade v1.0 to v2.0
        if (this.Version == "1.0")
        {
            // move "thumbnail_size" to "preview.canvas_size"
#pragma warning disable 0618
            var canvasSize = new MouseJumpCanvasSize(
                width: this.Properties?.ThumbnailSize?.Width,
                height: this.Properties?.ThumbnailSize?.Height);

            if (this?.Properties?.ThumbnailSize is not null)
            {
                this.Properties.ThumbnailSize = null;
            }
#pragma warning restore 0618

            if (canvasSize.Width.HasValue || canvasSize.Height.HasValue)
            {
                this.Properties ??= new MouseJumpProperties();
                this.Properties.PreviewStyle ??= new MouseJumpPreviewStyle();
                this.Properties.PreviewStyle.CanvasSize ??= new MouseJumpCanvasSize();
                this.Properties.PreviewStyle.CanvasSize.Width = canvasSize.Width;
                this.Properties.PreviewStyle.CanvasSize.Height = canvasSize.Height;
            }

            this.Version = "2.0";
            return true;
        }

        return false;
    }
}
