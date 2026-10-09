// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Contracts;

namespace PowerDisplay.Ipc;

/// <summary>
/// Command-name → handler registry for the CLI IPC dispatcher. Adding a command is a single row here
/// plus its <see cref="ICliCommandHandler"/> implementation — no change to the dispatcher's control
/// flow. Lookup is ordinal to match the canonical <see cref="CliCommandNames"/> constants exactly.
/// </summary>
internal static class CliCommandHandlers
{
    private static readonly IReadOnlyDictionary<string, ICliCommandHandler> Handlers =
        new Dictionary<string, ICliCommandHandler>(StringComparer.Ordinal)
        {
            [CliCommandNames.List] = new ListCommandHandler(),
            [CliCommandNames.Get] = new GetCommandHandler(),
            [CliCommandNames.Set] = new SetCommandHandler(),
            [CliCommandNames.Up] = new AdjustCommandHandler(),
            [CliCommandNames.Down] = new AdjustCommandHandler(),
            [CliCommandNames.Capabilities] = new CapabilitiesCommandHandler(),
            [CliCommandNames.Profiles] = new ProfilesCommandHandler(),
            [CliCommandNames.ApplyProfile] = new ApplyProfileCommandHandler(),
        };

    /// <summary>
    /// Resolves the handler for <paramref name="command"/>. Returns <see langword="false"/> for an
    /// unrecognized command name (a newer CLI talking to an older app), which the dispatcher maps to
    /// <c>ARGUMENT_ERROR</c>.
    /// </summary>
    public static bool TryGet(string command, out ICliCommandHandler handler)
        => Handlers.TryGetValue(command, out handler!);

    // ─── list ─────────────────────────────────────────────────────────────────
    private sealed class ListCommandHandler : ICliCommandHandler
    {
        public Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            var result = MonitorDtoProjector.BuildListResult(context.Snapshot, context.HiddenIds);
            return Task.FromResult(CliResponse.Serialize(result, ContractsJsonContext.Default.CliListResult));
        }
    }

    // ─── get ──────────────────────────────────────────────────────────────────
    private sealed class GetCommandHandler : ICliCommandHandler
    {
        public Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            var req = context.Envelope.Get ?? new GetRequest();
            var outcome = MonitorDtoProjector.BuildGetResult(
                context.Snapshot,
                context.HiddenIds,
                req.MonitorNumber,
                req.MonitorId,
                req.SettingFilter,
                context.CustomMappings);
            return Task.FromResult(CliResponse.ResultOrError(outcome, ContractsJsonContext.Default.CliGetResult));
        }
    }

    // ─── set ──────────────────────────────────────────────────────────────────
    private sealed class SetCommandHandler : ICliCommandHandler
    {
        public async Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            if (context.Envelope.Set is null)
            {
                return CliResponse.SerializeError(
                    CliResponse.MakeError(CliCommandNames.Set, CliErrorCodes.ArgumentError, "missing 'set' payload"));
            }

            var outcome = await SetCommandExecutor.ExecuteAsync(
                context.Manager,
                context.Snapshot,
                context.HiddenIds,
                context.Envelope.Set,
                ct,
                context.CustomMappings).ConfigureAwait(false);

            return CliResponse.ResultOrError(outcome, ContractsJsonContext.Default.CliSetResult);
        }
    }

    // ─── up / down (relative adjust) ────────────────────────────────────────────
    private sealed class AdjustCommandHandler : ICliCommandHandler
    {
        public async Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            if (context.Envelope.Adjust is null)
            {
                return CliResponse.SerializeError(
                    CliResponse.MakeError(context.Envelope.Command, CliErrorCodes.ArgumentError, "missing 'adjust' payload"));
            }

            var outcome = await AdjustCommandExecutor.ExecuteAsync(
                context.Manager,
                context.Snapshot,
                context.HiddenIds,
                context.Envelope.Adjust,
                isUp: context.Envelope.Command == CliCommandNames.Up,
                context.DefaultStep,
                ct).ConfigureAwait(false);

            return CliResponse.ResultOrError(outcome, ContractsJsonContext.Default.CliSetResult);
        }
    }

    // ─── capabilities ───────────────────────────────────────────────────────────
    private sealed class CapabilitiesCommandHandler : ICliCommandHandler
    {
        public Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            var req = context.Envelope.Capabilities ?? new CapabilitiesRequest();
            var outcome = MonitorDtoProjector.BuildCapabilitiesResult(
                context.Snapshot,
                context.HiddenIds,
                req.MonitorNumber,
                req.MonitorId,
                req.SettingFilter,
                context.CustomMappings);
            return Task.FromResult(CliResponse.ResultOrError(outcome, ContractsJsonContext.Default.CliCapabilitiesResult));
        }
    }

    // ─── profiles ───────────────────────────────────────────────────────────────
    private sealed class ProfilesCommandHandler : ICliCommandHandler
    {
        public async Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            var profiles = await context.LoadProfilesAsync(ct).ConfigureAwait(false);
            var result = ProfileDtoProjector.BuildProfileListResult(profiles);
            return CliResponse.Serialize(result, ContractsJsonContext.Default.CliProfileListResult);
        }
    }

    // ─── apply-profile ────────────────────────────────────────────────────────
    private sealed class ApplyProfileCommandHandler : ICliCommandHandler
    {
        public async Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct)
        {
            var profileId = context.Envelope.ApplyProfile?.ProfileId ?? 0;
            if (profileId <= 0)
            {
                return CliResponse.SerializeError(
                    CliResponse.MakeError(CliCommandNames.ApplyProfile, CliErrorCodes.ArgumentError, "profile id must be positive"));
            }

            var name = await context.ApplyProfileAsync(profileId, ct).ConfigureAwait(false);
            if (name is null)
            {
                return CliResponse.SerializeError(
                    CliResponse.MakeCodedError(
                        CliCommandNames.ApplyProfile,
                        CliErrorCodes.ArgumentError,
                        CliMessageIds.ProfileNotFound,
                        value: profileId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            var applyResult = new CliApplyProfileResult { ProfileId = profileId, Profile = name };
            return CliResponse.Serialize(applyResult, ContractsJsonContext.Default.CliApplyProfileResult);
        }
    }
}
