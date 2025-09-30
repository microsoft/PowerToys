// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

/// <summary>
/// In-memory effective model used for rendering (not serialized to disk).
/// </summary>
public sealed class EffectiveProviderModel
{
    public string ProviderId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<EffectiveGroup> Groups { get; set; } = new();
}
