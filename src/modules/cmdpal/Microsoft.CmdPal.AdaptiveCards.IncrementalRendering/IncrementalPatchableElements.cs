// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Lists the custom element types whose rendered controls implement
/// <see cref="IIncrementalAdaptiveElementControl"/> and can be patched in place.
/// </summary>
/// <remarks>
/// For a registered element, every property except the host-owned ones is patchable. The
/// Adaptive Cards renderer applies host-owned properties (layout, visibility, fallback, and
/// actions) outside the custom control, so changing them always replaces the complete card.
/// The exception is the fallback of an element without <c>requires</c>: a registered element
/// always renders itself, so that fallback is never drawn and its changes are ignored. Register
/// only element types that the host's renderer renders.
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

    private readonly HashSet<string> _elementTypes = new(StringComparer.Ordinal);

    /// <summary>Registers an element type as patchable.</summary>
    /// <returns>This instance, so registrations can be chained.</returns>
    public IncrementalPatchableElements Add(string elementType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementType);
        _elementTypes.Add(elementType);
        return this;
    }

    /// <summary>
    /// Returns whether <paramref name="propertyName"/> is applied by the custom control rather than
    /// by the Adaptive Cards renderer.
    /// </summary>
    public static bool IsPatchableProperty(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) && !HostOwnedProperties.Contains(propertyName);

    internal bool IsEmpty => _elementTypes.Count == 0;

    internal bool Contains(string? elementType) =>
        elementType is not null && _elementTypes.Contains(elementType);
}
