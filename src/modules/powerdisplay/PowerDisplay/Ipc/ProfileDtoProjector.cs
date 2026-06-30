// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using PowerDisplay.ViewModels;

namespace PowerDisplay.Ipc;

/// <summary>
/// Pure-function projector that converts the app's profile model and per-monitor apply-outcomes
/// into the flat Contracts DTOs used by the CLI IPC renderers for the <c>profiles</c> and
/// <c>apply-profile</c> commands.
/// </summary>
public static class ProfileDtoProjector
{
    // ─── profiles (list) ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds the result DTO for the <c>profiles</c> command.
    /// Projects each <see cref="PowerDisplayProfile"/> in <paramref name="profiles"/> to a
    /// <see cref="CliProfileInfo"/>:
    /// <list type="bullet">
    ///   <item><see cref="CliProfileInfo.Name"/> — profile name.</item>
    ///   <item><see cref="CliProfileInfo.MonitorCount"/> — <c>MonitorSettings.Count</c>.</item>
    ///   <item><see cref="CliProfileInfo.LastModified"/> — <see cref="PowerDisplayProfile.LastModified"/>
    ///         formatted as ISO 8601 round-trip ("o") with invariant culture.</item>
    /// </list>
    /// </summary>
    /// <param name="profiles">The loaded profiles collection (may be empty; must not be null).</param>
    public static CliProfileListResult BuildProfileListResult(PowerDisplayProfiles profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var infos = new List<CliProfileInfo>(profiles.Profiles.Count);
        foreach (var profile in profiles.Profiles)
        {
            if (profile is null)
            {
                continue;
            }

            infos.Add(new CliProfileInfo
            {
                Name = profile.Name ?? string.Empty,
                MonitorCount = profile.MonitorSettings?.Count ?? 0,

                // ISO 8601 round-trip ("o") with invariant culture.
                LastModified = profile.LastModified.ToString("o", CultureInfo.InvariantCulture),
            });
        }

        return new CliProfileListResult { Profiles = infos };
    }

    // ─── apply-profile ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the result DTO for the <c>apply-profile</c> command from pre-computed
    /// per-monitor apply outcomes, and computes the process exit code.
    /// <para>
    /// Exit-code precedence (highest wins):
    /// <c>HardwareFailure (5) &gt; InvalidDiscreteValue (3) &gt; OutOfRange (2) &gt; Ok (0)</c>.
    /// </para>
    /// <para>
    /// <c>unsupported</c> settings do not affect the exit code.
    /// </para>
    /// </summary>
    /// <param name="profileName">The profile that was applied.</param>
    /// <param name="outcomes">
    /// Per-monitor outcomes produced by
    /// <see cref="MainViewModel.ApplyProfileWithOutcomesAsync"/>.
    /// Must NOT be <c>null</c>; a <c>null</c> return from
    /// <c>ApplyProfileWithOutcomesAsync</c> means "profile not found" and must be
    /// handled by the IPC handler before calling this method — the
    /// handler returns a <c>CliErrorResult</c> with <c>CliErrorCodes.ArgumentError</c>
    /// / exit code 7.
    /// </param>
    /// <returns>
    /// The DTO to serialize to the IPC caller. The process exit code is carried on
    /// <see cref="CliApplyProfileResult.ExitCode"/> (worst-outcome precedence:
    /// HardwareFailure &gt; OutOfRange &gt; Ok), which the CLI dispatcher relays back.
    /// </returns>
    public static CliApplyProfileResult BuildApplyProfileResult(
        string profileName,
        IReadOnlyList<ProfileApplyOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var monitorOutcomes = new List<CliProfileMonitorOutcome>(outcomes.Count);
        var anyHardwareFailure = false;
        var anyInvalidDiscreteValue = false;
        var anyOutOfRange = false;

        foreach (var outcome in outcomes)
        {
            if (!outcome.Connected)
            {
                // Not connected — report with empty changes list and no monitor ref fields
                // beyond the ID (no number/name available since the monitor is offline).
                monitorOutcomes.Add(new CliProfileMonitorOutcome
                {
                    Monitor = new CliMonitorRef { Id = outcome.MonitorId },
                    Connected = false,
                    Changes = [],
                });
                continue;
            }

            // outcome.Changes are already Contracts DTOs; scan them for the worst-outcome flags
            // (HardwareFailure > InvalidDiscreteValue > OutOfRange; "unsupported" sets none — intentional).
            foreach (var change in outcome.Changes)
            {
                if (change.Status == CliProfileChange.StatusHardwareFailure)
                {
                    anyHardwareFailure = true;
                }
                else if (change.Status == CliProfileChange.StatusInvalidDiscreteValue)
                {
                    anyInvalidDiscreteValue = true;
                }
                else if (change.Status == CliProfileChange.StatusOutOfRange)
                {
                    anyOutOfRange = true;
                }
            }

            monitorOutcomes.Add(new CliProfileMonitorOutcome
            {
                // Carry the real number/name (the connected outcome has them) so the CLI renderer
                // prints "Monitor 2 (Dell ...)" instead of the placeholder "Monitor 0 ()".
                Monitor = new CliMonitorRef { Id = outcome.MonitorId, Number = outcome.Number, Name = outcome.Name },
                Connected = true,
                Changes = outcome.Changes,
            });
        }

        // Worst-outcome precedence: HardwareFailure > InvalidDiscreteValue > OutOfRange > Ok.
        var exitCode = anyHardwareFailure ? CliExitCodes.HardwareFailure
            : anyInvalidDiscreteValue ? CliExitCodes.InvalidDiscreteValue
            : anyOutOfRange ? CliExitCodes.OutOfRange
            : CliExitCodes.Ok;

        return new CliApplyProfileResult
        {
            ExitCode = exitCode,
            Profile = profileName ?? string.Empty,
            Monitors = monitorOutcomes,
        };
    }
}
