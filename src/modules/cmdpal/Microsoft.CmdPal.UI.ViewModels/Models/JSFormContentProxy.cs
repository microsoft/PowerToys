// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Proxy that presents a Node.js extension form as <see cref="IFormContent"/>.
/// Submitting the form forwards a <c>form/submit</c> request and maps the
/// response to a toolkit command result.
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

        // Each serialized form carries a required formId that is unique within its
        // page. Capturing it here lets a page with multiple forms, or a form nested
        // inside tree content, route its submission back to the correct handler
        // instead of relying on the SDK first-form fallback.
        _formId = JSModelMapper.GetString(_data, "formId") ?? JSModelMapper.GetString(_data, "FormId") ?? string.Empty;
    }

    public string TemplateJson => GetJsonProperty("template", "templateJson");

    public string DataJson => GetJsonProperty("data", "dataJson");

    public string StateJson => GetJsonProperty("state", "stateJson");

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

    private string GetJsonProperty(string camel, string pascal)
    {
        if (JSModelMapper.TryGetAnyCase(_data, camel, pascal, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? string.Empty : prop.GetRawText();
        }

        return string.Empty;
    }
}
