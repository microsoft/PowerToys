// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract class DynamicListPage : ListPage, IDynamicListPage
{
    protected virtual string LastSearchText { get; private set; } = string.Empty;

    protected virtual string InitialSearchText { get; set; } = string.Empty;

    private string? _changedTextValue;

    public override string SearchText
    {
        get
        {
            if (_changedTextValue == null)
            {
                return InitialSearchText;
            }
            else
            {
                var result = _changedTextValue;
                _changedTextValue = null;
                return result;
            }
        }

        set
        {
            var oldSearch = LastSearchText;
            LastSearchText = value;
            UpdateSearchText(oldSearch, value);
        }
    }

    public abstract void UpdateSearchText(string oldSearch, string newSearch);

    public void ChangeSearchText(string newText)
    {
        _changedTextValue = newText;
        SearchText = newText;
        OnPropertyChanged(nameof(SearchText));
    }
}
