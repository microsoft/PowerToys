// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class SystemCommandsCache
{
    public SystemCommandsCache(SettingsManager manager)
    {
        var list = new List<IListItem>();
        var listLock = new object();

        var a = Task.Run(() =>
        {
            var isBootedInUefiMode = Win32Helpers.GetSystemFirmwareType() == FirmwareType.Uefi;

            var separateEmptyRB = manager.HideEmptyRecycleBin;
            var confirmSystemCommands = manager.ShowDialogToConfirmCommand;
            var showSuccessOnEmptyRB = manager.ShowSuccessMessageAfterEmptyingRecycleBin;

            // normal system commands are fast and can be returned immediately
            var systemCommands = Commands.GetSystemCommands(isBootedInUefiMode, separateEmptyRB, confirmSystemCommands, showSuccessOnEmptyRB);
            lock (listLock)
            {
                list.AddRange(systemCommands);
            }
        });

        var b = Task.Run(() =>
        {
            // Network (ip and mac) results are slow with many network cards and returned delayed.
            // On global queries the first word/part has to be 'ip', 'mac' or 'address' for network results
            var networkConnectionResults = Commands.GetNetworkConnectionResults(manager);
            lock (listLock)
            {
                list.AddRange(networkConnectionResults);
            }
        });

        Task.WaitAll(a, b);
        CachedCommands = list.ToArray();
    }

    public IListItem[] CachedCommands { get; }
}
