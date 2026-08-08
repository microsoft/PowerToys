// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Auth;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Window-aware <see cref="IAuthBrokerPlatform"/>. Opens the system browser and
/// re-foregrounds the Command Palette window (via the existing summon message)
/// once the authorization redirect is captured.
/// </summary>
public sealed partial class WindowAuthBrokerPlatform : IAuthBrokerPlatform
{
    public void LaunchBrowser(Uri authorizationUri)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        _ = global::Windows.System.Launcher.LaunchUriAsync(authorizationUri);
    }

    public void BringToForeground()
    {
        // Reuse the same summon path the tray icon and settings use to bring the
        // window forward without navigating anywhere.
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, IntPtr.Zero));
    }
}
