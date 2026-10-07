// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Reconciles retained command IDs with the current catalog without changing persisted state.</summary>
internal static class AppCatalogCommandAliases
{
    /// <summary>Reconciles observed app IDs with retained aliases and safe install-move redirects.</summary>
    /// <remarks>Empty targets mark ambiguous aliases and remain ambiguous; absent apps do not expire saved aliases.</remarks>
    /// <returns>The original map when unchanged, or a replacement immutable map.</returns>
    internal static FrozenDictionary<string, string> Retain(
        FrozenDictionary<string, string> savedAliases,
        IEnumerable<AppItem> apps)
    {
        // Alias keys preserve persisted spelling; their typed command targets compare case-insensitively.
        var observed = new Dictionary<string, string>(StringComparer.Ordinal);
        var launchAliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var app in apps)
        {
            if (string.IsNullOrEmpty(app.CatalogId))
            {
                continue;
            }

            var commandId = AppIdentity.ForCommand(app.CatalogId);
            var legacyId = AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget);
            if (!string.IsNullOrEmpty(app.LaunchTarget))
            {
                launchAliases.Add(legacyId);
            }

            foreach (var alias in app.CommandIds.Prepend(legacyId).Append(commandId))
            {
                // An ambiguous legacy ID cannot safely select either launch entry.
                observed[alias] = observed.TryGetValue(alias, out var previous) && !string.Equals(previous, commandId, StringComparison.OrdinalIgnoreCase) ? string.Empty : commandId;
            }
        }

        var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var alias in observed)
        {
            if ((AppIdentity.IsCommandId(alias.Key) || launchAliases.Contains(alias.Key))
                && !string.IsNullOrEmpty(alias.Value)
                && savedAliases.TryGetValue(alias.Key, out var previous)
                && !string.IsNullOrEmpty(previous) && !string.Equals(previous, alias.Value, StringComparison.OrdinalIgnoreCase)
                && (!observed.TryGetValue(previous, out var target) || !string.Equals(target, previous, StringComparison.OrdinalIgnoreCase)))
            {
                // An unchanged launch entry can follow an install move, but never replace a current app.
                redirects[previous] = redirects.TryGetValue(previous, out var other) && !string.Equals(other, alias.Value, StringComparison.OrdinalIgnoreCase) ? string.Empty : alias.Value;
            }
        }

        foreach (var redirect in redirects)
        {
            observed[redirect.Key] = observed.TryGetValue(redirect.Key, out var target) && !string.Equals(target, redirect.Value, StringComparison.OrdinalIgnoreCase) ? string.Empty : redirect.Value;
        }

        Dictionary<string, string>? updated = null;
        foreach (var alias in savedAliases)
        {
            if (observed.TryGetValue(alias.Value, out var commandId) && !string.Equals(commandId, alias.Value, StringComparison.OrdinalIgnoreCase))
            {
                updated ??= new(savedAliases, StringComparer.Ordinal);
                updated[alias.Key] = commandId;
            }
        }

        foreach (var alias in observed)
        {
            if (alias.Key == alias.Value)
            {
                continue;
            }

            var current = updated is null ? (IReadOnlyDictionary<string, string>)savedAliases : updated;
            var exists = current.TryGetValue(alias.Key, out var previous);
            var commandId = exists && !string.Equals(previous, alias.Value, StringComparison.OrdinalIgnoreCase) ? string.Empty : alias.Value;
            if (!exists || previous != commandId)
            {
                updated ??= new(savedAliases, StringComparer.Ordinal);
                updated[alias.Key] = commandId;
            }
        }

        return updated is null
            ? savedAliases
            : updated.Where(pair => pair.Key != pair.Value).ToFrozenDictionary(StringComparer.Ordinal);
    }
}
