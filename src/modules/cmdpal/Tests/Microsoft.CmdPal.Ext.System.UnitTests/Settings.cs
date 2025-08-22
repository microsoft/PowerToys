// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.System.Helpers;

namespace Microsoft.CmdPal.Ext.System.UnitTests;

public class Settings : ISettingsInterface
{
    private bool hideDisconnectedNetworkInfo;
    private bool hideEmptyRecycleBin;
    private bool showDialogToConfirmCommand;
    private bool showSuccessMessageAfterEmptyingRecycleBin;
    private FirmwareType firmwareType;

    public Settings(bool hideDisconnectedNetworkInfo = false, bool hideEmptyRecycleBin = false, bool showDialogToConfirmCommand = false, bool showSuccessMessageAfterEmptyingRecycleBin = false, FirmwareType firmwareType = FirmwareType.Uefi)
    {
        this.hideDisconnectedNetworkInfo = hideDisconnectedNetworkInfo;
        this.hideEmptyRecycleBin = hideEmptyRecycleBin;
        this.showDialogToConfirmCommand = showDialogToConfirmCommand;
        this.showSuccessMessageAfterEmptyingRecycleBin = showSuccessMessageAfterEmptyingRecycleBin;
        this.firmwareType = firmwareType;
    }

    public bool HideDisconnectedNetworkInfo() => hideDisconnectedNetworkInfo;

    public bool HideEmptyRecycleBin() => hideEmptyRecycleBin;

    public bool ShowDialogToConfirmCommand() => showDialogToConfirmCommand;

    public bool ShowSuccessMessageAfterEmptyingRecycleBin() => showSuccessMessageAfterEmptyingRecycleBin;

    public FirmwareType GetSystemFirmwareType() => firmwareType;
}
