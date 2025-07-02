// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.CmdPal.Ext.Registry.Classes;
using Microsoft.CmdPal.Ext.Registry.Commands;
using Microsoft.CmdPal.Ext.Registry.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

/// <summary>
/// Helper class to easier work with context menu entries
/// </summary>
internal static class ContextMenuHelper
{
    /// <summary>
    /// Return a list with all context menu entries for the given <see cref="Result"/>
    /// <para>Symbols taken from <see href="https://learn.microsoft.com/windows/uwp/design/style/segoe-ui-symbol-font"/></para>
    /// </summary>
    internal static List<CommandContextItem> GetContextMenu(RegistryEntry entry)
    {
        var list = new List<CommandContextItem>();

        if (string.IsNullOrEmpty(entry.ValueName))
        {
            list.Add(new CommandContextItem(new CopyRegistryInfoCommand(entry, CopyType.Key)));
        }
        else
        {
            list.Add(new CommandContextItem(new CopyRegistryInfoCommand(entry, CopyType.ValueData)));
            list.Add(new CommandContextItem(new CopyRegistryInfoCommand(entry, CopyType.ValueName)));
        }

        // list.Add(new CommandContextItem(new OpenKeyInEditorCommand(entry)));
        return list;
    }
}
