// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// A thin wrapper around <see cref="ImmutableList{T}"/> that provides <em>structural</em>
/// (element-by-element) equality. <see cref="ImmutableList{T}"/> itself only implements
/// reference equality, which means a record holding one compares unequal to an otherwise
/// identical record whenever the list was rebuilt into a fresh instance (e.g. after loading
/// settings from disk).
/// </summary>
/// <remarks>
/// Used as the <em>backing field</em> type for record list properties (the public properties
/// still expose <see cref="ImmutableList{T}"/>). Because the compiler-synthesized record
/// equality compares backing fields, swapping the field type here makes that synthesized
/// equality structural — with no hand-written <c>Equals</c> to keep in sync as new properties
/// are added.
/// </remarks>
internal readonly struct EquatableList<T> : IEquatable<EquatableList<T>>
{
    private readonly ImmutableList<T>? _list;

    public EquatableList(ImmutableList<T>? list) => _list = list;

    public ImmutableList<T> List => _list ?? ImmutableList<T>.Empty;

    public bool Equals(EquatableList<T> other)
    {
        var a = List;
        var b = other.List;

        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < a.Count; i++)
        {
            if (!comparer.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableList<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var item in List)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
