// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TopToolbar.Models;

namespace TopToolbar.Providers.Configuration;

public sealed class ExternalProviderConfig
{
    public ExternalProviderType Type { get; set; } = ExternalProviderType.None;

    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int? StartupTimeoutSeconds { get; set; }
}
