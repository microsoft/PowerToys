// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Applies catalog visibility rules to explicit preference and alias snapshots.</summary>
internal sealed class AppCatalogVisibility
{
    private readonly string[] _names;
    private readonly string[] _paths;

    /// <summary>Initializes a new instance of the <see cref="AppCatalogVisibility"/> class. Captures trimmed, case-insensitive name and path wildcard exclusions.</summary>
    internal AppCatalogVisibility(IReadOnlyList<string> names, IReadOnlyList<string> paths)
    {
        _names = [.. names.Select(pattern => pattern.Trim()).Where(pattern => pattern.Length > 0)];
        _paths = [.. paths.Select(pattern => pattern.Trim().Replace('\\', '/')).Where(pattern => pattern.Length > 0)];
    }

    /// <summary>Compares the normalized exclusion patterns without regard to letter case.</summary>
    internal bool HasSamePatterns(AppCatalogVisibility other)
    {
        return _names.SequenceEqual(other._names, StringComparer.OrdinalIgnoreCase)
            && _paths.SequenceEqual(other._paths, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Classifies an app using exclusion patterns first, then explicit hides and resolved hidden IDs.</summary>
    internal AppVisibility GetVisibility(
        AppCatalogItem item,
        IReadOnlySet<string> hiddenIdentities,
        IReadOnlySet<string> hiddenCommandIds)
    {
        ArgumentNullException.ThrowIfNull(item);

        var matches = item.Payload switch
        {
            Win32AppPayload app => Matches(_names, app.Name)
                || Matches(_names, app.DisplayName)
                || MatchesPath(_paths, app.TargetPath)
                || MatchesPath(_paths, app.LnkFilePath)
                || MatchesPath(_paths, app.AppExecutionAliasTargetPath),
            PackagedAppPayload app => Matches(_names, app.Name)
                || MatchesPath(_paths, app.PackageLocation),
            _ => false,
        };

        // Merged representations retain their shortcut and executable paths in provenance and search terms.
        matches = matches || (_paths.Length > 0
            && (item.Provenance.References.Any(source => Path.IsPathFullyQualified(source.ItemId) && MatchesPath(_paths, source.ItemId))
                || item.MatchTerms.Any(term => Path.IsPathFullyQualified(term) && MatchesPath(_paths, term))));

        if (matches)
        {
            return AppVisibility.HiddenByPattern;
        }

        var hidden = item.IdentityAliases.Any(hiddenIdentities.Contains);
        if (!hidden && hiddenIdentities.Count > 0)
        {
            hidden = hiddenCommandIds.Contains(AppIdentity.ForCommand(item.Identity));
        }

        return hidden ? AppVisibility.Hidden : AppVisibility.Visible;
    }

    /// <summary>Adds an app's identity aliases when hiding, or removes equivalent saved hides when unhiding.</summary>
    /// <returns>The original identity set when unchanged, or an immutable updated set.</returns>
    internal static IReadOnlySet<string> UpdateHiddenIdentities(
        IReadOnlySet<string> current,
        AppCatalogItem item,
        bool hidden,
        IReadOnlyDictionary<string, string> aliases)
    {
        var updated = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
        if (hidden)
        {
            updated.UnionWith(item.IdentityAliases);
        }
        else
        {
            var commandIds = item.IdentityAliases.Select(AppIdentity.ForCommand).ToHashSet(StringComparer.OrdinalIgnoreCase);
            updated.RemoveWhere(identity =>
            {
                var commandId = AppIdentity.ForCommand(identity);
                return commandIds.Contains(commandId)
                    || (aliases.TryGetValue(commandId, out var target) && commandIds.Contains(target));
            });
        }

        return updated.SetEquals(current) ? current : updated.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves stored hides through retained aliases without rewriting the stored identities.</summary>
    internal static FrozenSet<string> ResolveHiddenCommandIds(
        IReadOnlySet<string> identities,
        IReadOnlyDictionary<string, string> aliases)
    {
        if (identities.Count == 0)
        {
            return FrozenSet<string>.Empty;
        }

        // Resolve once per publication without rewriting the user's stored hides.
        var commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in identities)
        {
            var commandId = AppIdentity.ForCommand(identity);
            commandIds.Add(commandId);
            if (aliases.TryGetValue(commandId, out var target) && !string.IsNullOrEmpty(target))
            {
                commandIds.Add(target);
            }
        }

        return commandIds.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool Matches(IReadOnlyList<string> patterns, string? value)
    {
        return !string.IsNullOrEmpty(value)
            && patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, value, ignoreCase: true));
    }

    // Backslashes escape characters in MatchesSimpleExpression, so normalize Windows separators first.
    private static bool MatchesPath(IReadOnlyList<string> patterns, string? path)
    {
        return patterns.Count > 0 && Matches(patterns, path?.Replace('\\', '/'));
    }
}
