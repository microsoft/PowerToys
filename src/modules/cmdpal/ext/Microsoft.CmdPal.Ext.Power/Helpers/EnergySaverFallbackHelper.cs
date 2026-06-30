// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static partial class EnergySaverFallbackHelper
{
    internal const string BatterySaverSettingsUri = "ms-settings:batterysaver";
    internal const string PowerSettingsUri = "ms-settings:powersleep";

    internal static bool TryOpenEnergySaverSettings() =>
        ShellHelpers.OpenInShell(BatterySaverSettingsUri)
        || ShellHelpers.OpenInShell(PowerSettingsUri);
}
