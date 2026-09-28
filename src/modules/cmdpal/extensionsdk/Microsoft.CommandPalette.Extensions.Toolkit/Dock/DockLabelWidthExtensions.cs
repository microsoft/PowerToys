// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Helpers for reserving and limiting Dock label space on Toolkit items with extended attributes.
/// </summary>
/// <remarks>
/// Custom providers must return a persistent, writable property bag from <c>GetProperties()</c>.
/// Each call updates both attributes before notifying <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/>.
/// Unchanged values do not notify. Other attributes are preserved.
/// </remarks>
public static class DockLabelWidthExtensions
{
    /// <param name="item">The item whose Dock label width to configure.</param>
    /// <typeparam name="TItem">The item's concrete type.</typeparam>
    extension<TItem>(TItem item)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        /// <summary>
        /// Reserves the larger enabled row's width, clamped by any shared limits.
        /// Compact mode excludes the subtitle. Each row uses its own font and text scale.
        /// </summary>
        /// <param name="titleWidth">The title reservation, or null to remove it.</param>
        /// <param name="subtitleWidth">The subtitle reservation, or null to remove it.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelReservations(DockLabelWidth? titleWidth, DockLabelWidth? subtitleWidth) =>
            SetWidths(item, WellKnownExtensionAttributes.DockTitleWidth, titleWidth, WellKnownExtensionAttributes.DockSubtitleWidth, subtitleWidth);

        /// <summary>
        /// Removes both row reservations, preserving shared limits.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        public TItem ClearDockLabelReservations() => item.SetDockLabelReservations(null, null);

        /// <summary>
        /// Sets shared label limits, excluding the icon and padding. Font-relative limits use the title font.
        /// Limits clamp row reservations; without reservations they bound the content's natural width.
        /// The host ignores both limits if the resolved minimum exceeds the maximum.
        /// </summary>
        /// <param name="minimum">The minimum width, or null to remove it.</param>
        /// <param name="maximum">The maximum width, or null to remove it.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelWidthLimits(DockLabelWidth? minimum, DockLabelWidth? maximum) =>
            SetWidths(item, WellKnownExtensionAttributes.DockMinLabelWidth, minimum, WellKnownExtensionAttributes.DockMaxLabelWidth, maximum);

        /// <summary>
        /// Removes both shared limits, preserving row reservations.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        public TItem ClearDockLabelWidthLimits() => item.SetDockLabelWidthLimits(null, null);
    }

    private static TItem SetWidths<TItem>(TItem item, string firstKey, DockLabelWidth? firstWidth, string secondKey, DockLabelWidth? secondWidth)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        ArgumentNullException.ThrowIfNull(item);
        var properties = ((IExtendedAttributesProvider)item).GetProperties();
        if (properties is null || properties.IsReadOnly)
        {
            throw new InvalidOperationException("The item's extended attributes must be a writable property bag.");
        }

        var firstChanged = SetWidth(properties, firstKey, firstWidth?.Value);
        var secondChanged = SetWidth(properties, secondKey, secondWidth?.Value);
        if (firstChanged || secondChanged)
        {
            item.NotifyDockLabelWidthChanged();
        }

        return item;
    }

    private static bool SetWidth(IDictionary<string, object> properties, string key, object? value)
    {
        if (value is null)
        {
            return properties.Remove(key);
        }

        if (properties.TryGetValue(key, out var previous) && Equals(previous, value))
        {
            return false;
        }

        properties[key] = value;
        return true;
    }
}
