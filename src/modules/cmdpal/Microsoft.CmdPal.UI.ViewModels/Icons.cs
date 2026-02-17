// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class Icons
{
    public static IconInfo PinIcon => new("\uE718"); // Pin icon

    public static IconInfo UnpinIcon => new("\uE77A"); // Unpin icon

    public static IconInfo SettingsIcon => new("\uE713"); // Settings icon

    public static IconInfo EditIcon => new("\uE70F"); // Edit icon
}
