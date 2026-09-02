// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

public static class CliExitCodes
{
    public const int Ok = 0;
    public const int MonitorNotFound = 1;
    public const int OutOfRange = 2;
    public const int InvalidDiscreteValue = 3;
    public const int UnsupportedFeature = 4;
    public const int HardwareFailure = 5;
    public const int SelectorMissing = 6;
    public const int ArgumentError = 7;
    public const int Timeout = 8;
    public const int InternalError = 9;

    /// <summary>The PowerDisplay app/provider is not running or could not be reached.</summary>
    public const int ProviderUnavailable = 10;

    /// <summary>
    /// Maps a <see cref="CliErrorCodes"/> value to its corresponding process exit code. The two
    /// sets are a 1:1 name mirror; this is the single source of that pairing so an error's code and
    /// its exit code can never disagree. An unrecognized code maps to <see cref="InternalError"/>.
    /// </summary>
    public static int ForErrorCode(string errorCode) => errorCode switch
    {
        CliErrorCodes.MonitorNotFound => MonitorNotFound,
        CliErrorCodes.OutOfRange => OutOfRange,
        CliErrorCodes.InvalidDiscreteValue => InvalidDiscreteValue,
        CliErrorCodes.UnsupportedFeature => UnsupportedFeature,
        CliErrorCodes.HardwareFailure => HardwareFailure,
        CliErrorCodes.SelectorMissing => SelectorMissing,
        CliErrorCodes.ArgumentError => ArgumentError,
        CliErrorCodes.Timeout => Timeout,
        CliErrorCodes.InternalError => InternalError,
        CliErrorCodes.ProviderUnavailable => ProviderUnavailable,
        _ => InternalError,
    };
}
