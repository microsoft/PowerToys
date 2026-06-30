// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// The result of applying one setting from a profile to one monitor.
/// </summary>
public sealed class CliProfileChange
{
    public const string StatusApplied = "applied";
    public const string StatusUnsupported = "unsupported";
    public const string StatusOutOfRange = "out-of-range";

    // A discrete value (color-temperature) that parses as a byte but is not in the monitor's
    // advertised supported set. Distinct from out-of-range (raw byte bounds) so apply-profile maps
    // it to the same exit code (3 / INVALID_DISCRETE_VALUE) the `set` command uses for that case.
    public const string StatusInvalidDiscreteValue = "invalid-discrete-value";

    public const string StatusHardwareFailure = "hardware-failure";

    public string Setting { get; init; } = string.Empty;

    /// <summary>The raw value the profile requested (percentage for continuous, VCP value for color-temperature).</summary>
    public int Value { get; init; }

    /// <summary>Human-readable applied value (e.g. "50%", "6500K (0x05)"); present only when <see cref="Status"/> is "applied".</summary>
    public string? Display { get; init; }

    /// <summary>One of applied / unsupported / out-of-range / invalid-discrete-value / hardware-failure.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Hardware error message; present only when <see cref="Status"/> is "hardware-failure".</summary>
    public string? Error { get; init; }
}
