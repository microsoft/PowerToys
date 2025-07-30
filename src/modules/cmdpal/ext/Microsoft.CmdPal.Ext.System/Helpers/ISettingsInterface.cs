// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.System.Helpers;

public interface ISettingsInterface
{
    public bool ShowDialogToConfirmCommand();

    public bool ShowSuccessMessageAfterEmptyingRecycleBin();

    public bool HideEmptyRecycleBin();

    public bool HideDisconnectedNetworkInfo();

    public FirmwareType GetSystemFirmwareType();
}
