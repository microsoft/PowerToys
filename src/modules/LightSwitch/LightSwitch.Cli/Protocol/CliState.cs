// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LightSwitch.Cli.Protocol;

internal sealed class CliState
{
    [JsonRequired]
    public string SystemTheme { get; init; } = string.Empty;

    [JsonRequired]
    public string AppsTheme { get; init; } = string.Empty;

    [JsonRequired]
    public bool ChangeSystem { get; init; }

    [JsonRequired]
    public bool ChangeApps { get; init; }

    [JsonRequired]
    public string ScheduleMode { get; init; } = string.Empty;

    [JsonRequired]
    public bool ManualOverride { get; init; }
}
