// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.JsonRpc.Models;

/// <summary>
/// Exposes a Node.js extension's settings page as <see cref="ICommandSettings"/>.
/// The full settings page payload is kept intact, including title, name, icon,
/// details, and commands.
/// </summary>
internal sealed partial class JSCommandSettingsProxy : ICommandSettings
{
    private readonly string _settingsPageId;
    private readonly JsonRpcConnection _connection;
    private readonly JsonElement _settingsPageData;

    public JSCommandSettingsProxy(string settingsPageId, JsonRpcConnection connection, JsonElement settingsPageData = default)
    {
        _settingsPageId = settingsPageId;
        _connection = connection;
        _settingsPageData = settingsPageData;
    }

    public IContentPage SettingsPage => new JSContentPageProxy(_settingsPageId, _connection, _settingsPageData);
}
