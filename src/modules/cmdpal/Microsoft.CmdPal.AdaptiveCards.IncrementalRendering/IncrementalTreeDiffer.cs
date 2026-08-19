// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Produces conservative in-place update plans for ordered logical trees. Any structural ambiguity or
/// property marked <see cref="IncrementalPropertyBehavior.ReplaceRoot"/> selects a root replacement.
/// </summary>
public static class IncrementalTreeDiffer
{
    public static IncrementalUpdatePlan CreatePlan(
        IncrementalNodeSnapshot current,
        IncrementalNodeSnapshot candidate,
        long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidate);

        if (HasDuplicateStableIds(current) || HasDuplicateStableIds(candidate))
        {
            return Replace(expectedVersion, "A tree contains duplicate stable IDs.");
        }

        var updates = new List<IncrementalPropertyUpdate>();
        var reason = CompareNodes(current, candidate, updates);
        if (reason is not null)
        {
            return Replace(expectedVersion, reason);
        }

        return new IncrementalUpdatePlan(
            expectedVersion,
            updates.Count == 0 ? IncrementalPlanDisposition.NoChanges : IncrementalPlanDisposition.PatchInPlace,
            updates,
            null);
    }

    private static string? CompareNodes(
        IncrementalNodeSnapshot current,
        IncrementalNodeSnapshot candidate,
        List<IncrementalPropertyUpdate> updates)
    {
        if (!string.Equals(current.Path, candidate.Path, StringComparison.Ordinal))
        {
            return $"Node path changed from '{current.Path}' to '{candidate.Path}'.";
        }

        if (!string.Equals(current.Type, candidate.Type, StringComparison.Ordinal))
        {
            return $"Node '{current.Path}' changed type.";
        }

        if (!string.Equals(current.StableId, candidate.StableId, StringComparison.Ordinal))
        {
            return $"Node '{current.Path}' changed stable ID.";
        }

        if (current.Properties.Count != candidate.Properties.Count)
        {
            return $"Node '{current.Path}' changed its property schema.";
        }

        for (var i = 0; i < current.Properties.Count; i++)
        {
            var oldProperty = current.Properties[i];
            var newProperty = candidate.Properties[i];
            if (!string.Equals(oldProperty.Name, newProperty.Name, StringComparison.Ordinal)
                || oldProperty.Behavior != newProperty.Behavior)
            {
                return $"Node '{current.Path}' changed its property schema.";
            }

            if (oldProperty.Value == newProperty.Value)
            {
                continue;
            }

            if (oldProperty.Behavior == IncrementalPropertyBehavior.ReplaceRoot)
            {
                return $"Property '{oldProperty.Name}' on node '{current.Path}' requires root replacement.";
            }

            updates.Add(new IncrementalPropertyUpdate(
                current.Path,
                current.Type,
                oldProperty.Name,
                oldProperty.Value,
                newProperty.Value));
        }

        if (current.Children.Count != candidate.Children.Count)
        {
            return $"Node '{current.Path}' changed child count.";
        }

        for (var i = 0; i < current.Children.Count; i++)
        {
            var reason = CompareNodes(current.Children[i], candidate.Children[i], updates);
            if (reason is not null)
            {
                return reason;
            }
        }

        return null;
    }

    private static bool HasDuplicateStableIds(IncrementalNodeSnapshot root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<IncrementalNodeSnapshot>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.StableId is not null && !ids.Add(node.StableId))
            {
                return true;
            }

            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }

        return false;
    }

    private static IncrementalUpdatePlan Replace(long expectedVersion, string reason) => new(
        expectedVersion,
        IncrementalPlanDisposition.ReplaceRoot,
        Array.Empty<IncrementalPropertyUpdate>(),
        reason);
}