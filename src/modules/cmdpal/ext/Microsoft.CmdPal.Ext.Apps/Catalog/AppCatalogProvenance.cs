// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Describes every source representation of an application and the representation whose payload won.
/// </summary>
/// <remarks>
/// The preferred source remains attached to its priority and payload when provenance is merged. This makes
/// representation selection independent of merge order while still retaining all contributing references.
/// </remarks>
internal sealed class AppCatalogProvenance
{
    /// <summary>Gets the representation preference; lower values are preferred.</summary>
    public int Priority { get; }

    /// <summary>Gets the source reference associated with the selected payload.</summary>
    public AppCatalogSourceReference PreferredSource { get; }

    /// <summary>Gets the sorted, unique references for every contributing representation.</summary>
    public ImmutableArray<AppCatalogSourceReference> References { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogProvenance"/> class. Initializes immutable provenance loaded from or written to the catalog cache.</summary>
    [JsonConstructor]
    public AppCatalogProvenance(
        int priority,
        AppCatalogSourceReference preferredSource,
        ImmutableArray<AppCatalogSourceReference> references)
        : this(priority, NormalizeReferences(preferredSource, references), preferredSource)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogProvenance"/> class. Initializes provenance for one source representation.</summary>
    public AppCatalogProvenance(int priority, AppCatalogSourceReference sourceReference)
        : this(priority, ImmutableArray.Create(sourceReference), sourceReference)
    {
    }

    private AppCatalogProvenance(
        int priority,
        ImmutableArray<AppCatalogSourceReference> normalizedReferences,
        AppCatalogSourceReference preferredSource)
    {
        ArgumentNullException.ThrowIfNull(preferredSource);

        Priority = priority;
        References = normalizedReferences;
        PreferredSource = FindEquivalentReference(References, preferredSource);
    }

    /// <summary>Merges reference sets while retaining the preferred representation.</summary>
    public AppCatalogProvenance Merge(AppCatalogProvenance other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var preferred = ComparePreferenceTo(other) <= 0 ? this : other;
        return new AppCatalogProvenance(
            preferred.Priority,
            MergeReferences(References, other.References),
            preferred.PreferredSource);
    }

    /// <summary>Compares the source representation keys used to choose a payload.</summary>
    public int ComparePreferenceTo(AppCatalogProvenance other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var comparison = Priority.CompareTo(other.Priority);
        if (comparison != 0)
        {
            return comparison;
        }

        if (PreferredSource.Equals(other.PreferredSource))
        {
            return 0;
        }

        return PreferredSource.CompareTo(other.PreferredSource);
    }

    /// <summary>Determines whether persisted provenance has the same logical content.</summary>
    public bool HasSamePersistedContent(AppCatalogProvenance other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Priority != other.Priority
            || !PreferredSource.Equals(other.PreferredSource)
            || References.Length != other.References.Length)
        {
            return false;
        }

        for (var index = 0; index < References.Length; index++)
        {
            if (!References[index].Equals(other.References[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<AppCatalogSourceReference> NormalizeReferences(
        AppCatalogSourceReference preferredSource,
        ImmutableArray<AppCatalogSourceReference> references)
    {
        ArgumentNullException.ThrowIfNull(preferredSource);

        var candidates = new List<AppCatalogSourceReference>(references.IsDefault ? 1 : references.Length + 1);
        if (!references.IsDefault)
        {
            foreach (var reference in references)
            {
                if (reference is not null)
                {
                    candidates.Add(reference);
                }
            }
        }

        candidates.Add(preferredSource);
        candidates.Sort(static (left, right) => left.CompareTo(right));

        var unique = ImmutableArray.CreateBuilder<AppCatalogSourceReference>(candidates.Count);
        AppCatalogSourceReference? previous = null;
        foreach (var candidate in candidates)
        {
            if (previous is null || !previous.Equals(candidate))
            {
                unique.Add(candidate);
                previous = candidate;
            }
        }

        return unique.ToImmutable();
    }

    private static ImmutableArray<AppCatalogSourceReference> MergeReferences(
        ImmutableArray<AppCatalogSourceReference> left,
        ImmutableArray<AppCatalogSourceReference> right)
    {
        var merged = ImmutableArray.CreateBuilder<AppCatalogSourceReference>(left.Length + right.Length);
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftReference = left[leftIndex];
            var rightReference = right[rightIndex];
            if (leftReference.Equals(rightReference))
            {
                merged.Add(leftReference.CompareTo(rightReference) <= 0 ? leftReference : rightReference);
                leftIndex++;
                rightIndex++;
            }
            else if (leftReference.CompareTo(rightReference) < 0)
            {
                merged.Add(leftReference);
                leftIndex++;
            }
            else
            {
                merged.Add(rightReference);
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

    private static AppCatalogSourceReference FindEquivalentReference(
        ImmutableArray<AppCatalogSourceReference> references,
        AppCatalogSourceReference reference)
    {
        foreach (var candidate in references)
        {
            if (candidate.Equals(reference))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Preferred source must be present in catalog provenance.");
    }
}
