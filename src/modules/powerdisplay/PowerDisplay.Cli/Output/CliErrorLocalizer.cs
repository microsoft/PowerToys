// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Cli.Properties;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Maps an app-produced <see cref="CliError"/> to its localized (message, hint) pair, keyed by
/// <see cref="CliError.MessageId"/> and filled from the error's structured fields (Setting, Value).
/// The app sends only ids + data (no prose); this is the single place the CLI owns the human text.
/// <para>
/// Hints are generated here — the CLI already knows the valid setting lists, so the app need not
/// send them. An unrecognized or empty <see cref="CliError.MessageId"/> falls back to the app's
/// English <see cref="CliError.Message"/> / <see cref="CliError.Hint"/> (version-skew safety).
/// </para>
/// </summary>
internal static class CliErrorLocalizer
{
    private static readonly string AllSettings = string.Join(", ", CliSettingNames.All);

    private static readonly string DiscreteSettings = string.Join(
        ", ", CliSettingNames.ColorTemperature, CliSettingNames.InputSource, CliSettingNames.PowerState);

    private static readonly string ContinuousSettings = string.Join(
        ", ", CliSettingNames.Brightness, CliSettingNames.Contrast, CliSettingNames.Volume);

    /// <summary>Returns the localized message and optional hint for <paramref name="e"/>.</summary>
    public static (string Message, string? Hint) Localize(CliError e)
    {
        var value = e.Value ?? string.Empty;
        var setting = e.Setting ?? string.Empty;

        return e.MessageId switch
        {
            CliMessageIds.OutOfRange => (Resources.ErrMsg_OutOfRange(value, setting), null),
            CliMessageIds.InvalidInteger => (Resources.ErrMsg_InvalidInteger(value, setting), null),
            CliMessageIds.InvalidDiscrete => (Resources.ErrMsg_InvalidDiscrete(value, setting), Resources.Hint_UseHexVcp),
            CliMessageIds.DiscreteNotInSet => (Resources.ErrMsg_DiscreteNotInSet(value, setting), Resources.Hint_UseHexVcp),
            CliMessageIds.InvalidOrientation => (Resources.ErrMsg_InvalidOrientation(value), Resources.Hint_Orientation),
            CliMessageIds.Unsupported => (Resources.ErrMsg_Unsupported(setting), null),
            CliMessageIds.PowerBlankingConfirm => (Resources.ErrMsg_PowerBlankingConfirm, Resources.Hint_ConfirmPowerOff),
            CliMessageIds.HardwareFailure => (Resources.ErrMsg_HardwareFailure, null),
            CliMessageIds.UnknownSetting => (Resources.ErrMsg_UnknownSetting(value), Resources.Hint_ValidSettings(AllSettings)),
            CliMessageIds.NotDiscreteSetting => (Resources.ErrMsg_NotDiscreteSetting(value), Resources.Hint_ValidDiscreteSettings(DiscreteSettings)),
            CliMessageIds.SelectorMissing => (Resources.ErrMsg_SelectorMissing, Resources.Hint_SelectorMissing),
            CliMessageIds.MonitorNotFoundNumber => (Resources.ErrMsg_MonitorNotFoundNumber(value), Resources.Hint_RunList),
            CliMessageIds.MonitorNotFoundId => (Resources.ErrMsg_MonitorNotFoundId(value), Resources.Hint_RunList),
            CliMessageIds.UnknownSettingAdjust => (Resources.ErrMsg_UnknownSetting(value), Resources.Hint_AdjustSettings(ContinuousSettings)),
            CliMessageIds.NotAdjustable => (Resources.ErrMsg_NotAdjustable(setting), Resources.Hint_AdjustSettings(ContinuousSettings)),
            CliMessageIds.AdjustValueUnknown => (Resources.ErrMsg_AdjustValueUnknown(setting), Resources.Hint_UseSetForAbsolute),
            CliMessageIds.ProfileNotFound => (Resources.ErrMsg_ProfileNotFound(value), Resources.Hint_RunProfiles),
            CliMessageIds.UnknownCommand => (Resources.ErrMsg_UnknownCommand(value), null),
            CliMessageIds.InternalError => (Resources.ErrMsg_InternalError, null),

            // Unknown/empty id: fall back to whatever English prose the app supplied.
            _ => (e.Message, e.Hint),
        };
    }
}
