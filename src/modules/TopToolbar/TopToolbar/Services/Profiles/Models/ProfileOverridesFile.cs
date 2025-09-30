// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

/// <summary>
/// Per-profile overrides file (profiles/<id>.json) containing only divergence from defaults.
/// </summary>
public sealed class ProfileOverridesFile
{
    public int SchemaVersion { get; set; } = 1;

    public string ProfileId { get; set; } = string.Empty;

    public ProfileOverrides Overrides { get; set; } = new();
}
