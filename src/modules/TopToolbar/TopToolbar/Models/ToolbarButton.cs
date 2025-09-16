// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Models;

public class ToolbarButton
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; }

    public ToolbarIconType IconType { get; set; } = ToolbarIconType.Glyph;

    public int IconTypeIndex
    {
        get => (int)IconType;
        set => IconType = (ToolbarIconType)value;
    }

    // For glyph icons (Segoe MDL2 Assets)
    public string IconGlyph { get; set; } = "\uE10F";

    // For custom images (png/jpg/ico) absolute path
    public string IconPath { get; set; }

    public bool IsEnabled { get; set; } = true;

    public ToolbarAction Action { get; set; } = new();
}
