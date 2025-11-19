// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.PowerToys.Helper;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.Classes;

internal sealed class PowerToysModuleEntry
{
    public required SettingsWindow Module { get; set; }

    public void NavigateToSettingsPage()
    {
        var moduleKey = Module.ModuleKey();
        if (PowerToysRpcClient.TryInvoke(moduleKey, "navigateToSettings"))
        {
            return;
        }

        OpenSettings(Module);
    }
}
