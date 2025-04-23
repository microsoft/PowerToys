// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Ext.Bookmarks.Command;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarkPlaceholderForm : FormContent
{
    private static readonly CompositeFormat ErrorMessage = System.Text.CompositeFormat.Parse(Resources.bookmarks_required_placeholder);

    private readonly List<string> _placeholderNames;

    private readonly string _bookmark = string.Empty;

    private BookmarkType _bookmarkType;

    // TODO pass in an array of placeholders
    public BookmarkPlaceholderForm(string name, string url, BookmarkType bookmarkType)
    {
        _bookmark = url;
        _bookmarkType = bookmarkType;

        var r = new Regex(Regex.Escape("{") + "(.*?)" + Regex.Escape("}"));
        var matches = r.Matches(url);
        _placeholderNames = matches.Select(m => m.Groups[1].Value).ToList();
        var inputs = _placeholderNames.Select(p =>
        {
            var errorMessage = string.Format(CultureInfo.CurrentCulture, ErrorMessage, p);
            return $$"""
{
    "type": "Input.Text",
    "style": "text",
    "id": "{{p}}",
    "label": "{{p}}",
    "isRequired": true,
    "errorMessage": "{{errorMessage}}"
}
""";
        }).ToList();

        var allInputs = string.Join(",", inputs);

        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
""" + allInputs + $$"""
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "{{Resources.bookmarks_form_open}}",
      "data": {
        "placeholder": "placeholder"
      }
    }
  ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var target = _bookmark;

        // parse the submitted JSON and then open the link
        var formInput = JsonNode.Parse(payload);
        var formObject = formInput?.AsObject();
        if (formObject == null)
        {
            return CommandResult.GoHome();
        }

        foreach (var (key, value) in formObject)
        {
            var placeholderString = $"{{{key}}}";
            var placeholderData = value?.ToString();
            target = target.Replace(placeholderString, placeholderData);
        }

        try
        {
            CommandResult result = CommandResult.ShowToast(new ToastArgs() { Message = "Invalid bookmark" });

            switch (_bookmarkType)
            {
                case BookmarkType.Cmd:
                case BookmarkType.PWSH:
                case BookmarkType.PowerShell:
                case BookmarkType.Ptyhon3:
                case BookmarkType.Python:
                    result = ShellCommand.Invoke(target, _bookmarkType);
                    break;

                case BookmarkType.Folder:
                case BookmarkType.File:
                case BookmarkType.Web:
                    result = UrlCommand.Invoke(target);
                    break;

                default:
                    ExtensionHost.LogMessage($"Invalid bookmark type: {_bookmarkType}");
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage($"Invoke bookmark failed. ex.message: {ex.Message}");
        }

        return CommandResult.GoHome();
    }
}
