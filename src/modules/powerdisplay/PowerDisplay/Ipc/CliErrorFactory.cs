// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using PowerDisplay.Contracts;

namespace PowerDisplay.Ipc;

/// <summary>
/// Shared factories for the <see cref="CliErrorResult"/> envelopes that the <c>set</c> and relative
/// <c>up</c>/<c>down</c> executors emit identically — only the owning command name differs. Keeps the
/// "unsupported" and "hardware failure" message/hint text in one place so the two executors cannot drift.
/// </summary>
internal static class CliErrorFactory
{
    // TODO(M4): app should set Code-only; CLI maps Code->localized message.

    /// <summary>UNSUPPORTED_FEATURE: the monitor does not support the named setting.</summary>
    public static CliErrorResult Unsupported(string command, CliMonitorRef monitorRef, string settingName, string unsupportedReason)
        => new()
        {
            Command = command,
            Monitor = monitorRef,
            Error = new CliError
            {
                Code = CliErrorCodes.UnsupportedFeature,
                Message = string.Format(
                    CultureInfo.InvariantCulture,
                    "monitor {0} ({1}): {2} is not supported",
                    monitorRef.Number,
                    monitorRef.Name,
                    settingName),
                Hint = string.Format(CultureInfo.InvariantCulture, "reason: {0}", unsupportedReason),
            },
        };

    /// <summary>
    /// HARDWARE_FAILURE: the DDC/CI or GDI write failed. Uses <paramref name="errorMessage"/> when
    /// present, otherwise <paramref name="fallback"/>.
    /// </summary>
    public static CliErrorResult HardwareFailure(string command, CliMonitorRef monitorRef, string? errorMessage, string fallback)
        => new()
        {
            Command = command,
            Monitor = monitorRef,
            Error = new CliError
            {
                Code = CliErrorCodes.HardwareFailure,
                Message = errorMessage ?? fallback,
            },
        };
}
