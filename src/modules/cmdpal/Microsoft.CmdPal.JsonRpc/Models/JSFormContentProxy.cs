// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.JsonRpc.Models;

/// <summary>
/// Exposes a Node.js extension form as <see cref="IFormContent"/>.
/// Submit sends <c>form/submit</c> and maps the response to a toolkit command result.
/// </summary>
internal sealed partial class JSFormContentProxy : BaseObservable, IFormContent
{
    private readonly string _pageId;
    private readonly string _formId;
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;

    public JSFormContentProxy(string pageId, JsonElement data, JsonRpcConnection connection)
    {
        _pageId = pageId;
        _data = data;
        _connection = connection;

        // Each form carries a formId that is unique within its page. Keep it so pages
        // with multiple forms, or forms nested in tree content, submit to the correct
        // handler instead of the SDK first-form fallback.
        _formId = JSModelMapper.GetString(_data, "formId") ?? string.Empty;
    }

    public string TemplateJson => GetJsonProperty("templateJson");

    public string DataJson => GetJsonProperty("dataJson");

    public string StateJson => GetJsonProperty("stateJson");

    public ICommandResult SubmitForm(string inputs, string data)
    {
        try
        {
            var request = new JsonObject { ["pageId"] = _pageId, ["inputs"] = inputs, ["data"] = data };
            if (!string.IsNullOrEmpty(_formId))
            {
                request["formId"] = _formId;
            }

            var response = _connection.SendRequestAsync(
                "form/submit",
                request,
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"Form submit error for page {_pageId}: {response.Error.Message}");
                return CommandResult.KeepOpen();
            }

            return JSCommandResultParser.ParseCommandResult(response.Result, _connection);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to submit form for page {_pageId}: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }

    private string GetJsonProperty(string name)
    {
        if (JSModelMapper.TryGetProperty(_data, name, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? string.Empty : prop.GetRawText();
        }

        return string.Empty;
    }
}
