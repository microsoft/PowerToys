// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Windowing;

namespace SamplePagesExtension;

internal sealed partial class SampleDynamicListPage : DynamicListPage
{
    public SampleDynamicListPage()
    {
        Icon = new(string.Empty);
        Name = "Dynamic List";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(newSearch.Length);
    }

    public override IListItem[] GetItems()
    {
        var items = SearchText.ToCharArray().Select(ch => new ListItem(new NoOpCommand()) { Title = ch.ToString() }).ToArray();
        if (items.Length == 0)
        {
            items = [new ListItem(new NoOpCommand()) { Title = "Start typing in the search box" }];
        }

        if (items.Length > 0)
        {
            items[0].Subtitle = "Notice how the number of items changes for this page when you type in the filter box";
        }

        return items;
    }
}
