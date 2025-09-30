// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Registry root file (profiles.json): tracks known profiles and the active one.
/// </summary>
public sealed class ProfilesRegistry
{
    public int SchemaVersion { get; set; } = 1;

    public string ActiveProfileId { get; set; } = "default";

    public List<ProfileMeta> Profiles { get; set; } = new();
}
