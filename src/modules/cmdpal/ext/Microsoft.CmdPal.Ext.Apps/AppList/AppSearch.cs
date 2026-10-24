// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.AppList;

/// <summary>Evaluates application fields using one captured matcher, query and executable-name policy.</summary>
public sealed class AppSearch
{
    private readonly IPrecomputedFuzzyMatcher _matcher;
    private readonly FuzzyQuery _query;
    private readonly int _minimumMatchScore;
    private readonly bool _searchPaths;
    private readonly bool _matchShortcutNames;
    private readonly bool _matchExecutableNames;
    private readonly bool _matchExecutableStem;
    private readonly string? _activeExecutionAliasAumid;

    public int QueryLength => _query.Original.Length;

    /// <summary>Initializes a new instance of the <see cref="AppSearch"/> class. Captures a query, matcher, and executable-name priority policy for reuse across application rows.</summary>
    /// <param name="query">The query whose trimmed text is precomputed.</param>
    /// <param name="matcher">The matcher used for the query and application targets.</param>
    /// <param name="executableNameMatchMode">Controls exact executable-name priority without disabling ordinary fuzzy matches.</param>
    /// <param name="activeExecutionAliasAumid">The selected execution-alias owner, or null when the query is not a known alias.</param>
    public AppSearch(
        string query,
        IPrecomputedFuzzyMatcher matcher,
        ExecutableNameMatchMode executableNameMatchMode,
        string? activeExecutionAliasAumid = null)
    {
        _matcher = matcher;
        _query = matcher.PrecomputeQuery(query.Trim());
        var idealTarget = matcher.PrecomputeTarget(_query.Original);
        _minimumMatchScore = (int)Math.Ceiling(matcher.Score(_query, idealTarget) * 0.75);
        _searchPaths = _query.Original.AsSpan().IndexOfAny('\\', '/', ':') >= 0;
        _matchShortcutNames = !_searchPaths && !_query.Original.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        _matchExecutableNames = executableNameMatchMode != ExecutableNameMatchMode.Disabled;
        _matchExecutableStem = executableNameMatchMode == ExecutableNameMatchMode.FilenameAndStem
            && !PathHelpers.IsExecutablePath(_query.Original);
        _activeExecutionAliasAumid = activeExecutionAliasAumid;
    }

    /// <summary>Computes fuzzy scores and exact-match signals without applying Home or All Apps ordering.</summary>
    public Match Evaluate(AppListItem item)
    {
        var exactExecutableFilenameMatch = false;
        var exactExecutableMatch = false;
        var allowExecutablePriority = _matchExecutableNames && string.IsNullOrWhiteSpace(item.App.LaunchArguments);
        var executableNames = item.ExecutableNames;
        for (var i = 0; i < executableNames.Count; i++)
        {
            var name = executableNames[i];
            if (string.Equals(_query.Original, name, StringComparison.OrdinalIgnoreCase))
            {
                exactExecutableFilenameMatch = true;
                exactExecutableMatch = allowExecutablePriority;
                break;
            }

            if (allowExecutablePriority && _matchExecutableStem && Path.GetFileNameWithoutExtension(name.AsSpan()).Equals(_query.Original.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                exactExecutableMatch = true;
            }
        }

        var targets = item.GetSearchTargets(_matcher);
        var titleScore = _matcher.Score(_query, targets.Title);
        var descriptionScore = string.Equals(targets.Title.Original, targets.Description.Original, StringComparison.OrdinalIgnoreCase)
            ? titleScore
            : _matcher.Score(_query, targets.Description);
        var metadataScore = 0;
        var exactMetadataMatch = false;
        foreach (ref readonly var target in (_searchPaths ? targets.PathMetadata : targets.Metadata).AsSpan())
        {
            var score = string.Equals(target.Original, targets.Title.Original, StringComparison.OrdinalIgnoreCase)
                ? titleScore
                : string.Equals(target.Original, targets.Description.Original, StringComparison.OrdinalIgnoreCase)
                    ? descriptionScore
                    : _matcher.Score(_query, target);
            metadataScore = Math.Max(metadataScore, score);
            exactMetadataMatch |= string.Equals(_query.Original, target.Original, StringComparison.OrdinalIgnoreCase);
        }

        int? shortcutIndex = null;
        var exactTitleMatch = string.Equals(_query.Original, targets.Title.Original, StringComparison.OrdinalIgnoreCase);
        if (!_searchPaths)
        {
            // Dotted display names are not executable queries unless they name this app's executable.
            var selectShortcut = _matchShortcutNames && !exactTitleMatch && !exactExecutableFilenameMatch;
            var bestScore = titleScore;
            var bestIsExact = false;
            var comparison = selectShortcut ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (ref readonly var shortcut in targets.Shortcuts.AsSpan())
            {
                if (!selectShortcut && !shortcut.IsMetadata)
                {
                    continue;
                }

                var target = shortcut.Target;
                var score = string.Equals(target.Original, targets.Title.Original, comparison)
                    ? titleScore
                    : string.Equals(target.Original, targets.Description.Original, comparison)
                        ? descriptionScore
                        : _matcher.Score(_query, target);
                var isExact = string.Equals(_query.Original, target.Original, StringComparison.OrdinalIgnoreCase);
                if (shortcut.IsMetadata)
                {
                    var metadataTargetScore = string.Equals(target.Original, targets.Title.Original, StringComparison.OrdinalIgnoreCase)
                        ? titleScore
                        : string.Equals(target.Original, targets.Description.Original, StringComparison.OrdinalIgnoreCase)
                            ? descriptionScore
                            : score;
                    metadataScore = Math.Max(metadataScore, metadataTargetScore);
                }

                if (selectShortcut)
                {
                    metadataScore = Math.Max(metadataScore, score);
                }

                exactMetadataMatch |= isExact;
                if (selectShortcut && score > 0 && score >= _minimumMatchScore
                    && ((isExact && !bestIsExact) || (isExact == bestIsExact && score > bestScore)))
                {
                    shortcutIndex = shortcut.Index;
                    bestScore = score;
                    bestIsExact = isExact;
                }
            }

            if (shortcutIndex is not null)
            {
                titleScore = bestScore;
                exactTitleMatch = bestIsExact;
            }
        }

        var preferredExecutionAliasMatch = exactMetadataMatch
            && !string.IsNullOrEmpty(_activeExecutionAliasAumid)
            && string.Equals(_activeExecutionAliasAumid, item.App.AppUserModelId, StringComparison.OrdinalIgnoreCase);
        return new Match(titleScore, descriptionScore, metadataScore, _minimumMatchScore, exactMetadataMatch, exactExecutableMatch, exactTitleMatch, preferredExecutionAliasMatch, shortcutIndex);
    }

    public readonly record struct Match(
        int TitleScore,
        int DescriptionScore,
        int MetadataScore,
        int MinimumMatchScore,
        bool IsExactMetadataMatch,
        bool IsExactExecutableMatch,
        bool IsExactTitleMatch,
        bool IsPreferredExecutionAliasMatch = false,
        int? ShortcutIndex = null)
    {
        public bool HasMetadataMatch => IsExactMetadataMatch || (MetadataScore > 0 && MetadataScore >= MinimumMatchScore);

        public bool HasMatch => TitleScore > 0 || DescriptionScore > 0 || HasMetadataMatch;

        public double LexicalScore => Math.Max(TitleScore, IsExactExecutableMatch || IsPreferredExecutionAliasMatch ? MetadataScore : (Math.Max(DescriptionScore, MetadataScore) - 4) / 2.0);
    }

    internal readonly record struct ShortcutTarget(FuzzyTarget Target, int Index, bool IsMetadata);

    internal sealed record Targets(uint SchemaId, FuzzyTarget Title, FuzzyTarget Description, FuzzyTarget[] Metadata, FuzzyTarget[] PathMetadata, ShortcutTarget[] Shortcuts);
}
