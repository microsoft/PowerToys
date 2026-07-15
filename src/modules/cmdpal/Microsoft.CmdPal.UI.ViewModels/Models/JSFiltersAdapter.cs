// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapts a JSON filters payload to <see cref="IFilters"/>. Changing the current
/// filter forwards a <c>listPage/setFilter</c> request to the extension.
/// </summary>
internal sealed partial class JSFiltersAdapter : IFilters
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private readonly string _pageId;
    private string _currentFilterId;

    public JSFiltersAdapter(JsonElement data, JsonRpcConnection connection, string pageId)
    {
        _data = data;
        _connection = connection;
        _pageId = pageId;
        _currentFilterId = JSModelMapper.GetString(data, "currentFilterId") ?? string.Empty;
    }

    public string CurrentFilterId
    {
        get => _currentFilterId;

        set
        {
            _currentFilterId = value;

            if (!string.IsNullOrEmpty(_pageId))
            {
                _ = _connection.SendRequestAsync(
                    "listPage/setFilter",
                    new JsonObject { ["pageId"] = _pageId, ["filterId"] = value },
                    CancellationToken.None);
            }
        }
    }

    public IFilterItem[] GetFilters() => JSModelMapper.ParseFilterItems(_data);
}
