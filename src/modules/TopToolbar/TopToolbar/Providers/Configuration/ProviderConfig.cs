// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

public sealed class ProviderConfig
{
    public string Id { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProviderLayoutConfig Layout { get; set; } = new();

    public List<ProviderActionConfig> Actions { get; set; } = new();
}

public sealed class ProviderLayoutConfig
{
    public ToolbarGroupLayoutStyle? Style { get; set; }

    public ToolbarGroupOverflowMode? Overflow { get; set; }

    public int? MaxInline { get; set; }

    public bool? ShowLabels { get; set; }
}

public sealed class ProviderActionConfig
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ToolbarIconType? IconType { get; set; }

    public string IconGlyph { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;

    public double? SortOrder { get; set; }

    public bool? IsEnabled { get; set; }

    public ToolbarAction Action { get; set; } = new();
}
