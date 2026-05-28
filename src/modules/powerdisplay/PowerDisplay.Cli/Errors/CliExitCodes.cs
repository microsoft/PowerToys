// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Errors;

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
}
