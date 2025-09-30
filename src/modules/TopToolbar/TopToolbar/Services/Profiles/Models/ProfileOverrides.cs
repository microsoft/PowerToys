// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace TopToolbar.Services.Profiles.Models;

public sealed class ProfileOverrides
{
    public Dictionary<string, bool> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, bool> Actions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
