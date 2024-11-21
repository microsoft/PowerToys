// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Registry.Classes;
using Microsoft.CmdPal.Ext.Registry.Helpers;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Registry;

internal sealed partial class RegistryListPage : DynamicListPage
{
    private readonly string _defaultIconPath;

    public RegistryListPage()
    {
        Icon = new("\uE74C"); // OEM
        Name = "Windows Registry";
        Id = "com.microsoft.cmdpal.registry";
        _defaultIconPath = "Images/reg.light.png";
    }

    public List<ListItem> Query(string query)
    {
        if (query is null)
        {
            return new List<ListItem>(0);
        }

        var searchForValueName = QueryHelper.GetQueryParts(query, out var queryKey, out var queryValueName);

        var (baseKeyList, subKey) = RegistryHelper.GetRegistryBaseKey(queryKey);
        if (baseKeyList is null)
        {
            // no base key found
            return ResultHelper.GetResultList(RegistryHelper.GetAllBaseKeys(), _defaultIconPath);
        }
        else if (baseKeyList.Count() == 1)
        {
            // only one base key was found -> start search for the sub-key
            var list = RegistryHelper.SearchForSubKey(baseKeyList.First(), subKey);

            // when only one sub-key was found and a user search for values ("\\")
            // show the filtered list of values of one sub-key
            return searchForValueName && list.Count == 1
                ? ResultHelper.GetValuesFromKey(list.First().Key, _defaultIconPath, queryValueName)
                : ResultHelper.GetResultList(list, _defaultIconPath);
        }
        else if (baseKeyList.Count() > 1)
        {
            // more than one base key was found -> show results
            return ResultHelper.GetResultList(baseKeyList.Select(found => new RegistryEntry(found)), _defaultIconPath);
        }

        return new List<ListItem>();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        return Query(SearchText).ToArray();
    }
}
