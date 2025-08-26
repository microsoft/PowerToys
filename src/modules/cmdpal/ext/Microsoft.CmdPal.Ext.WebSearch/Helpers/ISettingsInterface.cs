// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public interface ISettingsInterface
{
    public bool GlobalIfURI { get; }

    public string ShowHistory { get; }

    public List<ListItem> LoadHistory();

    public void SaveHistory(HistoryItem historyItem);
}
