// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Represents an immutable canonicalizable source item with one payload and complete provenance.
/// </summary>
internal sealed class AppCatalogItem
{
    /// <summary>Initializes a catalog item loaded from or written to the catalog cache.</summary>
    [JsonConstructor]
    public AppCatalogItem(
        string identity,
        AppCatalogProvenance provenance,
        ImmutableArray<string> matchTerms,
        IAppCatalogPayload payload)
        : this(identity, provenance, payload, NormalizeMatchTerms(matchTerms))
    {
    }

    /// <summary>Initializes an item contributed by one application source.</summary>
    public AppCatalogItem(
        string identity,
        int priority,
        AppCatalogSourceReference sourceReference,
        IReadOnlyList<string> matchTerms,
        IAppCatalogPayload payload)
        : this(
            identity,
            new AppCatalogProvenance(priority, sourceReference),
            payload,
            NormalizeMatchTerms(matchTerms))
    {
    }

    private AppCatalogItem(
        string identity,
        AppCatalogProvenance provenance,
        IAppCatalogPayload payload,
        ImmutableArray<string> normalizedMatchTerms)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        MatchTerms = normalizedMatchTerms;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    /// <summary>Gets the stable canonical identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the selected representation and all contributing source references.</summary>
    public AppCatalogProvenance Provenance { get; }

    /// <summary>Gets the sorted source aliases and metadata retained across deduplication.</summary>
    public ImmutableArray<string> MatchTerms { get; }

    /// <summary>Gets the exactly-one immutable application payload.</summary>
    public IAppCatalogPayload Payload { get; }

    /// <summary>Materializes the consumer-facing application payload.</summary>
    public AppItem ToAppItem()
    {
        var app = Payload.ToAppItem();
        app.MatchTerms = MatchTerms;
        return app;
    }

    /// <summary>Merges overlapping representations while preserving a deterministic preferred payload.</summary>
    public AppCatalogItem MergeProvenance(AppCatalogItem other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!string.Equals(Identity, other.Identity, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only catalog items with the same identity can be merged.", nameof(other));
        }

        var preferenceComparison = Provenance.ComparePreferenceTo(other.Provenance);
        if (preferenceComparison == 0 && !Payload.Equals(other.Payload))
        {
            throw new InvalidOperationException(
                "One source representation cannot contribute different payloads to the same catalog snapshot.");
        }

        var preferred = preferenceComparison <= 0 ? this : other;
        return new AppCatalogItem(
            preferred.Identity,
            Provenance.Merge(other.Provenance),
            preferred.Payload,
            MergeMatchTerms(MatchTerms, other.MatchTerms));
    }

    /// <summary>Determines whether two items would write equivalent catalog-cache content.</summary>
    public bool HasSamePersistedContent(AppCatalogItem other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Identity, other.Identity, StringComparison.OrdinalIgnoreCase)
            && Provenance.HasSamePersistedContent(other.Provenance)
            && HasSameMatchTerms(MatchTerms, other.MatchTerms)
            && Payload.Equals(other.Payload);
    }

    /// <summary>Determines whether the existing consumer-facing application can be reused.</summary>
    public bool CanReuseMaterializedApp(AppCatalogItem other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Identity, other.Identity, StringComparison.OrdinalIgnoreCase)
            && Payload.Equals(other.Payload)
            && HasSameMatchTerms(MatchTerms, other.MatchTerms);
    }

    private static bool HasSameMatchTerms(ImmutableArray<string> left, ImmutableArray<string> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<string> NormalizeMatchTerms(ImmutableArray<string> matchTerms)
    {
        if (matchTerms.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var candidates = new List<string>(matchTerms.Length);
        foreach (var term in matchTerms)
        {
            if (!string.IsNullOrWhiteSpace(term))
            {
                candidates.Add(term);
            }
        }

        return NormalizeMatchTerms(candidates);
    }

    private static ImmutableArray<string> NormalizeMatchTerms(IReadOnlyList<string> matchTerms)
    {
        ArgumentNullException.ThrowIfNull(matchTerms);

        var candidates = new List<string>(matchTerms.Count);
        foreach (var term in matchTerms)
        {
            if (!string.IsNullOrWhiteSpace(term))
            {
                candidates.Add(term);
            }
        }

        return NormalizeMatchTerms(candidates);
    }

    private static ImmutableArray<string> NormalizeMatchTerms(List<string> candidates)
    {
        candidates.Sort(CompareMatchTerms);
        var unique = ImmutableArray.CreateBuilder<string>(candidates.Count);
        string? previous = null;
        foreach (var candidate in candidates)
        {
            if (previous is null || !string.Equals(previous, candidate, StringComparison.OrdinalIgnoreCase))
            {
                unique.Add(candidate);
                previous = candidate;
            }
        }

        return unique.ToImmutable();
    }

    private static ImmutableArray<string> MergeMatchTerms(
        ImmutableArray<string> left,
        ImmutableArray<string> right)
    {
        var merged = ImmutableArray.CreateBuilder<string>(left.Length + right.Length);
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftTerm = left[leftIndex];
            var rightTerm = right[rightIndex];
            if (string.Equals(leftTerm, rightTerm, StringComparison.OrdinalIgnoreCase))
            {
                merged.Add(string.Compare(leftTerm, rightTerm, StringComparison.Ordinal) <= 0 ? leftTerm : rightTerm);
                leftIndex++;
                rightIndex++;
            }
            else if (CompareMatchTerms(leftTerm, rightTerm) < 0)
            {
                merged.Add(leftTerm);
                leftIndex++;
            }
            else
            {
                merged.Add(rightTerm);
                rightIndex++;
            }
        }

        while (leftIndex < left.Length)
        {
            merged.Add(left[leftIndex++]);
        }

        while (rightIndex < right.Length)
        {
            merged.Add(right[rightIndex++]);
        }

        return merged.ToImmutable();
    }

    private static int CompareMatchTerms(string left, string right)
    {
        var comparison = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return comparison != 0
            ? comparison
            : string.Compare(left, right, StringComparison.Ordinal);
    }
}
