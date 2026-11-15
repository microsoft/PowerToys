// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

internal enum IncrementalPropertyBehavior
{
    PatchInPlace,
    ReplaceRoot,
}

internal sealed class IncrementalPropertySnapshot(
    string name,
    string? value,
    IncrementalPropertyBehavior behavior)
{
    public string Name { get; } = !string.IsNullOrEmpty(name)
        ? name
        : throw new ArgumentException("A property name is required.", nameof(name));

    public string? Value { get; } = value;

    public IncrementalPropertyBehavior Behavior { get; } = behavior;
}

internal sealed class IncrementalNodeSnapshot(
    string type,
    int childCount,
    IReadOnlyList<IncrementalPropertySnapshot>? properties = null)
{
    public string Type { get; } = !string.IsNullOrEmpty(type)
        ? type
        : throw new ArgumentException("A node type is required.", nameof(type));

    public int ChildCount { get; } = childCount >= 0
        ? childCount
        : throw new ArgumentOutOfRangeException(nameof(childCount));

    public IReadOnlyList<IncrementalPropertySnapshot> Properties { get; } =
        properties ?? Array.Empty<IncrementalPropertySnapshot>();
}

internal sealed class IncrementalTreeSnapshot
{
    public IncrementalTreeSnapshot(IReadOnlyList<IncrementalNodeSnapshot> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0)
        {
            throw new ArgumentException("A tree must contain a root node.", nameof(nodes));
        }

        Nodes = nodes;
    }

    public IReadOnlyList<IncrementalNodeSnapshot> Nodes { get; }
}

internal enum IncrementalPlanDisposition
{
    NoChanges,
    PatchInPlace,
    ReplaceRoot,
}

internal sealed class IncrementalPropertyUpdate(
    int nodeIndex,
    string expectedNodeType,
    string propertyName,
    string? expectedOldValue,
    string? newValue)
{
    public int NodeIndex { get; } = nodeIndex;

    public string ExpectedNodeType { get; } = expectedNodeType;

    public string PropertyName { get; } = propertyName;

    public string? ExpectedOldValue { get; } = expectedOldValue;

    public string? NewValue { get; } = newValue;
}

internal sealed class IncrementalUpdatePlan
{
    internal IncrementalUpdatePlan(
        IncrementalPlanDisposition disposition,
        IReadOnlyList<IncrementalPropertyUpdate> propertyUpdates)
    {
        Disposition = disposition;
        PropertyUpdates = propertyUpdates;
    }

    public IncrementalPlanDisposition Disposition { get; }

    public IReadOnlyList<IncrementalPropertyUpdate> PropertyUpdates { get; }
}
