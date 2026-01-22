// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed class Icons
{
    internal static IconInfo TimeDateExtIcon { get; } = IconHelpers.FromRelativePath("Assets\\TimeDate.svg");

    internal static IconInfo TimeIcon { get; } = new IconInfo("\uE823");

    internal static IconInfo CalendarIcon { get; } = new IconInfo("\uE787");

    internal static IconInfo TimeDateIcon { get; } = new IconInfo("\uEC92");

    internal static IconInfo ErrorIcon { get; } = IconHelpers.FromRelativePaths("Microsoft.CmdPal.Ext.TimeDate\\Assets\\Warning.light.png", "Microsoft.CmdPal.Ext.TimeDate\\Assets\\Warning.dark.png");
}
