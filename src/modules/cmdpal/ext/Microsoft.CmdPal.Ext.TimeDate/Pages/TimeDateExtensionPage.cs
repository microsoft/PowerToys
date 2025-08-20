// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

internal sealed partial class TimeDateExtensionPage : DynamicListPage
{
    private readonly Lock _resultsLock = new();

    private IList<ListItem> _results = new List<ListItem>();
    private bool _dataLoaded;

    private ISettingsInterface _settingsManager;

    public TimeDateExtensionPage(ISettingsInterface settingsManager)
    {
        Icon = Icons.TimeDateExtIcon;
        Title = Resources.Microsoft_plugin_timedate_main_page_title;
        Name = Resources.Microsoft_plugin_timedate_main_page_name;
        PlaceholderText = Resources.Microsoft_plugin_timedate_placeholder_text;
        Id = "com.microsoft.cmdpal.timedate";
        _settingsManager = settingsManager;
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        ListItem[] results;
        lock (_resultsLock)
        {
            if (_dataLoaded)
            {
                results = _results.ToArray();
                _dataLoaded = false;
                return results;
            }
        }

        DoExecuteSearch(string.Empty);

        lock (_resultsLock)
        {
            results = _results.ToArray();
            _dataLoaded = false;
            return results;
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        DoExecuteSearch(newSearch);
    }

    private void DoExecuteSearch(string query)
    {
        try
        {
            var result = TimeDateCalculator.ExecuteSearch(_settingsManager, query);
            UpdateResult(result);
        }
        catch (Exception)
        {
            // sometimes, user's input may not correct.
            // In most of the time, user may not have completed their input.
            // So, we need to clean the result.
            // But in that time, empty result may cause exception.
            // So, we need to add at least on item to user.
            var items = new List<ListItem>
            {
                ResultHelper.CreateInvalidInputErrorResult(),
            };

            UpdateResult(items);
        }
    }

    private void UpdateResult(IList<ListItem> result)
    {
        lock (_resultsLock)
        {
            this._results = result;
            _dataLoaded = true;
        }

        RaiseItemsChanged(this._results.Count);
    }
}
