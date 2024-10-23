// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public partial class TerminalTopLevelListItem : ListItem
{
    public TerminalTopLevelListItem()
        : base(new ProfilesListPage())
    {
        Title = "Open WT Profiles";
        Subtitle = "Windows Terminal";
    }
}
