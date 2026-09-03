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
internal sealed partial class JSCommandSettingsProxy : ICommandSettings, IDisposable
{
    private readonly JSLazyCache<IContentPage> _settingsPage;

    public JSCommandSettingsProxy(string settingsPageId, JsonRpcConnection connection, JsonElement settingsPageData = default)
    {
        _settingsPage = new JSLazyCache<IContentPage>(
            () => new JSContentPageProxy(settingsPageId, connection, settingsPageData),
            JSLazyCache<IContentPage>.DisposeValue);
    }

    public IContentPage SettingsPage => _settingsPage.Value;

    public void Dispose() => _settingsPage.Dispose();
}
