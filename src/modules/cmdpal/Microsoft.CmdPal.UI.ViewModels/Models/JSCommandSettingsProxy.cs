// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Exposes a Node.js extension's settings page as <see cref="ICommandSettings"/>.
/// The settings page id is resolved through <c>provider/getSettings</c> and
/// presented as a content page.
/// </summary>
internal sealed partial class JSCommandSettingsProxy : ICommandSettings
{
    private readonly string _settingsPageId;
    private readonly JsonRpcConnection _connection;

    public JSCommandSettingsProxy(string settingsPageId, JsonRpcConnection connection)
    {
        _settingsPageId = settingsPageId;
        _connection = connection;
    }

    public IContentPage SettingsPage => new JSContentPageProxy(_settingsPageId, _connection);
}
