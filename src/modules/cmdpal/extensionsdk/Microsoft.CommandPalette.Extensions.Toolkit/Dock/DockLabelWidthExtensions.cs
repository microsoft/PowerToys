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
        public TItem SetDockLabelWidth(double width) => SetDockWidthsCore(item, width, width, perRow: false);

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
            return SetDockWidthsCore(item, width, width, perRow: false);
        }

        /// <summary>
        /// Removes both shared Dock label bounds, preserving any title and subtitle reservations.
        /// Raises one <c>PropChanged</c> notification for
        /// <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/> if either hint was present.
        /// Other extended attributes are preserved.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem ClearDockLabelWidth() => ClearDockWidthsCore(item, perRow: false);

        /// <summary>
        /// Sets fixed title and subtitle reservations in DIPs. The host uses the larger enabled row's width.
        /// Preserves shared bounds and notifies <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/>
        /// once after updating both hints. Reapplying the same hints does not notify.
        /// </summary>
        /// <param name="titleWidth">The title reservation in DIPs, stored as a <see cref="double"/>.</param>
        /// <param name="subtitleWidth">The subtitle reservation in DIPs, stored as a <see cref="double"/>.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelWidths(double titleWidth, double subtitleWidth) => SetDockWidthsCore(item, titleWidth, subtitleWidth, perRow: true);

        /// <summary>
        /// Sets fixed title and subtitle reservations using unit strings. The host uses the larger enabled row's width.
        /// Character units use each row's own font and text scale before widths are compared.
        /// Preserves shared bounds and notifies <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/>
        /// once after updating both hints. Reapplying the same hints does not notify.
        /// </summary>
        /// <param name="titleWidth">An invariant length such as <c>"5ch"</c> or <c>"500sqh"</c>.</param>
        /// <param name="subtitleWidth">An invariant length such as <c>"12ch"</c> or <c>"1200sqh"</c>.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/>, <paramref name="titleWidth"/>, or <paramref name="subtitleWidth"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelWidths(string titleWidth, string subtitleWidth)
        {
            ArgumentNullException.ThrowIfNull(titleWidth);
            ArgumentNullException.ThrowIfNull(subtitleWidth);
            return SetDockWidthsCore(item, titleWidth, subtitleWidth, perRow: true);
        }

        /// <summary>
        /// Removes both row width hints, preserving text samples and shared label bounds.
        /// Notifies <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/> once if either hint was present.
        /// Other extended attributes are preserved.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem ClearDockLabelWidths() => ClearDockWidthsCore(item, perRow: true);

        /// <summary>
        /// Reserves each row's width by measuring literal text in that row's font and text scale.
        /// Samples take precedence over row width hints and are independent of the displayed text.
        /// Preserves width hints and notifies <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/>
        /// once after updating both samples. Reapplying the same samples does not notify.
        /// </summary>
        /// <param name="titleSample">The title sample; null removes it, and an empty string reserves zero width.</param>
        /// <param name="subtitleSample">The subtitle sample; null removes it, and an empty string reserves zero width.</param>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem SetDockLabelWidthSamples(string? titleSample = null, string? subtitleSample = null)
        {
            var properties = GetWritableProperties(item);
            var titleChanged = SetWidthSample(properties, WellKnownExtensionAttributes.DockTitleWidthSample, titleSample);
            var subtitleChanged = SetWidthSample(properties, WellKnownExtensionAttributes.DockSubtitleWidthSample, subtitleSample);
            if (titleChanged || subtitleChanged)
            {
                item.NotifyDockLabelWidthChanged();
            }

            return item;
        }

        /// <summary>
        /// Removes both text samples, preserving row width hints and shared label bounds.
        /// Notifies <see cref="WellKnownExtensionAttributes.DockLabelWidthPropertyName"/> once if either sample was present.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The item does not return writable extended attributes.</exception>
        public TItem ClearDockLabelWidthSamples() => item.SetDockLabelWidthSamples();
    }

    private static bool SetWidthSample(IDictionary<string, object> properties, string key, string? sample)
    {
        if (sample is null)
        {
            return properties.Remove(key);
        }

        if (properties.TryGetValue(key, out var previous) && Equals(previous, sample))
        {
            return false;
        }

        properties[key] = sample;
        return true;
    }

    private static TItem SetDockWidthsCore<TItem>(TItem item, object firstWidth, object secondWidth, bool perRow)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        var properties = GetWritableProperties(item);
        var firstKey = perRow ? WellKnownExtensionAttributes.DockTitleWidth : WellKnownExtensionAttributes.DockMinLabelWidth;
        var secondKey = perRow ? WellKnownExtensionAttributes.DockSubtitleWidth : WellKnownExtensionAttributes.DockMaxLabelWidth;
        if (properties.TryGetValue(firstKey, out var first) &&
            Equals(first, firstWidth) &&
            properties.TryGetValue(secondKey, out var second) &&
            Equals(second, secondWidth))
        {
            return item;
        }

        properties[firstKey] = firstWidth;
        properties[secondKey] = secondWidth;
        item.NotifyDockLabelWidthChanged();
        return item;
    }

    private static TItem ClearDockWidthsCore<TItem>(TItem item, bool perRow)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        var properties = GetWritableProperties(item);
        var removedFirst = properties.Remove(perRow ? WellKnownExtensionAttributes.DockTitleWidth : WellKnownExtensionAttributes.DockMinLabelWidth);
        var removedSecond = properties.Remove(perRow ? WellKnownExtensionAttributes.DockSubtitleWidth : WellKnownExtensionAttributes.DockMaxLabelWidth);
        if (removedFirst || removedSecond)
        {
            item.NotifyDockLabelWidthChanged();
        }

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
