// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Registry.Classes;
using Microsoft.CmdPal.Ext.Registry.Helpers;
using Microsoft.CmdPal.Ext.Registry.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.Registry.Commands;

internal sealed partial class OpenKeyInEditorCommand : InvokableCommand
{
    private readonly RegistryEntry _entry;

    internal OpenKeyInEditorCommand(RegistryEntry entry)
    {
        Name = Resources.OpenKeyInRegistryEditor;
        Icon = new IconInfo("\xE8A7"); // OpenInNewWindow icon
        _entry = entry;
    }

    internal static bool TryToOpenInRegistryEditor(in RegistryEntry entry)
    {
        try
        {
            RegistryHelper.OpenRegistryKey(entry.Key?.Name ?? entry.KeyPath);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // TODO GH #118 We need a convenient way to show errors to a user
            // MessageBox.Show(
            //    Resources.OpenInRegistryEditorAccessExceptionText,
            //    Resources.OpenInRegistryEditorAccessExceptionTitle,
            //    MessageBoxButton.OK,
            //    MessageBoxImage.Error);
            return false;
        }
#pragma warning disable CS0168, IDE0059
        catch (Exception exception)
        {
            // TODO GH #108: Logging
            // Log.Exception("Error on opening Windows registry editor", exception, typeof(Main));
            return false;
        }
#pragma warning restore CS0168, IDE0059
    }

    public override CommandResult Invoke()
    {
        TryToOpenInRegistryEditor(_entry);

        return CommandResult.KeepOpen();
    }
}
