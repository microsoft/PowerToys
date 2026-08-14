// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class TopLevelCommandResolver
{
    internal const int RecentCommandLimit = 5;

    internal sealed record Sections<TCommand>(
        IReadOnlyList<TCommand> Pinned,
        IReadOnlyList<TCommand> Recent,
        IReadOnlyList<TCommand> Regular);

    internal static Sections<IListItem> Resolve(
        IEnumerable<PinnedCommandSettings> pinnedCommands,
        IEnumerable<string> recentCommandIds,
        IEnumerable<TopLevelViewModel> availableCommands,
        bool includeApps,
        int recentCommandLimit = RecentCommandLimit,
        bool includeRegular = true)
    {
        static IListItem? ResolveRecentApp(string commandId) =>
            AllAppsCommandProvider.Page.TryGetCurrentItem(commandId, out var item) ? item : null;

        Func<string, IListItem?>? additionalRecentResolver = includeApps ? ResolveRecentApp : null;
        return Resolve<IListItem>(
            pinnedCommands,
            recentCommandIds,
            availableCommands,
            GetProviderId,
            GetCommandId,
            IsEligibleForHome,
            additionalRecentResolver,
            recentCommandLimit,
            includeRegular);
    }

    internal static string GetProviderId(IListItem command) =>
        command is TopLevelViewModel topLevel ? topLevel.CommandProviderId : AllAppsCommandProvider.WellKnownId;

    internal static string GetCommandId(IListItem command) =>
        command is TopLevelViewModel topLevel ? topLevel.Id : command.Command?.Id ?? string.Empty;

    internal static bool IsEligibleForHome(IListItem command) =>
        command is TopLevelViewModel topLevel
            ? TopLevelCommandEligibility.IsEligibleForHome(topLevel)
            : command.Command is not null && !string.IsNullOrEmpty(command.Title);

    internal static Sections<TCommand> Resolve<TCommand>(
        IEnumerable<PinnedCommandSettings> pinnedCommands,
        IEnumerable<string> recentCommandIds,
        IEnumerable<TCommand> availableCommands,
        Func<TCommand, string> providerIdSelector,
        Func<TCommand, string> commandIdSelector,
        Func<TCommand, bool> isEligible,
        Func<string, TCommand?>? resolveAdditionalRecentCommand = null,
        int recentCommandLimit = RecentCommandLimit,
        bool includeRegular = true)
        where TCommand : class
    {
        var eligibleCommands = new List<TCommand>();
        var commandsByProviderAndId = new Dictionary<(string ProviderId, string CommandId), TCommand>();
        var commandsById = new Dictionary<string, TCommand>(StringComparer.Ordinal);

        foreach (var command in availableCommands)
        {
            if (!isEligible(command))
            {
                continue;
            }

            if (includeRegular)
            {
                eligibleCommands.Add(command);
            }

            var providerId = providerIdSelector(command);
            var commandId = commandIdSelector(command);
            commandsByProviderAndId.TryAdd((providerId, commandId), command);
            if (!string.IsNullOrEmpty(commandId))
            {
                commandsById.TryAdd(commandId, command);
            }
        }

        var featuredCommandKeys = new HashSet<(string ProviderId, string CommandId)>();
        var featuredCommandIds = new HashSet<string>(StringComparer.Ordinal);
        var pinned = new List<TCommand>();
        foreach (var pinnedCommand in pinnedCommands)
        {
            var key = (pinnedCommand.ProviderId, pinnedCommand.CommandId);
            if (commandsByProviderAndId.TryGetValue(key, out var command) && featuredCommandKeys.Add(key))
            {
                pinned.Add(command);
                if (!string.IsNullOrEmpty(pinnedCommand.CommandId))
                {
                    featuredCommandIds.Add(pinnedCommand.CommandId);
                }
            }
        }

        var recent = new List<TCommand>();
        if (recentCommandLimit > 0)
        {
            foreach (var commandId in recentCommandIds)
            {
                if (recent.Count == recentCommandLimit)
                {
                    break;
                }

                if (string.IsNullOrEmpty(commandId) || featuredCommandIds.Contains(commandId))
                {
                    continue;
                }

                if (!commandsById.TryGetValue(commandId, out var command))
                {
                    command = resolveAdditionalRecentCommand?.Invoke(commandId);
                    if (command is null || !isEligible(command))
                    {
                        continue;
                    }
                }

                var key = (providerIdSelector(command), commandIdSelector(command));
                if (featuredCommandKeys.Add(key))
                {
                    recent.Add(command);
                    featuredCommandIds.Add(commandId);
                }
            }
        }

        IReadOnlyList<TCommand> regular = [];
        if (includeRegular)
        {
            var regularCommands = new List<TCommand>(eligibleCommands.Count);
            foreach (var command in eligibleCommands)
            {
                var key = (providerIdSelector(command), commandIdSelector(command));
                if (!featuredCommandKeys.Contains(key))
                {
                    regularCommands.Add(command);
                }
            }

            regular = regularCommands;
        }

        return new Sections<TCommand>(pinned, recent, regular);
    }
}
