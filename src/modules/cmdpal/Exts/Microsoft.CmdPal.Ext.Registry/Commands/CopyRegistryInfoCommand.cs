// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Registry.Classes;
using Microsoft.CmdPal.Ext.Registry.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.Registry.Commands;

internal sealed partial class CopyRegistryInfoCommand : InvokableCommand
{
    private readonly RegistryEntry _entry;
    private readonly string _stringToCopy;

    internal CopyRegistryInfoCommand(RegistryEntry entry, CopyType typeToCopy)
    {
        if (typeToCopy == CopyType.Key)
        {
            Name = Resources.CopyKeyNamePath;
            Icon = new("\xE8C8"); // Copy Icon
            _stringToCopy = entry.GetRegistryKey();
        }
        else if (typeToCopy == CopyType.ValueData)
        {
            Name = Resources.CopyValueData;
            Icon = new("\xF413"); // CopyTo Icon
            _stringToCopy = entry.GetValueData();
        }
        else if (typeToCopy == CopyType.ValueName)
        {
            Name = Resources.CopyValueName;
            Icon = new("\xE8C8"); // Copy Icon
            _stringToCopy = entry.GetValueNameWithKey();
        }

        _entry = entry;
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetText(_stringToCopy);

        return CommandResult.KeepOpen();
    }
}
