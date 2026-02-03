// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.Core.ViewModels;

internal readonly struct CacheKey : IEquatable<CacheKey>
{
    private enum Kind : byte
    {
        None,
        StableString,
        StableGuid,
        ComIdentity,
        Reference,
    }

    private readonly Kind _kind;
    private readonly nint _ptr;
    private readonly Guid _guid;
    private readonly string? _str;
    private readonly object? _ref;

    private CacheKey(Kind kind, nint ptr = 0, Guid guid = default, string? str = null, object? @ref = null)
    {
        _kind = kind;
        _ptr = ptr;
        _guid = guid;
        _str = str;
        _ref = @ref;
    }

    public static CacheKey Stable(string id) => new(Kind.StableString, str: id);

    public static CacheKey Stable(Guid id) => new(Kind.StableGuid, guid: id);

    // Pointer value of IUnknown identity (we release it immediately; the pointer value is just a key).
    public static CacheKey ComIdentity(nint iunknownIdentityPtr) => new(Kind.ComIdentity, iunknownIdentityPtr);

    // Last-resort: reference identity. Only helps if the same instance is reused by the source.
    public static CacheKey Reference(object instance) => new(Kind.Reference, @ref: instance);

    public bool Equals(CacheKey other)
    {
        if (_kind != other._kind)
        {
            return false;
        }

        return _kind switch
        {
            Kind.StableString => StringComparer.Ordinal.Equals(_str, other._str),
            Kind.StableGuid => _guid.Equals(other._guid),
            Kind.ComIdentity => _ptr == other._ptr,
            Kind.Reference => ReferenceEquals(_ref, other._ref),
            _ => false,
        };
    }

    public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

    public override int GetHashCode()
    {
        return _kind switch
        {
            Kind.StableString => HashCode.Combine((int)_kind, StringComparer.Ordinal.GetHashCode(_str ?? string.Empty)),
            Kind.StableGuid => HashCode.Combine((int)_kind, _guid),
            Kind.ComIdentity => HashCode.Combine((int)_kind, _ptr),
            Kind.Reference => HashCode.Combine((int)_kind, RuntimeHelpers.GetHashCode(_ref!)), // reference hash
            _ => 0,
        };
    }

    public static bool operator ==(CacheKey left, CacheKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CacheKey left, CacheKey right)
    {
        return !left.Equals(right);
    }
}
