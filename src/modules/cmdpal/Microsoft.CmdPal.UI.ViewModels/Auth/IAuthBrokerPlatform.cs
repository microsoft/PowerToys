// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// The UI-thread coupled pieces of the authorization broker: launching the
/// system browser and re-foregrounding Command Palette once the redirect is
/// captured. Kept behind an interface so the broker's state and lifecycle logic
/// can be unit tested without a real browser or window.
/// </summary>
public interface IAuthBrokerPlatform
{
    /// <summary>Open the default browser to the authorization URL.</summary>
    void LaunchBrowser(Uri authorizationUri);

    /// <summary>Bring the Command Palette window back to the foreground.</summary>
    void BringToForeground();
}
