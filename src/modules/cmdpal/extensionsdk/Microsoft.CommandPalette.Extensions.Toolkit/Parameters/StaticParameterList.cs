// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// A helper class for parameters that display a static list of values. The list
/// of values is provided at construction time, and cannot be changed after
/// that. This is useful for parameters that have a fixed set of possible
/// values, such as an enum parameter.
///
/// Here's an example of how to use this class for an enum parameter:
///
/// <code>
/// var parameter = new StaticParameterList&lt;MyEnum&gt;(
///     Enum.GetValues(typeof(MyEnum)).Cast&lt;MyEnum&gt;(),
///     (value, listItem) =&gt; {
///         listItem.Title = value.ToString();
///         return listItem;
///     });
/// parameter.ValueSelected += (s, MyEnum value) =&gt; {
///     // Do something with the selected value
/// };
/// </code>
///
// </summary>
public partial class StaticParameterList<T> : ListPage
{
    public event TypedEventHandler<object, T>? ValueSelected;

    private readonly IEnumerable<T> _values;
    private readonly List<IListItem> _items = new();
    private readonly Lock _initializeLock = new();
    private bool _isInitialized;
    private Func<T, ListItem, ListItem> _customizeListItemsCallback;

    // ctor takes an IEnumerable<T> values, and a function to customize the ListItem's depending on the value
    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = (value, listItem) =>
        {
            return customizeListItem(value);
        };
    }

    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = customizeListItem;
    }

    public override IListItem[] GetItems()
    {
        lock (_initializeLock)
        {
            if (!_isInitialized)
            {
                Initialize(_values, _customizeListItemsCallback);
                _isInitialized = true;
            }

            return _items.ToArray();
        }
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
