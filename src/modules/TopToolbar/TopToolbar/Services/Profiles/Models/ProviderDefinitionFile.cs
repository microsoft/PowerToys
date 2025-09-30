// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

/// <summary>
/// Provider definition metadata + default enabled states. Loaded from providers/<providerId>.json.
/// </summary>
public sealed class ProviderDefinitionFile
{
    public int SchemaVersion { get; set; } = 1;

    public string ProviderId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<ProviderGroupDef> Groups { get; set; } = new();
}
