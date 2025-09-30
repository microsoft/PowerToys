// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Overrides: only deviations from provider defaults need to be stored.
/// </summary>
public sealed class ProfileOverrides
{
    public Dictionary<string, bool> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, bool> Actions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
