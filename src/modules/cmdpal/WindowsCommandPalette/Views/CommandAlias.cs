// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.Ext.Calc;
using Microsoft.CmdPal.Ext.Registry;
using Microsoft.CmdPal.Ext.Settings;
using Microsoft.CmdPal.Ext.WindowsServices;
using Microsoft.CmdPal.Ext.WindowsSettings;
using Microsoft.CmdPal.Ext.WindowsTerminal;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;
using WindowsCommandPalette.BuiltinCommands;
using WindowsCommandPalette.Models;

namespace WindowsCommandPalette.Views;

public class CommandAlias(string shortcut, string commandId, bool direct = false)
{
    public string CommandId { get; set; } = commandId;

    public string Alias { get; set; } = shortcut;

    public bool IsDirect { get; set; } = direct;

    public string SearchPrefix => Alias + (IsDirect ? string.Empty : " ");
}
