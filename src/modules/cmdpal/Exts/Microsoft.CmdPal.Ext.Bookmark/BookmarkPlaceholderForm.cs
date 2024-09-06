// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed class BookmarkPlaceholderForm : Form
{
    private readonly List<string> _placeholderNames;

    private readonly string _bookmark = string.Empty;

    // TODO pass in an array of placeholders
    public BookmarkPlaceholderForm(string name, string url, string type)
    {
        _bookmark = url;
        Regex r = new Regex(Regex.Escape("{") + "(.*?)" + Regex.Escape("}"));
        MatchCollection matches = r.Matches(url);
        _placeholderNames = matches.Select(m => m.Groups[1].Value).ToList();
    }

    public override string TemplateJson()
    {
        var inputs = _placeholderNames.Select(p =>
        {
            return $$"""
{
    "type": "Input.Text",
    "style": "text",
    "id": "{{p}}",
    "label": "{{p}}",
    "isRequired": true,
    "errorMessage": "{{p}} is required"
}
""";
        }).ToList();

        var allInputs = string.Join(",", inputs);

        var json = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
""" + allInputs + """
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Open",
      "data": {
        "placeholder": "placeholder"
      }
    }
  ]
}
""";
        return json;
    }

    public override string DataJson() => throw new NotImplementedException();

    public override string StateJson() => throw new NotImplementedException();

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
            Uri? uri = UrlAction.GetUri(target);
            if (uri != null)
            {
                _ = Launcher.LaunchUriAsync(uri);
            }
            else
            {
                // throw new UriFormatException("The provided URL is not valid.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching URL: {ex.Message}");
        }

        return CommandResult.GoHome();
    }
}
