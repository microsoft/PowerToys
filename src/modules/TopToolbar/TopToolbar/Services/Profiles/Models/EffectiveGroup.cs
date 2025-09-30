// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

public sealed class EffectiveGroup
{
    public string GroupId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public List<EffectiveAction> Actions { get; set; } = new();
}
