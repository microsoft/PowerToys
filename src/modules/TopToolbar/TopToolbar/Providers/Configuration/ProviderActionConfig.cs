// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

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

    public ProviderActionInputConfig Input { get; set; } = new();
}
