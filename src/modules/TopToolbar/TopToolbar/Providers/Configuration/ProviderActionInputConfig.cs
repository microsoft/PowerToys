// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

public sealed class ProviderActionInputConfig
{
    public bool? HideBaseAction { get; set; }

    public List<ProviderActionEnumOption> Enum { get; set; } = new();

    public ProviderActionDynamicInputConfig Dynamic { get; set; }

    public JsonNode ArgsTemplate { get; set; }

    public string LabelTemplate { get; set; } = string.Empty;

    public string DescriptionTemplate { get; set; } = string.Empty;

    public string IconGlyph { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;

    public ToolbarIconType? IconType { get; set; }
}
