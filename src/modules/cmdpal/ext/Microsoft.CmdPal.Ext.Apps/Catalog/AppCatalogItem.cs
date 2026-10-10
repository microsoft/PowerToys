// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Represents an immutable canonicalizable source item with one payload and complete provenance.
/// </summary>
internal sealed class AppCatalogItem
{
    /// <summary>Gets the stable canonical identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the selected representation and all contributing source references.</summary>
    public AppCatalogProvenance Provenance { get; }

    /// <summary>Gets the sorted source aliases and metadata retained across deduplication.</summary>
    public ImmutableArray<string> MatchTerms { get; }

    /// <summary>Gets the persisted command IDs of all contributing representations.</summary>
    public ImmutableArray<string> CommandIds { get; }

    /// <summary>Gets contributing catalog identities retained for existing visibility preferences.</summary>
    public ImmutableArray<string> IdentityAliases { get; }

    /// <summary>Gets the exactly-one immutable application payload.</summary>
    public IAppCatalogPayload Payload { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogItem"/> class. Initializes a catalog item loaded from or written to the catalog cache.</summary>
    [JsonConstructor]
    public AppCatalogItem(
        string identity,
        AppCatalogProvenance provenance,
        ImmutableArray<string> matchTerms,
        IAppCatalogPayload payload,
        ImmutableArray<string> commandIds = default,
        ImmutableArray<string> identityAliases = default)
        : this(
            identity,
            provenance,
            payload,
            NormalizeMatchTerms(matchTerms),
            (commandIds.IsDefault ? [] : commandIds)
                .Append((payload ?? throw new ArgumentNullException(nameof(payload))).GetCommandId())
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            NormalizeIdentityAliases(identity, identityAliases.IsDefault ? [] : identityAliases))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogItem"/> class. Initializes an item contributed by one application source.</summary>
    public AppCatalogItem(
        string identity,
        int priority,
        AppCatalogSourceReference sourceReference,
        IReadOnlyList<string> matchTerms,
        IAppCatalogPayload payload,
        ImmutableArray<string> identityAliases = default)
        : this(
            identity,
            new AppCatalogProvenance(priority, sourceReference),
            payload,
            NormalizeMatchTerms(matchTerms),
            [(payload ?? throw new ArgumentNullException(nameof(payload))).GetCommandId()],
            NormalizeIdentityAliases(identity, identityAliases.IsDefault ? [] : identityAliases))
    {
    }

    private AppCatalogItem(
        string identity,
        AppCatalogProvenance provenance,
        IAppCatalogPayload payload,
        ImmutableArray<string> normalizedMatchTerms,
        ImmutableArray<string> commandIds,
        ImmutableArray<string> identityAliases)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(payload);

        Identity = identity;
        Provenance = provenance;
        MatchTerms = normalizedMatchTerms;
        Payload = payload;
        CommandIds = commandIds;
        IdentityAliases = identityAliases;
    }

    /// <summary>Materializes the consumer-facing application payload.</summary>
    public AppItem ToAppItem()
    {
        var app = Payload.ToAppItem();
        app.CatalogId = Identity;
        app.MatchTerms = MatchTerms;
        app.ExecutableSourcePaths = Provenance.References
            .Select(reference => reference.ItemId)
            .Where(path => Path.IsPathFullyQualified(path) && PathHelpers.IsExecutablePath(path))
            .ToArray();
        app.CommandIds = CommandIds.Concat(IdentityAliases.SelectMany(identity => AppIdentity.GetCommandIds(identity))).Distinct(StringComparer.Ordinal).ToArray();
        return app;
    }

    /// <summary>
    /// Returns this item under a canonical identity.
    /// </summary>
    /// <param name="identity">The identity shared by equivalent representations.</param>
    /// <returns>This instance when the identity already matches; otherwise, an immutable reidentified copy.</returns>
    internal AppCatalogItem WithIdentity(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        return string.Equals(Identity, identity, StringComparison.OrdinalIgnoreCase)
            ? this
            : new AppCatalogItem(
                identity,
                Provenance,
                Payload,
                MatchTerms,
                CommandIds,
                NormalizeIdentityAliases(identity, IdentityAliases));
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
            MergeMatchTerms(MatchTerms, other.MatchTerms),
            CommandIds.Union(other.CommandIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            NormalizeIdentityAliases(Identity, IdentityAliases.Concat(other.IdentityAliases)));
    }

    /// <summary>Compares source snapshots by persisted item content, independent of item order.</summary>
    internal static bool HaveSamePersistedContent(
        IReadOnlyList<AppCatalogItem> left,
        IReadOnlyList<AppCatalogItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var leftById = new Dictionary<string, AppCatalogItem>(left.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var item in left)
        {
            if (!leftById.TryAdd(item.Identity, item))
            {
                return false;
            }
        }

        foreach (var item in right)
        {
            if (!leftById.Remove(item.Identity, out var leftItem)
                || !leftItem.HasSamePersistedContent(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether two items would write equivalent catalog-cache content.</summary>
    public bool HasSamePersistedContent(AppCatalogItem other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Identity, other.Identity, StringComparison.OrdinalIgnoreCase)
            && Provenance.HasSamePersistedContent(other.Provenance)
            && HasSameMatchTerms(MatchTerms, other.MatchTerms)
            && CommandIds.SequenceEqual(other.CommandIds, StringComparer.Ordinal)
            && IdentityAliases.SequenceEqual(other.IdentityAliases, StringComparer.OrdinalIgnoreCase)
            && Payload.Equals(other.Payload);
    }

    /// <summary>Determines whether the existing consumer-facing application can be reused.</summary>
    public bool CanReuseMaterializedApp(AppCatalogItem other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Identity, other.Identity, StringComparison.OrdinalIgnoreCase)
            && Payload.Equals(other.Payload)
            && CommandIds.SequenceEqual(other.CommandIds, StringComparer.Ordinal)
            && IdentityAliases.SequenceEqual(other.IdentityAliases, StringComparer.OrdinalIgnoreCase)
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

    private static ImmutableArray<string> NormalizeIdentityAliases(string identity, IEnumerable<string> aliases)
    {
        return aliases.Append(identity)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
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
