// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

public sealed class ProviderActionDynamicInputConfig
{
    public string SourceTool { get; set; } = string.Empty;

    public string ItemsPath { get; set; } = "content[0].data";

    public int? CacheSeconds { get; set; }

    public int? MaxItems { get; set; }

    public string LabelField { get; set; } = string.Empty;

    public string LabelTemplate { get; set; } = string.Empty;

    public string DescriptionField { get; set; } = string.Empty;

    public string DescriptionTemplate { get; set; } = string.Empty;

    public string ValueField { get; set; } = string.Empty;

    public JsonNode ArgsTemplate { get; set; }

    public string IconGlyphField { get; set; } = string.Empty;

    public string IconPathField { get; set; } = string.Empty;

    public ToolbarIconType? IconType { get; set; }

    public string SortField { get; set; } = string.Empty;
}
