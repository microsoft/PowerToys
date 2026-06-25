// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

public sealed class CliListMonitor
{
    public int Number { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public bool SupportsBrightness { get; init; }

    public bool SupportsContrast { get; init; }

    public bool SupportsVolume { get; init; }

    public bool SupportsColorTemperature { get; init; }

    public bool SupportsInputSource { get; init; }

    public bool SupportsPowerState { get; init; }

    public bool SupportsOrientation { get; init; }
}
