// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

internal static class IncrementalTreeDiffer
{
    public static IncrementalUpdatePlan CreatePlan(
        IncrementalTreeSnapshot current,
        IncrementalTreeSnapshot candidate)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidate);

        if (current.Nodes.Count != candidate.Nodes.Count)
        {
            return Replace();
        }

        var updates = new List<IncrementalPropertyUpdate>();
        for (var nodeIndex = 0; nodeIndex < current.Nodes.Count; nodeIndex++)
        {
            var currentNode = current.Nodes[nodeIndex];
            var candidateNode = candidate.Nodes[nodeIndex];
            if (!string.Equals(currentNode.Type, candidateNode.Type, StringComparison.Ordinal)
                || currentNode.ChildCount != candidateNode.ChildCount
                || currentNode.Properties.Count != candidateNode.Properties.Count)
            {
                return Replace();
            }

            for (var propertyIndex = 0; propertyIndex < currentNode.Properties.Count; propertyIndex++)
            {
                var currentProperty = currentNode.Properties[propertyIndex];
                var candidateProperty = candidateNode.Properties[propertyIndex];
                if (!string.Equals(currentProperty.Name, candidateProperty.Name, StringComparison.Ordinal)
                    || currentProperty.Behavior != candidateProperty.Behavior)
                {
                    return Replace();
                }

                if (string.Equals(currentProperty.Value, candidateProperty.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                if (currentProperty.Behavior == IncrementalPropertyBehavior.ReplaceRoot)
                {
                    return Replace();
                }

                updates.Add(new IncrementalPropertyUpdate(
                    nodeIndex,
                    currentNode.Type,
                    currentProperty.Name,
                    currentProperty.Value,
                    candidateProperty.Value));
            }
        }

        return new IncrementalUpdatePlan(
            updates.Count == 0
                ? IncrementalPlanDisposition.NoChanges
                : IncrementalPlanDisposition.PatchInPlace,
            updates);
    }

    private static IncrementalUpdatePlan Replace() => new(
        IncrementalPlanDisposition.ReplaceRoot,
        Array.Empty<IncrementalPropertyUpdate>());
}
