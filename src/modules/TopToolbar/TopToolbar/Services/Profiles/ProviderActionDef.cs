// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Profiles;

public sealed class ProviderActionDef
{
    public string Name { get; set; } = string.Empty; // Raw action name; actionId derived as <groupId>-><Name>

    public string DisplayName { get; set; } = string.Empty;

    public bool? DefaultEnabled { get; set; }

    public string IconGlyph { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;
}
