// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Provides extension methods for traversing the visual tree.
/// </summary>
internal static class VisualTreeExtensions
{
    /// <summary>
    /// Finds the first descendant of type T (optionally with a specific Name).
    /// Breadth-first to return the nearest match.
    /// </summary>
    public static T? FindDescendant<T>(DependencyObject? root, string? name = null, bool includeSelf = false)
        where T : DependencyObject
    {
        if (root is null)
        {
            return null;
        }

        var queue = new Queue<DependencyObject>();

        if (includeSelf)
        {
            queue.Enqueue(root);
        }
        else
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is not null)
                {
                    queue.Enqueue(child);
                }
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current is T t && NameMatches(current, name))
            {
                return t;
            }

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is not null)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return null;

        static bool NameMatches(DependencyObject d, string? expected)
        {
            if (expected is null)
            {
                return true;
            }

            return d is FrameworkElement fe && fe.Name == expected;
        }
    }

    /// <summary>
    /// Finds the first descendant whose CLR type name matches (optionally with a specific Name).
    /// Useful for internal template types like "TextBoxView".
    /// </summary>
    public static DependencyObject? FindDescendantByTypeName(
        DependencyObject? root,
        string typeName,
        string? name = null,
        bool includeSelf = false)
    {
        if (root is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var queue = new Queue<DependencyObject>();

        if (includeSelf)
        {
            queue.Enqueue(root);
        }
        else
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(root, i));
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (TypeNameMatches(current, typeName) && NameMatches(current, name))
            {
                return current;
            }

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, i));
            }
        }

        return null;

        static bool TypeNameMatches(DependencyObject d, string typeName)
        {
            var t = d.GetType();

            // Match simple name or the tail of FullName to be resilient to namespace differences.
            return string.Equals(t.Name, typeName, StringComparison.Ordinal)
                   || (t.FullName?.EndsWith("." + typeName, StringComparison.Ordinal) ?? false);
        }

        static bool NameMatches(DependencyObject d, string? expected)
        {
            if (expected is null)
            {
                return true;
            }

            return d is FrameworkElement fe && fe.Name == expected;
        }
    }

    /// <summary>
    /// Enumerates all descendants of type T (optionally with a specific Name).
    /// </summary>
    public static IEnumerable<T> FindDescendants<T>(DependencyObject? root, string? name = null, bool includeSelf = false)
        where T : DependencyObject
    {
        if (root is null)
        {
            yield break;
        }

        var queue = new Queue<DependencyObject>();

        if (includeSelf)
        {
            queue.Enqueue(root);
        }
        else
        {
            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(root, i));
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current is T t && (name is null || (current is FrameworkElement fe && fe.Name == name)))
            {
                yield return t;
            }

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, i));
            }
        }
    }
}
