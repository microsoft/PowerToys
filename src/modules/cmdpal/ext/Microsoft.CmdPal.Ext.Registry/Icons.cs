// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Registry;

internal sealed class Icons
{
    internal static IconInfo RegistryIcon { get; } = IconHelpers.FromRelativePath("Assets\\Registry.svg");

    internal static IconInfo OpenInNewWindowIcon { get; } = new IconInfo("\xE8A7"); // OpenInNewWindow icon

    internal static IconInfo CopyIcon { get; } = new IconInfo("\xE8C8"); // Copy icon

    internal static IconInfo CopyToIcon { get; } = new IconInfo("\xF413"); // CopyTo Icon
}
