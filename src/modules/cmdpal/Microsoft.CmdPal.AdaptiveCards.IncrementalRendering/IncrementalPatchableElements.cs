// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Lists the custom element types whose controls implement <see cref="IIncrementalAdaptiveElementControl"/>.
/// </summary>
/// <remarks>
/// Every property of a registered element is patchable except the host-owned ones, which the
/// renderer applies outside the control. Its <c>fallback</c> is never drawn, so it's ignored, unless
/// the element has <c>requires</c> or fails its renders-itself check.
/// </remarks>
public sealed class IncrementalPatchableElements
{
    private static readonly HashSet<string> HostOwnedProperties = new(StringComparer.Ordinal)
    {
        "type",
        "id",
        "isVisible",
        "separator",
        "spacing",
        "height",
        "fallback",
        "requires",
        "targetWidth",
        "horizontalAlignment",
        "grid.area",
        "lang",
        "selectAction",
        "actions",
        "inlineAction",
    };

    private readonly Dictionary<string, Func<JsonElement, bool>?> _elementTypes = new(StringComparer.Ordinal);

    /// <summary>Registers an element type as patchable. Every element of the type renders itself.</summary>
    /// <returns>This instance, so registrations can be chained.</returns>
    public IncrementalPatchableElements Add(string elementType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementType);
        _elementTypes[elementType] = null;
        return this;
    }

    /// <summary>
    /// Registers an element type as patchable. Elements that <paramref name="rendersItself"/> rejects
    /// draw their <c>fallback</c> instead, so they aren't patchable.
    /// </summary>
    /// <param name="elementType">The element's <c>type</c>.</param>
    /// <param name="rendersItself">Gets whether the renderer draws the element JSON itself.</param>
    /// <returns>This instance, so registrations can be chained.</returns>
    public IncrementalPatchableElements Add(string elementType, Func<JsonElement, bool> rendersItself)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementType);
        ArgumentNullException.ThrowIfNull(rendersItself);
        _elementTypes[elementType] = rendersItself;
        return this;
    }

    /// <summary>Returns whether the control, not the renderer, applies <paramref name="propertyName"/>.</summary>
    public static bool IsPatchableProperty(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) && !HostOwnedProperties.Contains(propertyName);

    internal bool IsEmpty => _elementTypes.Count == 0;

    internal bool Contains(string? elementType) =>
        elementType is not null && _elementTypes.ContainsKey(elementType);

    /// <summary>Returns whether <paramref name="element"/> has a registered type and renders itself.</summary>
    internal bool RendersItself(string? elementType, JsonElement element) =>
        elementType is not null
        && _elementTypes.TryGetValue(elementType, out var rendersItself)
        && (rendersItself is null || rendersItself(element));
}
