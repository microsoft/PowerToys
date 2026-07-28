// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Selector used to locate elements via winappcli. winappcli has its own selector grammar
/// (semantic slugs, plain text search) so this type maps onto the CLI's argument shape
/// rather than mimicking Selenium's <c>By</c>.
/// </summary>
public sealed class By
{
    public enum Kind
    {
        /// <summary>Plain-text search against Name or AutomationId (case-insensitive substring).</summary>
        Text,

        /// <summary>Stable AutomationId, when the developer set one.</summary>
        AutomationId,

        /// <summary>A semantic slug (e.g., <c>btn-close-d1a0</c>) printed by <c>inspect</c>/<c>search</c>.</summary>
        Slug,
    }

    public Kind Selector { get; }

    public string Value { get; }

    private By(Kind kind, string value)
    {
        Selector = kind;
        Value = value;
    }

    /// <summary>Plain-text search; what you'd type into <c>winapp ui search "&lt;text&gt;"</c>.</summary>
    public static By Name(string name) => new(Kind.Text, name);

    /// <summary>Look up by stable AutomationId (winappcli also accepts these as selectors).</summary>
    public static By AccessibilityId(string id) => new(Kind.AutomationId, id);

    /// <inheritdoc cref="AccessibilityId(string)"/>
    public static By Id(string id) => new(Kind.AutomationId, id);

    /// <summary>Direct slug selector (e.g., <c>btn-colorpicker-b415</c>) as printed by inspect/search.</summary>
    public static By Slug(string slug) => new(Kind.Slug, slug);

    public override string ToString() => $"{Selector}={Value}";
}
