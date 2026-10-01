// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UITests;

internal static class AdvancedPasteUi
{
    internal static T CardControl<T>(Session session, string cardId, string controlType, string? className = null, string? automationId = null)
        where T : Element, new()
    {
        string[] excludedSubtreeIds = cardId switch
        {
            "PasteAsFile" => ["PasteAsTxtFile", "PasteAsPngFile", "PasteAsHtmlFile"],
            "Transcode" => ["TranscodeToMp3", "TranscodeToMp4"],
            _ => [],
        };
        return Child<T>(
            session,
            () => Card(session, cardId),
            node => Property(node, "type") == controlType &&
                (className is null || Property(node, "className") == className) &&
                (automationId is null || Property(node, "automationId") == automationId),
            $"{controlType} {automationId ?? className} in {cardId}",
            excludedSubtreeIds);
    }

    internal static Element Card(Session session, string cardId)
    {
        var matches = session.FindAll<Element>(By.AccessibilityId(cardId), 10_000)
            .Where(element => element.AutomationId == cardId).ToArray();
        Assert.HasCount(1, matches, $"Expected one Settings card with AutomationId '{cardId}'.");
        return matches[0];
    }

    internal static T Child<T>(Session session, Func<Element> parent, Func<JsonElement, bool> matches, string description, string[]? excludedSubtreeIds = null)
        where T : Element, new()
    {
        // Element.Find currently searches the whole session. Resolve the actual subtree instead
        // of choosing a different card's EditButton or a different history entry's More options.
        var result = WaitHelper.WaitForStable(
            () =>
            {
                var container = parent();
                var tree = WinappCli.InvokeJson("ui", "inspect", container.Selector, session.TargetFlag, session.TargetValue, "--json", "-d", "16");
                return Nodes(tree, excludedSubtreeIds).Where(matches).ToArray();
            },
            nodes => nodes is { Length: 1 },
            timeoutMS: 15_000,
            shouldRetryException: IsStaleElement);
        Assert.IsTrue(result.Succeeded, $"Expected one {description}; last matching count: {result.LastObservation?.Length}.");
        var selector = Property(result.LastObservation![0], "selector");
        Assert.IsFalse(string.IsNullOrEmpty(selector), $"The {description} UIA node did not expose a selector.");
        return session.Find<T>(By.Slug(selector));
    }

    internal static string Property(JsonElement node, string property) =>
        node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : string.Empty;

    internal static bool IsStaleElement(Exception exception) =>
        exception is AssertFailedException && exception.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonElement> Nodes(JsonElement root, string[]? excludedSubtreeIds = null)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            var element = root.TryGetProperty("type", out _);
            if (element)
            {
                if (excludedSubtreeIds?.Contains(Property(root, "automationId"), StringComparer.Ordinal) == true)
                {
                    yield break;
                }

                yield return root;
            }

            foreach (var property in root.EnumerateObject())
            {
                foreach (var child in Nodes(property.Value, excludedSubtreeIds))
                {
                    yield return child;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var child in Nodes(item, excludedSubtreeIds))
                {
                    yield return child;
                }
            }
        }
    }
}
