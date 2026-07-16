// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// Stable error codes emitted as <c>error.code</c> in JSON output.
/// </summary>
public static class CliErrorCodes
{
    public const string MonitorNotFound = "MONITOR_NOT_FOUND";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string InvalidDiscreteValue = "INVALID_DISCRETE_VALUE";
    public const string UnsupportedFeature = "UNSUPPORTED_FEATURE";
    public const string HardwareFailure = "HARDWARE_FAILURE";
    public const string SelectorMissing = "SELECTOR_MISSING";
    public const string ArgumentError = "ARGUMENT_ERROR";
    public const string Timeout = "TIMEOUT";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
}
