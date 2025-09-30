// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

public sealed class ProviderActionEnumOption
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double? SortOrder { get; set; }

    public JsonNode Args { get; set; }

    public string IconGlyph { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;

    public ToolbarIconType? IconType { get; set; }
}
