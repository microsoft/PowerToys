// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Proxy that implements ICommandSettings for JavaScript extensions by wrapping
/// a JSContentPageProxy for the settings page. The settings page is fetched via
/// the <c>provider/getSettings</c> JSON-RPC method and then relies on existing
/// <c>contentPage/getContent</c> and <c>form/submit</c> handlers for interaction.
/// </summary>
internal sealed class JSCommandSettingsProxy : ICommandSettings
{
    private readonly JSContentPageProxy _settingsPage;

    public JSCommandSettingsProxy(string settingsPageId, JsonRpcConnection connection)
    {
        _settingsPage = new JSContentPageProxy(settingsPageId, connection);
    }

    public IContentPage SettingsPage => _settingsPage;
}
