// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// Stable, fine-grained identifiers for CLI error messages, shared by the app (which stamps one on
/// <see cref="CliError.MessageId"/>) and the CLI (which maps it to a localized template). Decoupled
/// from <see cref="CliErrorCodes"/>: several messages can share one coarse error code / exit code
/// (e.g. many are <see cref="CliErrorCodes.ArgumentError"/>). Never localized; never surfaced to users.
/// </summary>
public static class CliMessageIds
{
    // set / common
    public const string OutOfRange = "out-of-range";
    public const string InvalidInteger = "invalid-integer";
    public const string InvalidDiscrete = "invalid-discrete";
    public const string DiscreteNotInSet = "discrete-not-in-set";
    public const string InvalidOrientation = "invalid-orientation";
    public const string Unsupported = "unsupported";
    public const string PowerBlankingConfirm = "power-blanking-confirm";
    public const string HardwareFailure = "hardware-failure";

    // get / capabilities
    public const string UnknownSetting = "unknown-setting";
    public const string NotDiscreteSetting = "not-discrete-setting";

    // monitor resolution
    public const string SelectorMissing = "selector-missing";
    public const string MonitorNotFoundNumber = "monitor-not-found-number";
    public const string MonitorNotFoundId = "monitor-not-found-id";

    // up / down
    public const string UnknownSettingAdjust = "unknown-setting-adjust";
    public const string NotAdjustable = "not-adjustable";
    public const string AdjustValueUnknown = "adjust-value-unknown";

    // profiles / internal
    public const string ProfileNotFound = "profile-not-found";
    public const string UnknownCommand = "unknown-command";
    public const string InternalError = "internal-error";
}
