// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Page : Command, IPage
{
    // NOTE TO MAINTAINERS: Do NOT implement `IExtendedAttributesProvider` here or
    // on the Toolkit page classes. Pages opt in by declaring it, which keeps the
    // host from reading attributes of every page. See the note in `CommandItem`.
    // Most pages publish no attributes, so the property set is created on first use.
    private PropertySet? _extendedAttributes;

    private IContextItem[] _menuCommands = [];

    // The host may read attributes on another thread, so concurrent first uses must publish one set.
    private PropertySet ExtendedAttributes => LazyInitializer.EnsureInitialized(ref _extendedAttributes, static () => new PropertySet());

    public virtual bool IsLoading { get; set => SetProperty(ref field, value); }

    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual OptionalColor AccentColor { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets the entries of the page-title menu, in order. Separators are allowed;
    /// an empty array removes the menu.
    /// </summary>
    /// <remarks>
    /// Command Palette reads the menu only from page classes that declare <see cref="IExtendedAttributesProvider"/>,
    /// which <see cref="GetProperties"/> then implements; Toolkit pages don't declare it.
    /// Command Palette recognizes that interface only when the extension's command provider returns an
    /// <see cref="IExtendedAttributesProvider"/> stub from <see cref="ICommandProvider2.GetApiExtensionStubs"/>;
    /// <see cref="CommandProvider"/> already does. Without either, the menu silently doesn't appear.
    /// Each change notifies <see cref="WellKnownExtensionAttributes.PageMenuCommands"/>. Editing the array
    /// after assigning it has no effect until the property is set again.
    /// Page-wide shortcuts yield to selected-item commands and host navigation and text input keys.
    /// </remarks>
    public IContextItem[] MenuCommands
    {
        get => _menuCommands;
        set
        {
            value ??= [];
            var unchanged = _extendedAttributes is { } attributes && attributes.TryGetValue(WellKnownExtensionAttributes.PageMenuCommands, out var current)
                ? current is IEnumerable<object> currentCommands && currentCommands.SequenceEqual(value, ReferenceEqualityComparer.Instance)
                : value.Length == 0;
            _menuCommands = value;
            if (unchanged)
            {
                return;
            }

            if (value.Length == 0)
            {
                ExtendedAttributes.Remove(WellKnownExtensionAttributes.PageMenuCommands);
            }
            else
            {
                // Box command objects explicitly so the property set stores them as an inspectable array.
                ExtendedAttributes[WellKnownExtensionAttributes.PageMenuCommands] = PropertyValue.CreateInspectableArray(value);
            }

            OnPropertyChanged(WellKnownExtensionAttributes.PageMenuCommands);
        }
    }

    /// <summary>
    /// Gets the page's extended attributes. Declare <see cref="IExtendedAttributesProvider"/> on a page class to publish them.
    /// </summary>
    /// <returns>The page's persistent, writable property bag.</returns>
    public IDictionary<string, object> GetProperties() => ExtendedAttributes;
}
