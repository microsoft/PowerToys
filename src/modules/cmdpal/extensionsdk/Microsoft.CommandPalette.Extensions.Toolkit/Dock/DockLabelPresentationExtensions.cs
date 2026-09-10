// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Helpers for presenting changing Dock labels within their reserved space.
/// </summary>
/// <remarks>
/// Custom providers must return a persistent, writable property bag from <c>GetProperties()</c>.
/// </remarks>
public static class DockLabelPresentationExtensions
{
    /// <param name="item">The item whose Dock label presentation to configure.</param>
    /// <typeparam name="TItem">The item's concrete type.</typeparam>
    extension<TItem>(TItem item)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        /// <summary>
        /// Opts the Dock label into tabular digits. The extension remains responsible for decimal precision.
        /// </summary>
        /// <param name="enabled">Whether tabular digits are enabled.</param>
        /// <returns>The same item, for fluent construction.</returns>
        public TItem SetDockLabelTabularDigits(bool enabled = true) =>
            SetHint(item, WellKnownExtensionAttributes.DockLabelTabularDigits, enabled, static target => target.NotifyDockLabelTabularDigitsChanged());

        /// <summary>
        /// Removes the Dock tabular-digits hint.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        public TItem ClearDockLabelTabularDigits() =>
            ClearHint(item, WellKnownExtensionAttributes.DockLabelTabularDigits, static target => target.NotifyDockLabelTabularDigitsChanged());

        /// <summary>
        /// Opts the Dock label into trailing-edge alignment, independently of numeral styling.
        /// </summary>
        /// <param name="enabled">Whether trailing-edge alignment is enabled.</param>
        /// <returns>The same item, for fluent construction.</returns>
        public TItem SetDockLabelTrailingAlignment(bool enabled = true) =>
            SetHint(item, WellKnownExtensionAttributes.DockLabelTrailingAlignment, enabled, static target => target.NotifyDockLabelTrailingAlignmentChanged());

        /// <summary>
        /// Removes the Dock trailing-alignment hint.
        /// </summary>
        /// <returns>The same item, for fluent construction.</returns>
        public TItem ClearDockLabelTrailingAlignment() =>
            ClearHint(item, WellKnownExtensionAttributes.DockLabelTrailingAlignment, static target => target.NotifyDockLabelTrailingAlignmentChanged());
    }

    private static TItem SetHint<TItem>(TItem item, string key, bool enabled, Action<TItem> notify)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        if (!enabled)
        {
            return ClearHint(item, key, notify);
        }

        var properties = GetWritableProperties(item);
        if (properties.TryGetValue(key, out var current) && current is true)
        {
            return item;
        }

        properties[key] = true;
        notify(item);
        return item;
    }

    private static TItem ClearHint<TItem>(TItem item, string key, Action<TItem> notify)
        where TItem : CommandItem, IExtendedAttributesProvider
    {
        var properties = GetWritableProperties(item);
        if (properties.Remove(key))
        {
            notify(item);
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
