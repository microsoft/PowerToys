// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class StaticParameterList<T> : ListPage
{
    public event TypedEventHandler<object, T>? ValueSelected;

    private readonly IEnumerable<T> _values;
    private readonly List<IListItem> _items = new();
    private bool _isInitialized;
    private Func<T, ListItem, ListItem> _customizeListItemsCallback;

    // ctor takes an IEnumerable<T> values, and a function to customize the ListItem's depending on the value
    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = (value, listItem) =>
        {
            customizeListItem(value);
            return listItem;
        };
    }

    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = customizeListItem;
    }

    public override IListItem[] GetItems()
    {
        if (!_isInitialized)
        {
            Initialize(_values, _customizeListItemsCallback);
            _isInitialized = true;
        }

        return _items.ToArray();
    }

    private void Initialize(IEnumerable<T> values, Func<T, ListItem, ListItem> customizeListItem)
    {
        foreach (var value in values)
        {
            var command = new SelectParameterCommand<T>(value);
            command.ValueSelected += (s, v) => ValueSelected?.Invoke(this, v);
            var listItem = new ListItem(command);
            var item = customizeListItem(value, listItem);
            _items.Add(item);
        }
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
