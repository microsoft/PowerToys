// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

/// <summary>
/// Root registry file describing all known profiles and the active profile id.
/// </summary>
public sealed class ProfilesRegistry
{
    public int SchemaVersion { get; set; } = 1;

    public string ActiveProfileId { get; set; } = "default";

    public List<ProfileMeta> Profiles { get; set; } = new();
}
