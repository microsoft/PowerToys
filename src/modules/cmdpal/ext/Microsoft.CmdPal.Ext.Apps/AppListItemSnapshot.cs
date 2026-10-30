// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>
/// Represents one atomic publication of visible and hidden application list items.
/// </summary>
public sealed class AppListItemSnapshot
{
    private static readonly char[] InvalidAliasNameCharacters = Path.GetInvalidFileNameChars();
    private static readonly ImmutableDictionary<string, string> EmptyExecutionAliasOwners = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<AppListItem> _visibleItemSet;
    private readonly Dictionary<string, AppListItem?> _byCommandId;
    private readonly Dictionary<string, AppListItem?> _byNormalizedCommandId;

    /// <summary>
    /// Gets applications eligible for normal user-facing views.
    /// </summary>
    public IReadOnlyList<AppListItem> VisibleItems { get; }

    /// <summary>
    /// Gets applications explicitly hidden by the user.
    /// </summary>
    public IReadOnlyList<AppListItem> HiddenItems { get; }

    /// <summary>Gets applications hidden by global name or path exclusion patterns.</summary>
    public IReadOnlyList<AppListItem> PatternHiddenItems { get; }

    /// <summary>Gets the exact executable-name priority policy for searches against this snapshot.</summary>
    public ExecutableNameMatchMode ExecutableNameMatchMode { get; }

    /// <summary>Gets the immutable execution-alias owners captured for this publication.</summary>
    public ImmutableDictionary<string, string> ExecutionAliasOwners { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppListItemSnapshot"/> class. Initializes an atomic visible and hidden list-item snapshot.
    /// </summary>
    /// <param name="visibleItems">Applications eligible for normal user-facing views.</param>
    /// <param name="hiddenItems">Applications explicitly hidden by the user.</param>
    /// <param name="patternHiddenItems">Applications hidden by global exclusion patterns.</param>
    /// <param name="commandAliases">Saved IDs mapped to the current application commands.</param>
    /// <param name="executableNameMatchMode">Exact executable-name priority captured for this publication.</param>
    /// <param name="executionAliasOwners">Cached execution-alias filename to AUMID preferences.</param>
    public AppListItemSnapshot(
        IReadOnlyList<AppListItem> visibleItems,
        IReadOnlyList<AppListItem> hiddenItems,
        IReadOnlyList<AppListItem>? patternHiddenItems = null,
        IReadOnlyDictionary<string, string>? commandAliases = null,
        ExecutableNameMatchMode executableNameMatchMode = ExecutableNameMatchMode.FilenameAndStem,
        ImmutableDictionary<string, string>? executionAliasOwners = null)
    {
        ArgumentNullException.ThrowIfNull(visibleItems);
        ArgumentNullException.ThrowIfNull(hiddenItems);

        VisibleItems = visibleItems;
        HiddenItems = hiddenItems;
        PatternHiddenItems = patternHiddenItems ?? [];
        ExecutableNameMatchMode = executableNameMatchMode;
        ExecutionAliasOwners = executionAliasOwners ?? EmptyExecutionAliasOwners;
        _visibleItemSet = new(VisibleItems);
        _byCommandId = new(StringComparer.Ordinal);
        _byNormalizedCommandId = new(StringComparer.OrdinalIgnoreCase);
        AddCanonicalItems(VisibleItems);
        AddCanonicalItems(HiddenItems);
        AddCanonicalItems(PatternHiddenItems);
        AddItemAliases(VisibleItems);
        AddItemAliases(HiddenItems);
        AddItemAliases(PatternHiddenItems);

        foreach (var entry in _byCommandId)
        {
            if (AppIdentity.TryNormalizeCommandId(entry.Key, out var normalized))
            {
                AddNormalizedCommandId(normalized, entry.Value);
            }
        }

        if (commandAliases is not null)
        {
            // Apply saved typed conflicts before resolving legacy targets.
            List<KeyValuePair<string, string>>? legacyAliases = null;
            foreach (var alias in commandAliases)
            {
                if (AppIdentity.TryNormalizeCommandId(alias.Key, out var normalized))
                {
                    AddSavedAlias(alias.Key, alias.Value, normalized);
                }
                else
                {
                    legacyAliases ??= [];
                    legacyAliases.Add(alias);
                }
            }

            if (legacyAliases is not null)
            {
                foreach (var alias in legacyAliases)
                {
                    AddSavedAlias(alias.Key, alias.Value, normalizedId: null);
                }
            }
        }
    }

    private AppListItemSnapshot(AppListItemSnapshot previous, ImmutableDictionary<string, string> executionAliasOwners)
    {
        VisibleItems = previous.VisibleItems;
        HiddenItems = previous.HiddenItems;
        PatternHiddenItems = previous.PatternHiddenItems;
        ExecutableNameMatchMode = previous.ExecutableNameMatchMode;
        ExecutionAliasOwners = executionAliasOwners;
        _visibleItemSet = previous._visibleItemSet;
        _byCommandId = previous._byCommandId;
        _byNormalizedCommandId = previous._byNormalizedCommandId;
    }

    /// <summary>Looks up a bare alias filename or stem using only this captured ownership map.</summary>
    public string? GetExecutionAliasOwner(string query)
    {
        query = query.Trim();
        if (query.Length == 0 || query.IndexOfAny(InvalidAliasNameCharacters) >= 0)
        {
            return null;
        }

        var alias = query.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? query : query + ".exe";
        return ExecutionAliasOwners.GetValueOrDefault(alias);
    }

    /// <summary>Captures new alias ownership while reusing the existing rows and command-resolution indexes.</summary>
    /// <returns>This snapshot when the ownership map is unchanged; otherwise, a snapshot sharing its rows.</returns>
    internal AppListItemSnapshot WithExecutionAliasOwners(ImmutableDictionary<string, string> owners)
    {
        return ReferenceEquals(owners, ExecutionAliasOwners) ? this : new AppListItemSnapshot(this, owners);
    }

    private void AddCanonicalItems(IReadOnlyList<AppListItem> items)
    {
        foreach (var item in items)
        {
            if (item.Command is { } command)
            {
                _byCommandId.TryAdd(command.Id, item);
            }
        }
    }

    private void AddItemAliases(IReadOnlyList<AppListItem> items)
    {
        foreach (var item in items)
        {
            foreach (var id in item.App.CommandIds)
            {
                AddAlias(id, item);
            }
        }
    }

    private void AddAlias(string id, AppListItem item)
    {
        if (!_byCommandId.TryGetValue(id, out var previous))
        {
            _byCommandId[id] = item;
        }
        else if (previous?.Command!.Id != id && !ReferenceEquals(previous, item))
        {
            // Hidden claimants must not invalidate a visible app's alias.
            if (previous is not null && _visibleItemSet.Contains(previous) && !_visibleItemSet.Contains(item))
            {
                return;
            }

            _byCommandId[id] = null;
        }
    }

    private void AddSavedAlias(string id, string target, string? normalizedId)
    {
        if (_byCommandId.TryGetValue(id, out var current) && current?.Command!.Id == id)
        {
            return;
        }

        if (normalizedId is not null
            && _byNormalizedCommandId.TryGetValue(normalizedId, out current)
            && current?.Command is { } command
            && string.Equals(normalizedId, command.Id, StringComparison.OrdinalIgnoreCase))
        {
            // Saved aliases cannot replace an unambiguous current canonical ID.
            return;
        }

        AppListItem? item;
        if (string.IsNullOrEmpty(target))
        {
            item = null;
        }
        else if (AppIdentity.TryNormalizeCommandId(target, out var normalized)
            ? !_byNormalizedCommandId.TryGetValue(normalized, out item)
            : !_byCommandId.TryGetValue(target, out item))
        {
            return;
        }

        _byCommandId[id] = item;
        if (normalizedId is not null)
        {
            AddNormalizedCommandId(normalizedId, item);
        }
    }

    private void AddNormalizedCommandId(string normalizedId, AppListItem? item)
    {
        if (_byNormalizedCommandId.TryGetValue(normalizedId, out var previous) && !ReferenceEquals(previous, item))
        {
            // Prefer the visible claimant without overriding ambiguity markers.
            if (previous is not null && item is not null && _visibleItemSet.Contains(previous) != _visibleItemSet.Contains(item))
            {
                item = _visibleItemSet.Contains(previous) ? previous : item;
            }
            else
            {
                item = null;
            }
        }

        _byNormalizedCommandId[normalizedId] = item;
    }

    /// <summary>Resolves a current app regardless of visibility for explicitly saved commands.</summary>
    public AppListItem? GetApp(string commandId)
    {
        if (AppIdentity.TryNormalizeCommandId(commandId, out var normalized))
        {
            return _byNormalizedCommandId.GetValueOrDefault(normalized);
        }

        return _byCommandId.GetValueOrDefault(commandId);
    }

    /// <summary>Resolves an app command against this snapshot's discovery visibility policy.</summary>
    public AppListItem? GetVisibleApp(string commandId)
    {
        var item = GetApp(commandId);
        return item is not null && _visibleItemSet.Contains(item) ? item : null;
    }

    /// <summary>Compares saved-command lookups, excluding search-only policy changes.</summary>
    internal bool HasSameCommandResolution(AppListItemSnapshot other)
    {
        if (!_visibleItemSet.SetEquals(other._visibleItemSet) || _byCommandId.Count != other._byCommandId.Count
            || _byNormalizedCommandId.Count != other._byNormalizedCommandId.Count)
        {
            return false;
        }

        return HaveSameEntries(_byCommandId, other._byCommandId)
            && HaveSameEntries(_byNormalizedCommandId, other._byNormalizedCommandId);
    }

    private static bool HaveSameEntries(Dictionary<string, AppListItem?> left, Dictionary<string, AppListItem?> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        foreach (var entry in left)
        {
            if (!right.TryGetValue(entry.Key, out var value) || !ReferenceEquals(entry.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Resolves a current app, including a hidden app, while preserving the requested persisted command ID.</summary>
    public ICommandItem? GetCommandItem(string commandId)
    {
        var item = GetApp(commandId);
        return item is null || string.Equals(item.Command!.Id, commandId, StringComparison.Ordinal)
            ? item
            : new AppCommandAlias(item, commandId);
    }
}
