// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class TopLevelCommandResolver
{
    internal sealed record Sections<TCommand>(
        IReadOnlyList<TCommand> Pinned,
        IReadOnlyList<TCommand> Recent,
        IReadOnlyList<TCommand> Regular);

    /// <summary>Resolves Home pins, recents, and regular commands against one captured Apps snapshot.</summary>
    /// <remarks>Explicit pins can resolve hidden apps; ordinary app results and app recents respect discovery visibility.</remarks>
    internal static Sections<InProcObject<IListItem>> Resolve(
        IEnumerable<PinnedCommandSettings> pinnedCommands,
        IEnumerable<string> recentCommandIds,
        IEnumerable<TopLevelViewModel> availableCommands,
        InProcObject<IListPage> allAppsPage,
        bool includeApps,
        int pinnedCommandLimit = int.MaxValue,
        int recentCommandLimit = SettingsModel.DefaultRecentCommandsDisplayLimit,
        bool includeRegular = true,
        bool recentCommandsFirst = false)
    {
        var appSnapshot = GetAppsSnapshot(allAppsPage);
        var pins = pinnedCommands.ToArray();
        var pinnedAppCommandIds = pins.Where(pin => pin.ProviderId == AllAppsCommandProvider.WellKnownId)
            .Select(pin => pin.CommandId)
            .ToHashSet(StringComparer.Ordinal);
        var identities = new Dictionary<IListItem, (string ProviderId, string CommandId, bool IsApp)>(ReferenceEqualityComparer.Instance);
        var appCommandsBySavedId = new Dictionary<string, TopLevelViewModel>(StringComparer.Ordinal);
        var preferredAppCommands = new Dictionary<string, TopLevelViewModel>(StringComparer.Ordinal);
        var commands = new List<IListItem>();
        foreach (var command in availableCommands)
        {
            if (!TopLevelCommandEligibility.IsEligibleForHome(command))
            {
                continue;
            }

            var providerId = command.CommandProviderId;
            var commandId = command.Id;
            var isApp = providerId == AllAppsCommandProvider.WellKnownId && command.CommandViewModel.IsInvokableCommand;
            if (providerId == AllAppsCommandProvider.WellKnownId)
            {
                if (!includeApps)
                {
                    continue;
                }

                var savedCommandId = commandId;
                if (isApp)
                {
                    var pinned = pinnedAppCommandIds.Contains(savedCommandId);
                    var app = pinned ? appSnapshot.GetApp(commandId) : appSnapshot.GetVisibleApp(commandId);
                    if (app is null)
                    {
                        continue;
                    }

                    // Explicit pins retain hidden apps until the user unpins them.
                    commandId = app.Command!.Id;
                    preferredAppCommands.TryAdd(commandId, command);
                }

                appCommandsBySavedId.TryAdd(savedCommandId, command);
            }

            identities.TryAdd(command, (providerId, commandId, isApp));
            commands.Add(command);
        }

        var preferredPinnedAppIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pin in pins)
        {
            if (pin.ProviderId == AllAppsCommandProvider.WellKnownId && appCommandsBySavedId.TryGetValue(pin.CommandId, out var command))
            {
                var canonicalId = identities[command].CommandId;
                if (identities[command].IsApp && preferredPinnedAppIds.Add(canonicalId))
                {
                    preferredAppCommands[canonicalId] = command;
                }
            }
        }

        var visibleCommands = commands.Where(command => !identities[command].IsApp
            || ReferenceEquals(preferredAppCommands[identities[command].CommandId], command));
        var visiblePins = pins.Where(pin => pin.ProviderId != AllAppsCommandProvider.WellKnownId || appCommandsBySavedId.ContainsKey(pin.CommandId))
            .Select(pin => pin.ProviderId == AllAppsCommandProvider.WellKnownId
                ? pin with { CommandId = identities[appCommandsBySavedId[pin.CommandId]].CommandId }
                : pin);
        Func<string, InProcObject<IListItem>?>? additionalRecentResolver = includeApps ? ResolveRecentApp : null;
        return Resolve<InProcObject<IListItem>>(
            visiblePins,
            recentCommandIds.Select(CanonicalAppId),
            visibleCommands.Select(command => new InProcObject<IListItem>(command)),
            command => GetIdentity(command).ProviderId,
            command => GetIdentity(command).CommandId,
            IsEligibleForHome,
            additionalRecentResolver,
            pinnedCommandLimit,
            recentCommandLimit,
            includeRegular,
            recentCommandsFirst);

        InProcObject<IListItem>? ResolveRecentApp(string commandId)
        {
            var item = appSnapshot.GetVisibleApp(commandId);
            return item is not null ? new(item) : null;
        }

        string CanonicalAppId(string commandId)
        {
            return includeApps ? appSnapshot.GetApp(commandId)?.Command?.Id ?? commandId : commandId;
        }

        (string ProviderId, string CommandId, bool IsApp) GetIdentity(InProcObject<IListItem> command)
        {
            return identities.TryGetValue(command.Safe, out var identity)
                ? identity
                : (GetProviderId(command), GetCommandId(command), true);
        }
    }

    internal static string GetProviderId(InProcObject<IListItem> command) =>
        command.Safe is TopLevelViewModel topLevel ? topLevel.CommandProviderId : AllAppsCommandProvider.WellKnownId;

    internal static string GetCommandId(InProcObject<IListItem> command) =>
        command.Safe is TopLevelViewModel topLevel ? topLevel.Id : command.Safe.Command?.Id ?? string.Empty;

    internal static bool IsEligibleForHome(InProcObject<IListItem> command) =>
        command.Safe is TopLevelViewModel topLevel
            ? TopLevelCommandEligibility.IsEligibleForHome(topLevel)
            : command.Safe.Command is not null && !string.IsNullOrEmpty(command.Safe.Title);

    internal static Sections<TCommand> Resolve<TCommand>(
        IEnumerable<PinnedCommandSettings> pinnedCommands,
        IEnumerable<string> recentCommandIds,
        IEnumerable<TCommand> availableCommands,
        Func<TCommand, string> providerIdSelector,
        Func<TCommand, string> commandIdSelector,
        Func<TCommand, bool> isEligible,
        Func<string, TCommand?>? resolveAdditionalRecentCommand = null,
        int pinnedCommandLimit = int.MaxValue,
        int recentCommandLimit = SettingsModel.DefaultRecentCommandsDisplayLimit,
        bool includeRegular = true,
        bool recentCommandsFirst = false)
        where TCommand : class
    {
        var eligibleCommands = new List<(TCommand Command, (string ProviderId, string CommandId) Key)>();
        var commandsByProviderAndId = new Dictionary<(string ProviderId, string CommandId), TCommand>();
        var commandsById = new Dictionary<string, TCommand>(StringComparer.Ordinal);

        foreach (var command in availableCommands)
        {
            if (!isEligible(command))
            {
                continue;
            }

            var providerId = providerIdSelector(command);
            var commandId = commandIdSelector(command);
            var key = (providerId, commandId);
            if (includeRegular)
            {
                eligibleCommands.Add((command, key));
            }

            commandsByProviderAndId.TryAdd(key, command);
            if (!string.IsNullOrEmpty(commandId))
            {
                commandsById.TryAdd(commandId, command);
            }
        }

        var featuredCommandKeys = new HashSet<(string ProviderId, string CommandId)>();
        var featuredCommandIds = new HashSet<string>(StringComparer.Ordinal);
        var pinned = new List<TCommand>();
        var recent = new List<TCommand>();

        void ResolvePinnedCommands()
        {
            var effectivePinnedCommandLimit = Math.Max(0, pinnedCommandLimit);
            foreach (var pinnedCommand in pinnedCommands)
            {
                if (pinned.Count >= effectivePinnedCommandLimit)
                {
                    break;
                }

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
        }

        void ResolveRecentCommands()
        {
            if (recentCommandLimit <= 0)
            {
                return;
            }

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

        // Resolve in presentation order so the first section owns duplicates and the second
        // section can continue scanning to fill its configured limit with distinct items.
        if (recentCommandsFirst)
        {
            ResolveRecentCommands();
            ResolvePinnedCommands();
        }
        else
        {
            ResolvePinnedCommands();
            ResolveRecentCommands();
        }

        IReadOnlyList<TCommand> regular = [];
        if (includeRegular)
        {
            var regularCommands = new List<TCommand>(eligibleCommands.Count);
            foreach (var (command, key) in eligibleCommands)
            {
                if (!featuredCommandKeys.Contains(key))
                {
                    regularCommands.Add(command);
                }
            }

            regular = regularCommands;
        }

        return new Sections<TCommand>(pinned, recent, regular);
    }

    internal static AppListItemSnapshot GetAppsSnapshot(InProcObject<IListPage> page)
    {
        return page.Safe is AllAppsPage appsPage
            ? appsPage.GetSnapshot()
            : new AppListItemSnapshot(page.Safe.GetItems().OfType<Microsoft.CmdPal.Ext.Apps.Programs.AppListItem>().ToArray(), []);
    }
}
