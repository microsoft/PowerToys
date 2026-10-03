// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps;

/// <summary>Evaluates application fields using one captured matcher, query and executable-name policy.</summary>
public sealed class AppSearch
{
    private readonly IPrecomputedFuzzyMatcher _matcher;
    private readonly FuzzyQuery _query;
    private readonly int _minimumMatchScore;
    private readonly bool _searchPaths;
    private readonly bool _matchExecutableNames;
    private readonly bool _matchExecutableStem;

    public AppSearch(
        string query,
        IPrecomputedFuzzyMatcher matcher,
        ExecutableNameMatchMode executableNameMatchMode)
    {
        _matcher = matcher;
        _query = matcher.PrecomputeQuery(query.Trim());
        var idealTarget = matcher.PrecomputeTarget(_query.Original);
        _minimumMatchScore = (int)Math.Ceiling(matcher.Score(_query, idealTarget) * 0.75);
        _searchPaths = _query.Original.AsSpan().IndexOfAny('\\', '/', ':') >= 0;
        _matchExecutableNames = executableNameMatchMode != ExecutableNameMatchMode.Disabled;
        _matchExecutableStem = executableNameMatchMode == ExecutableNameMatchMode.FilenameAndStem
            && !Win32Program.IsExecutablePath(_query.Original);
    }

    public int QueryLength => _query.Original.Length;

    public Match Evaluate(AppListItem item)
    {
        var targets = item.GetSearchTargets(_matcher);
        var titleScore = _matcher.Score(_query, targets.Title);
        var descriptionScore = string.Equals(targets.Title.Original, targets.Description.Original, StringComparison.OrdinalIgnoreCase)
            ? titleScore
            : _matcher.Score(_query, targets.Description);
        var metadataScore = 0;
        var exactMetadataMatch = false;
        foreach (var target in _searchPaths ? targets.PathMetadata : targets.Metadata)
        {
            var score = string.Equals(target.Original, targets.Title.Original, StringComparison.OrdinalIgnoreCase)
                ? titleScore
                : string.Equals(target.Original, targets.Description.Original, StringComparison.OrdinalIgnoreCase)
                    ? descriptionScore
                    : _matcher.Score(_query, target);
            metadataScore = Math.Max(metadataScore, score);
            exactMetadataMatch |= string.Equals(_query.Original, target.Original, StringComparison.OrdinalIgnoreCase);
        }

        var exactExecutableMatch = false;
        if (_matchExecutableNames)
        {
            var executableNames = item.ExecutableNames;
            for (var i = 0; i < executableNames.Count; i++)
            {
                var name = executableNames[i];
                if (string.Equals(_query.Original, name, StringComparison.OrdinalIgnoreCase)
                    || (_matchExecutableStem && Path.GetFileNameWithoutExtension(name.AsSpan()).Equals(_query.Original.AsSpan(), StringComparison.OrdinalIgnoreCase)))
                {
                    exactExecutableMatch = true;
                    break;
                }
            }
        }

        var exactTitleMatch = string.Equals(_query.Original, targets.Title.Original, StringComparison.OrdinalIgnoreCase);
        return new Match(titleScore, descriptionScore, metadataScore, _minimumMatchScore, exactMetadataMatch, exactExecutableMatch, exactTitleMatch);
    }

    public readonly record struct Match(
        int TitleScore,
        int DescriptionScore,
        int MetadataScore,
        int MinimumMatchScore,
        bool IsExactMetadataMatch,
        bool IsExactExecutableMatch,
        bool IsExactTitleMatch)
    {
        public bool HasMetadataMatch => IsExactMetadataMatch || (MetadataScore > 0 && MetadataScore >= MinimumMatchScore);

        public bool HasMatch => TitleScore > 0 || DescriptionScore > 0 || HasMetadataMatch;

        public double LexicalScore => Math.Max(TitleScore, IsExactExecutableMatch ? MetadataScore : (Math.Max(DescriptionScore, MetadataScore) - 4) / 2.0);
    }

    internal sealed record Targets(uint SchemaId, FuzzyTarget Title, FuzzyTarget Description, FuzzyTarget[] Metadata, FuzzyTarget[] PathMetadata);
}
