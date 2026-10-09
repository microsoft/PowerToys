// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

internal sealed class Icons
{
    internal static IconInfo WindowsSettingsIcon { get; } = IconHelpers.FromRelativePath("Assets\\WindowsSettings.svg");

    internal static IconInfo CopyIcon { get; } = new IconInfo("\xE8C8"); // Copy icon
}
