// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// Fallback platform used until the UI layer registers a window-aware one (and
/// in unit-test-free code paths). It can open the browser but cannot re-foreground
/// the window, so <see cref="BringToForeground"/> is a no-op here.
/// </summary>
public sealed partial class DefaultAuthBrokerPlatform : IAuthBrokerPlatform
{
    public void LaunchBrowser(Uri authorizationUri)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        _ = global::Windows.System.Launcher.LaunchUriAsync(authorizationUri);
    }

    public void BringToForeground()
    {
        // The UI layer registers a platform that can foreground the window; the
        // fallback intentionally does nothing.
    }
}
