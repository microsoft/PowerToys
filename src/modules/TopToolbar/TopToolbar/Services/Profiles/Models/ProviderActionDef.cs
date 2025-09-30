// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

public sealed class ProviderActionDef
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool? DefaultEnabled { get; set; }

    public string IconGlyph { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;
}
