// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Owns the normalization and comparison rules of an application launch target.</summary>
internal readonly record struct LaunchTarget
{
    private readonly string? _value;

    public string Value => _value ?? string.Empty;

    public LaunchTargetKind Kind { get; }

    /// <summary>Preserves sensitive URL bytes in catalog keys that use case-insensitive comparison.</summary>
    public string IdentityToken => Kind == LaunchTargetKind.Url
        ? $"url:{Convert.ToHexString(Encoding.UTF8.GetBytes(Value))}"
        : Value;

    private StringComparer Comparer => Kind == LaunchTargetKind.Url ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private LaunchTarget(string value, LaunchTargetKind kind)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        Kind = kind;
    }

    /// <summary>Creates a normalized filesystem target with case-insensitive identity comparisons.</summary>
    public static LaunchTarget FilePath(string value)
    {
        return new(PathHelpers.NormalizePath(value), LaunchTargetKind.FilePath);
    }

    /// <summary>Creates a URL target that preserves its spelling and compares case-sensitively.</summary>
    public static LaunchTarget Url(string value)
    {
        return new(value, LaunchTargetKind.Url);
    }

    /// <summary>Compares target kinds and values using the case rules of that target kind.</summary>
    public bool Equals(LaunchTarget other)
    {
        return Kind == other.Kind && Comparer.Equals(Value, other.Value);
    }

    /// <summary>Hashes the target kind and value using the same case rules as equality.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, Comparer.GetHashCode(Value));
    }
}

internal enum LaunchTargetKind
{
    FilePath,
    Url,
}
