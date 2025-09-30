// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Providers.Configuration;

public sealed class ProviderConfig
{
    public string Id { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProviderLayoutConfig Layout { get; set; } = new();

    public List<ProviderActionConfig> Actions { get; set; } = new();

    public ExternalProviderConfig External { get; set; } = new();
}

public enum ExternalProviderType
{
    None = 0,
    Mcp = 1,
}
