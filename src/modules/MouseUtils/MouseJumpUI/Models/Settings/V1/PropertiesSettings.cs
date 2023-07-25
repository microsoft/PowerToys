// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace MouseJumpUI.Models.Settings.V1;

internal sealed class PropertiesSettings
{
    public PropertiesSettings(
        ActivationShortcut? activationShortcut,
        CanvasSizeSettings thumbnailSize)
    {
        this.ActivationShortcut = activationShortcut;
        this.ThumbnailSize = thumbnailSize;
    }

    [JsonPropertyName("activation_shortcut")]
    public ActivationShortcut? ActivationShortcut
    {
        get;
    }

    [JsonPropertyName("thumbnail_size")]
    public CanvasSizeSettings ThumbnailSize
    {
        get;
    }
}
