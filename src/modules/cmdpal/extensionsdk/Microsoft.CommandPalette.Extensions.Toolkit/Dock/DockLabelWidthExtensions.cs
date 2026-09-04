// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Helpers for reserving Dock label space on Toolkit items with extended attributes.
/// </summary>
/// <remarks>
/// Custom providers must return a persistent, writable property bag from <c>GetProperties()</c>.
/// </remarks>
public static class DockLabelWidthExtensions
{
    /// <param name="item">The item whose Dock label width to set.</param>
    /// <typeparam name="TItem">The item's concrete type.</typeparam>
    extension<TItem>(TItem item)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        /// <summary>
        /// Sets equal minimum and maximum Dock label width hints in DIPs.
        /// Updates both hints before raising a single <c>PropChanged</c> notification for
        /// <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/>.
        /// Reapplying the same hints does not raise a notification. The host ignores invalid widths.
        /// </summary>
        /// <param name="width">The label width in DIPs, stored as a <see cref="double"/>.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelWidth(double width) => SetDockLabelWidthCore(item, width);

        /// <summary>
        /// Sets equal minimum and maximum Dock label width hints using a unit string.
        /// Updates both hints before raising a single <c>PropChanged</c> notification for
        /// <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/>.
        /// Reapplying the same hints does not raise a notification. The host ignores invalid widths.
        /// </summary>
        /// <param name="width">An invariant length such as <c>"12ch"</c> or <c>"1200sqh"</c>.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> or <paramref name="width"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelWidth(string width)
        {
            ArgumentNullException.ThrowIfNull(width);
            return SetDockLabelWidthCore(item, width);
        }

        /// <summary>
        /// Removes both Dock label width hints, restoring the host's default sizing.
        /// Raises one <c>PropChanged</c> notification for
        /// <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/> if either hint was present.
        /// Other extended attributes are preserved.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem ClearDockLabelWidth()
        {
            var properties = GetWritableProperties(item);
            var removedMinimum = properties.Remove(WellKnownExtensionAttributes.DockMinLabelWidth);
            var removedMaximum = properties.Remove(WellKnownExtensionAttributes.DockMaxLabelWidth);
            if (removedMinimum || removedMaximum)
            {
                item.NotifyDockLabelWidthChanged();
            }

            return item;
        }
    }

    private static TItem SetDockLabelWidthCore<TItem>(TItem item, object width)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        var properties = GetWritableProperties(item);
        if (properties.TryGetValue(WellKnownExtensionAttributes.DockMinLabelWidth, out var minimum) &&
            Equals(minimum, width) &&
            properties.TryGetValue(WellKnownExtensionAttributes.DockMaxLabelWidth, out var maximum) &&
            Equals(maximum, width))
        {
            return item;
        }

        properties[WellKnownExtensionAttributes.DockMinLabelWidth] = width;
        properties[WellKnownExtensionAttributes.DockMaxLabelWidth] = width;
        item.NotifyDockLabelWidthChanged();
        return item;
    }

    private static IDictionary<string, object> GetWritableProperties(IExtendedAttributesProvider item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var properties = item.GetProperties();
        if (properties is null || properties.IsReadOnly)
        {
            throw new InvalidOperationException("The item's extended attributes must be a writable property bag.");
        }

        return properties;
    }
}
