// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class BookmarkPlaceholderForm : FormContent
{
    private static readonly CompositeFormat ErrorMessage = CompositeFormat.Parse(Resources.bookmarks_required_placeholder);

    private readonly BookmarkData _bookmarkData;
    private readonly IBookmarkResolver _commandResolver;

    public BookmarkPlaceholderForm(BookmarkData data, IBookmarkResolver commandResolver)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(commandResolver);

        _bookmarkData = data;
        _commandResolver = commandResolver;
        var matches = CurlyContent().Matches(data.Bookmark);
        var placeholderNames = matches.Select(m => m.Groups[1].Value).ToList();
        var inputs = placeholderNames.Select(placeholderName =>
        {
            var errorMessage = string.Format(CultureInfo.CurrentCulture, ErrorMessage, placeholderName);
            return $$"""
                     {
                         "type": "Input.Text",
                         "style": "text",
                         "id": "{{placeholderName}}",
                         "label": "{{placeholderName}}",
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
                               {
                                 "type": "TextBlock",
                                 "size": "Medium",
                                 "weight": "Bolder",
                                 "text": "{{_bookmarkData.Name}}"
                               },
                               {{allInputs}}
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
        // parse the submitted JSON and then open the link
        var formInput = JsonNode.Parse(payload);
        var formObject = formInput?.AsObject();
        if (formObject is null)
        {
            return CommandResult.GoHome();
        }

        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in formObject)
        {
            var placeholderData = value?.ToString();
            placeholders[key] = placeholderData ?? string.Empty;
        }

        var target = ReplacePlaceholders(_bookmarkData.Bookmark, placeholders);
        var classification = _commandResolver.ClassifyOrUnknown(target);
        var success = CommandLauncher.Launch(classification);
        return success ? CommandResult.Dismiss() : CommandResult.KeepOpen();
    }

    private static string ReplacePlaceholders(string input, Dictionary<string, string> placeholders)
    {
        var result = input;
        foreach (var (key, value) in placeholders)
        {
            var placeholderString = $"{{{key}}}";
            result = result.Replace(placeholderString, value);
        }

        return result;
    }

    [GeneratedRegex(@"\{(.*?)\}", RegexOptions.Compiled)]
    private static partial Regex CurlyContent();
}
