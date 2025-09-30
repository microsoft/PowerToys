// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Provider definition file: describes display + default enabled states (providers/<providerId>.json).
/// </summary>
public sealed class ProviderDefinitionFile
{
    public int SchemaVersion { get; set; } = 1;

    public string ProviderId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<ProviderGroupDef> Groups { get; set; } = new();
}
