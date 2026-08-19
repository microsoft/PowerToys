// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>Controls how a changed property is reconciled.</summary>
public enum IncrementalPropertyBehavior
{
    PatchInPlace,
    ReplaceRoot,
}

/// <summary>The scalar kinds supported by an incremental tree snapshot.</summary>
public enum IncrementalValueKind
{
    Null,
    Boolean,
    Integer,
    Double,
    String,
}

/// <summary>
/// An immutable scalar value used by the XAML-neutral diff engine. Arbitrary objects are deliberately
/// excluded so update plans can be compared without retaining dependency objects.
/// </summary>
public readonly struct IncrementalValue : IEquatable<IncrementalValue>
{
    private readonly bool _booleanValue;
    private readonly long _integerValue;
    private readonly double _doubleValue;
    private readonly string? _stringValue;

    private IncrementalValue(IncrementalValueKind kind, bool booleanValue, long integerValue, double doubleValue, string? stringValue)
    {
        Kind = kind;
        _booleanValue = booleanValue;
        _integerValue = integerValue;
        _doubleValue = doubleValue;
        _stringValue = stringValue;
    }

    public IncrementalValueKind Kind { get; }

    public static IncrementalValue Null => new(IncrementalValueKind.Null, false, 0, 0, null);

    public static IncrementalValue FromBoolean(bool value) => new(IncrementalValueKind.Boolean, value, 0, 0, null);

    public static IncrementalValue FromInteger(long value) => new(IncrementalValueKind.Integer, false, value, 0, null);

    public static IncrementalValue FromDouble(double value) => new(IncrementalValueKind.Double, false, 0, value, null);

    public static IncrementalValue FromString(string? value) => value is null
        ? Null
        : new IncrementalValue(IncrementalValueKind.String, false, 0, 0, value);

    public bool GetBoolean() => Kind == IncrementalValueKind.Boolean
        ? _booleanValue
        : throw new InvalidOperationException("The incremental value is not a Boolean.");

    public long GetInteger() => Kind == IncrementalValueKind.Integer
        ? _integerValue
        : throw new InvalidOperationException("The incremental value is not an Integer.");

    public double GetDouble() => Kind == IncrementalValueKind.Double
        ? _doubleValue
        : throw new InvalidOperationException("The incremental value is not a Double.");

    public string? GetString() => Kind is IncrementalValueKind.String or IncrementalValueKind.Null
        ? _stringValue
        : throw new InvalidOperationException("The incremental value is not a String.");

    public bool Equals(IncrementalValue other)
    {
        if (Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            IncrementalValueKind.Null => true,
            IncrementalValueKind.Boolean => _booleanValue == other._booleanValue,
            IncrementalValueKind.Integer => _integerValue == other._integerValue,
            IncrementalValueKind.Double => _doubleValue.Equals(other._doubleValue),
            IncrementalValueKind.String => string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal),
            _ => false,
        };
    }

    public override bool Equals(object? obj) => obj is IncrementalValue other && Equals(other);

    public override int GetHashCode() => Kind switch
    {
        IncrementalValueKind.Null => HashCode.Combine(Kind),
        IncrementalValueKind.Boolean => HashCode.Combine(Kind, _booleanValue),
        IncrementalValueKind.Integer => HashCode.Combine(Kind, _integerValue),
        IncrementalValueKind.Double => HashCode.Combine(Kind, _doubleValue),
        IncrementalValueKind.String => HashCode.Combine(Kind, _stringValue),
        _ => 0,
    };

    public static bool operator ==(IncrementalValue left, IncrementalValue right) => left.Equals(right);

    public static bool operator !=(IncrementalValue left, IncrementalValue right) => !left.Equals(right);
}

/// <summary>An immutable, named property in a node snapshot.</summary>
public sealed class IncrementalPropertySnapshot(string name, IncrementalValue value, IncrementalPropertyBehavior behavior)
{
    public string Name { get; } = !string.IsNullOrEmpty(name) ? name : throw new ArgumentException("A property name is required.", nameof(name));

    public IncrementalValue Value { get; } = value;

    public IncrementalPropertyBehavior Behavior { get; } = behavior;
}

/// <summary>An immutable node in an ordered logical tree.</summary>
public sealed class IncrementalNodeSnapshot
{
    public IncrementalNodeSnapshot(
        string path,
        string type,
        string? stableId,
        IReadOnlyList<IncrementalPropertySnapshot>? properties = null,
        IReadOnlyList<IncrementalNodeSnapshot>? children = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(type);
        Path = path;
        Type = type;
        StableId = string.IsNullOrEmpty(stableId) ? null : stableId;
        Properties = properties ?? Array.Empty<IncrementalPropertySnapshot>();
        Children = children ?? Array.Empty<IncrementalNodeSnapshot>();
    }

    public string Path { get; }

    public string Type { get; }

    public string? StableId { get; }

    public IReadOnlyList<IncrementalPropertySnapshot> Properties { get; }

    public IReadOnlyList<IncrementalNodeSnapshot> Children { get; }
}

/// <summary>The overall action selected by the diff engine.</summary>
public enum IncrementalPlanDisposition
{
    NoChanges,
    PatchInPlace,
    ReplaceRoot,
}

/// <summary>A validated scalar property update for a retained node.</summary>
public sealed class IncrementalPropertyUpdate(
    string nodePath,
    string expectedNodeType,
    string propertyName,
    IncrementalValue expectedOldValue,
    IncrementalValue newValue)
{
    public string NodePath { get; } = nodePath;

    public string ExpectedNodeType { get; } = expectedNodeType;

    public string PropertyName { get; } = propertyName;

    public IncrementalValue ExpectedOldValue { get; } = expectedOldValue;

    public IncrementalValue NewValue { get; } = newValue;
}

/// <summary>An immutable update plan produced from two snapshots.</summary>
public sealed class IncrementalUpdatePlan
{
    internal IncrementalUpdatePlan(
        long expectedVersion,
        IncrementalPlanDisposition disposition,
        IReadOnlyList<IncrementalPropertyUpdate> propertyUpdates,
        string? fallbackReason)
    {
        ExpectedVersion = expectedVersion;
        Disposition = disposition;
        PropertyUpdates = propertyUpdates;
        FallbackReason = fallbackReason;
    }

    public long ExpectedVersion { get; }

    public IncrementalPlanDisposition Disposition { get; }

    public IReadOnlyList<IncrementalPropertyUpdate> PropertyUpdates { get; }

    public string? FallbackReason { get; }
}