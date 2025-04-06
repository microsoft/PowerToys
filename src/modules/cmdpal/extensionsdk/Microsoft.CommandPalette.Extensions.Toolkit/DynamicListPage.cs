// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract class DynamicListPage : ListPage, IDynamicListPage
{
    public override string SearchText
    {
        get => base.SearchText;
        set
        {
            var oldSearch = base.SearchText;
            base.SearchText = value;
            UpdateSearchText(oldSearch, value);
        }
    }

    public abstract void UpdateSearchText(string oldSearch, string newSearch);
}
