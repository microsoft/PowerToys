// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Services.Profiles;

/// <summary>
/// Lightweight profile descriptor.
/// </summary>
public sealed class ProfileMeta
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
