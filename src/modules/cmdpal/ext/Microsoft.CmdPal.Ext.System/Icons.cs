// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

internal sealed class Icons
{
    internal static IconInfo FirmwareSettingsIcon { get; } = new IconInfo("\uE950");

    internal static IconInfo LockIcon { get; } = new IconInfo("\uE72E");

    internal static IconInfo LogoffIcon { get; } = new IconInfo("\uF3B1");

    internal static IconInfo NetworkAdapterIcon { get; } = new IconInfo("\uEDA3");

    internal static IconInfo RecycleBinIcon { get; } = new IconInfo("\uE74D");

    internal static IconInfo RestartIcon { get; } = new IconInfo("\uE777");

    internal static IconInfo RestartShellIcon { get; } = new IconInfo("\uEC50");

    internal static IconInfo ShutdownIcon { get; } = new IconInfo("\uE7E8");

    internal static IconInfo SleepIcon { get; } = new IconInfo("\uE708");
}
