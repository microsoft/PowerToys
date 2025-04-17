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
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarkPlaceholderForm : FormContent
{
    private static readonly CompositeFormat ErrorMessage = System.Text.CompositeFormat.Parse(Resources.bookmarks_required_placeholder);

    private readonly List<string> _placeholderNames;

    private readonly string _bookmark = string.Empty;

    // TODO pass in an array of placeholders
    public BookmarkPlaceholderForm(string name, string url, string type)
    {
        _bookmark = url;
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
            var uri = UrlCommand.GetUri(target);
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
