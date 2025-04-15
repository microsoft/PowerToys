// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Commands;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Helpers;

/// <summary>
/// Helper class to easier work with context menu entries
/// </summary>
internal static class ContextMenuHelper
{
    internal static List<CommandContextItem> GetContextMenu(WindowsSetting entry)
    {
        var list = new List<CommandContextItem>(1)
        {
            new(new CopySettingCommand(entry)),
        };

        return list;
    }
}
