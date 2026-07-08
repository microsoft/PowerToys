// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

/// <summary>
/// The principled, deterministic ranking policy for the main/root page. Combines a hard
/// tier ladder with a weighted within-tier score, and packs both into a single sortable
/// integer so the existing score-descending sort continues to work unchanged.
/// </summary>
internal static class MainListRanker
{
    // Each tier occupies a band of this width in the packed score. The within-tier score
    // is clamped to this range so it can never spill into an adjacent tier's band. With a
    // 10M stride and 6 real tiers the maximum packed value (~63M) is far below int.MaxValue.
    internal const int TierStride = 10_000_000;

    // Scale factors that turn signals into within-tier points. These deliberately mirror
    // the previous flat balance (match x10 + history), so items that share a tier keep
    // their long-established relative ordering; only cross-tier behavior changes. Lexical
    // quality leads (x10) while frecency (x1) breaks ties and reorders near-equal matches,
    // and can overcome roughly a single point of lexical difference - matching the old
    // "one use makes VS -> Visual Studio the top hit" intent.
    internal const double LexicalScale = 10.0;
    internal const double FrecencyScale = 1.0;

    // A small nudge for items whose alias merely starts with the query (as opposed to an
    // exact alias, which gets its own top tier). Mirrors the previous +1-before-x10 boost.
    internal const double AliasSubstringBonus = 10.0;

    // Magnitude of the per-provider within-tier nudge. Deliberately small - half a point of
    // lexical quality (LexicalScale = 10) - so a Higher/Lower provider only breaks near-ties
    // and reorders items that already share a tier. It can NEVER move an item across a tier
    // boundary because the packed within-tier score is clamped to a single tier's band.
    internal const double ProviderWeightBonus = 5.0;

    /// <summary>
    /// Maps a per-provider <see cref="ProviderSearchWeight"/> to an additive within-tier
    /// bonus. Lower subtracts, Normal is neutral, Higher adds. The enum's underlying value is
    /// the sign of the nudge, so the result is simply the weight times
    /// <see cref="ProviderWeightBonus"/>. Note the nudge is slightly asymmetric at the tier
    /// floor: <see cref="Pack"/> clamps the within-tier score to a non-negative band, so a
    /// Lower nudge on an item already scoring near 0 (weak match, no history) can clamp to 0
    /// and read the same as Normal, whereas Higher always applies.
    /// </summary>
    public static double ProviderBonus(ProviderSearchWeight weight) => (int)weight * ProviderWeightBonus;

    /// <summary>
    /// Packs a tier and within-tier score into a single descending-sortable integer.
    /// Returns 0 for <see cref="RankTier.None"/> so non-matches are filtered by the
    /// existing "score &gt; 0" gate.
    /// </summary>
    public static int Pack(RankTier tier, double withinTierScore)
    {
        if (tier == RankTier.None)
        {
            return 0;
        }

        var within = (int)Math.Round(Math.Clamp(withinTierScore, 0.0, TierStride - 1));
        return ((int)tier * TierStride) + within;
    }

    /// <summary>
    /// Extracts the tier from a value produced by <see cref="Pack"/>. Intended for tests
    /// and telemetry.
    /// </summary>
    public static RankTier TierOf(int packedScore)
    {
        if (packedScore <= 0)
        {
            return RankTier.None;
        }

        var tier = packedScore / TierStride;
        return (RankTier)Math.Clamp(tier, 0, (int)RankTier.AliasExact);
    }

    /// <summary>
    /// Classifies an item into a relevance tier based purely on the textual relationship
    /// between the raw query and the title. Frecency/provider signals are intentionally
    /// not considered here - they only affect the within-tier score.
    /// </summary>
    /// <param name="query">The raw query text.</param>
    /// <param name="title">The item's title.</param>
    /// <param name="isFallback">Whether the item is a fallback (always ranked at the floor).</param>
    /// <param name="isAliasExact">Whether the query exactly equals the item's alias.</param>
    /// <param name="isAliasSubstringMatch">Whether the item's alias starts with the query
    /// (a partial alias match). An alias is an explicit, user-assigned shortcut and may be
    /// intentionally unrelated to the title, so a partial alias match floors the tier to at
    /// least <see cref="RankTier.Fuzzy"/> even when no lexical signal matched - otherwise
    /// such items would be classified <see cref="RankTier.None"/> and silently dropped.</param>
    /// <param name="matchedLexically">Whether any fuzzy signal (title/subtitle/extension) matched.</param>
    public static RankTier ClassifyTier(
        string query,
        string title,
        bool isFallback,
        bool isAliasExact,
        bool isAliasSubstringMatch,
        bool matchedLexically)
    {
        if (isAliasExact)
        {
            return RankTier.AliasExact;
        }

        // Fallbacks always live at the floor so dynamic matches (e.g. RDP hosts) appear
        // after direct command and app matches.
        if (isFallback)
        {
            return RankTier.FallbackFloor;
        }

        // A partial alias match is enough to keep the item visible even when nothing else
        // matched. It only floors to Fuzzy; a stronger title relationship below still wins.
        var matchedOrAlias = matchedLexically || isAliasSubstringMatch;

        var q = query.AsSpan().Trim();
        if (q.IsEmpty || string.IsNullOrEmpty(title))
        {
            return matchedOrAlias ? RankTier.Fuzzy : RankTier.None;
        }

        var titleSpan = title.AsSpan();

        // Ordinal (not culture-aware) comparisons keep ranking deterministic across locales
        // - e.g. the Turkish dotted/dotless-I would otherwise change prefix/acronym results
        // - and are faster on this per-item, per-keystroke path. The fuzzy matcher already
        // handles looser linguistic matching; these tier boundaries are intentionally crisp.
        if (titleSpan.Equals(q, StringComparison.OrdinalIgnoreCase))
        {
            return RankTier.ExactTitle;
        }

        if (titleSpan.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            return RankTier.Prefix;
        }

        if (MatchesWordBoundaryOrAcronym(title, q))
        {
            return RankTier.AcronymWordBoundary;
        }

        return matchedOrAlias ? RankTier.Fuzzy : RankTier.None;
    }

    /// <summary>
    /// Composes the within-tier score from normalized signals. Lexical quality dominates;
    /// frecency, the alias-substring nudge, and the extension (provider) bonus only
    /// reorder items that already share a tier.
    /// </summary>
    public static double WithinTierScore(
        double lexicalQuality,
        double frecencyWeight,
        double aliasSubstringBonus,
        double providerBonus)
    {
        return (lexicalQuality * LexicalScale)
            + (frecencyWeight * FrecencyScale)
            + aliasSubstringBonus
            + providerBonus;
    }

    /// <summary>
    /// Returns true when the query matches the start of any word in the title, or matches
    /// the acronym formed by the title's word-initials (e.g. "vs" -> "Visual Studio").
    /// </summary>
    internal static bool MatchesWordBoundaryOrAcronym(string title, ReadOnlySpan<char> query)
    {
        if (query.IsEmpty || string.IsNullOrEmpty(title))
        {
            return false;
        }

        // initials holds at most one char per title character. Use the stack for typical
        // short titles and fall back to the heap only for unusually long ones.
        const int StackLimit = 64;
        Span<char> initials = title.Length <= StackLimit
            ? stackalloc char[StackLimit]
            : new char[title.Length];
        var initialsLen = 0;

        for (var i = 0; i < title.Length; i++)
        {
            if (IsWordStart(title, i))
            {
                // Word-boundary: does a word start with the whole query?
                if (title.AsSpan(i).StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                initials[initialsLen++] = title[i];
            }
        }

        // Acronym: the query appears as a contiguous run of word-initials. Require length
        // >= 2 so single characters are handled solely by the word-boundary check above.
        if (query.Length >= 2 && initialsLen >= query.Length)
        {
            var initialsSpan = initials[..initialsLen];
            if (initialsSpan.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordStart(string s, int i)
    {
        if (i == 0)
        {
            return char.IsLetterOrDigit(s[i]);
        }

        var prev = s[i - 1];
        var cur = s[i];

        // Start of a new word after a separator.
        if (!char.IsLetterOrDigit(prev) && char.IsLetterOrDigit(cur))
        {
            return true;
        }

        // camelCase / PascalCase boundary: lower/digit -> upper.
        if (char.IsUpper(cur) && (char.IsLower(prev) || char.IsDigit(prev)))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Relevance tiers for main/root page ranking, from worst (lowest value) to best
/// (highest value). Ranking is <b>lexicographic</b>: an item in a higher tier always
/// sorts above an item in a lower tier. Signals such as frecency and per-provider
/// weighting only reorder items <i>within</i> the same tier - they can never promote an
/// item across a tier boundary. This is what keeps ordering predictable ("an exact match
/// always beats a fuzzy one").
/// </summary>
public enum RankTier
{
    /// <summary>No match. The item should be filtered out.</summary>
    None = 0,

    /// <summary>The item only matched because it is an always-present fallback, or a
    /// fallback whose dynamic title matched. Fallbacks live at the floor so they appear
    /// after direct command/app matches.</summary>
    FallbackFloor = 1,

    /// <summary>The query matched as a fuzzy subsequence of the title, subtitle, or
    /// extension name.</summary>
    Fuzzy = 2,

    /// <summary>The query matched the start of a word inside the title, or the acronym
    /// formed by the title's word-initials (e.g. "vs" -> "Visual Studio").</summary>
    AcronymWordBoundary = 3,

    /// <summary>The title starts with the query.</summary>
    Prefix = 4,

    /// <summary>The title equals the query (case-insensitive).</summary>
    ExactTitle = 5,

    /// <summary>The query exactly equals a user-assigned alias. This is the strongest,
    /// most explicit signal of intent.</summary>
    AliasExact = 6,
}
