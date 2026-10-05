// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Contracts;

namespace PowerDisplay.Ipc;

/// <summary>
/// Shared factories for the <see cref="CliErrorResult"/> envelopes that the <c>set</c> and relative
/// <c>up</c>/<c>down</c> executors emit identically — only the owning command name differs. The app
/// stamps a <see cref="CliError.Code"/> + <see cref="CliError.MessageId"/> + structured fields; the
/// CLI localizes the human-readable text (see <c>CliErrorLocalizer</c>).
/// </summary>
internal static class CliErrorFactory
{
    /// <summary>UNSUPPORTED_FEATURE: the monitor does not support the named setting.</summary>
    public static CliErrorResult Unsupported(string command, CliMonitorRef monitorRef, string settingName, string unsupportedReason)
        => new()
        {
            Command = command,
            Monitor = monitorRef,
            Error = new CliError
            {
                Code = CliErrorCodes.UnsupportedFeature,
                MessageId = CliMessageIds.Unsupported,
                Setting = settingName,
                Detail = unsupportedReason,
            },
        };

    /// <summary>
    /// HARDWARE_FAILURE: the DDC/CI or GDI write failed. <paramref name="errorMessage"/> (when present)
    /// is carried verbatim as the technical diagnostic; the CLI supplies the localized message.
    /// </summary>
    public static CliErrorResult HardwareFailure(string command, CliMonitorRef monitorRef, string? errorMessage)
        => new()
        {
            Command = command,
            Monitor = monitorRef,
            Error = new CliError
            {
                Code = CliErrorCodes.HardwareFailure,
                MessageId = CliMessageIds.HardwareFailure,
                Detail = errorMessage,
            },
        };
}
