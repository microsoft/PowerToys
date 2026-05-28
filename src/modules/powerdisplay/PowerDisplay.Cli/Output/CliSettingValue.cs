// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Output;

public sealed class CliSettingValue
{
    public string Setting { get; init; } = string.Empty;

    public int Raw { get; init; }

    public string Display { get; init; } = string.Empty;

    public bool Supported { get; init; }
}
