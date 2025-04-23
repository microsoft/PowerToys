// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class AddBookmarkForm : FormContent
{
    internal event TypedEventHandler<object, BookmarkData>? AddedCommand;

    private readonly BookmarkData? _bookmark;

    public AddBookmarkForm(BookmarkData? bookmark)
    {
        _bookmark = bookmark;
        var name = _bookmark?.Name ?? string.Empty;
        var url = _bookmark?.Bookmark ?? string.Empty;
        var bookmarkType = _bookmark?.Type.ToString() ?? string.Empty;
        var bookmarkTypeChoices = BookmarkTypeHelper.GetBookmarkChoices();

        TemplateJson = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
            "type": "Input.ChoiceSet",
            "label": "{{Resources.bookmarks_form_bookmark_type}}",
            "value":  {{JsonSerializer.Serialize(bookmarkType)}},
            "choices": {{bookmarkTypeChoices}},
            "placeholder": "Placeholder text",
            "id": "bookmarkType"
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "name",
            "label": "{{Resources.bookmarks_form_name_label}}",
            "value": {{JsonSerializer.Serialize(name)}},
            "isRequired": true,
            "errorMessage": "{{Resources.bookmarks_form_name_required}}"
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "bookmark",
            "value": {{JsonSerializer.Serialize(url)}},
            "label": "{{Resources.bookmarks_form_bookmark_label}}",
            "isRequired": true,
            "errorMessage": "{{Resources.bookmarks_form_bookmark_required}}"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "{{Resources.bookmarks_form_save}}",
            "data": {
                "name": "name",
                "bookmark": "bookmark"
            }
        }
    ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        // get the name and url out of the values
        var formName = formInput["name"] ?? string.Empty;
        var formBookmark = formInput["bookmark"] ?? string.Empty;
        var hasPlaceholder = formBookmark.ToString().Contains('{') && formBookmark.ToString().Contains('}');
        var bookmarkTypeString = formInput["bookmarkType"]?.ToString() ?? string.Empty;

        // Determine the type of the bookmark
        if (!Enum.TryParse<BookmarkType>(bookmarkTypeString, true, out var bookmarkType))
        {
            // TODO: need a toast
            return CommandResult.GoHome();
        }

        if (!ValidateBookmarkData(formName.ToString(), formBookmark.ToString(), bookmarkType))
        {
            // TODO: need a toast
            return CommandResult.GoHome();
        }

        var updated = _bookmark ?? new BookmarkData();
        updated.Name = formName.ToString();
        updated.Bookmark = formBookmark.ToString();
        updated.Type = bookmarkType;
        var paths = EnvironmentsCache.Instance.GetPaths();

        AddedCommand?.Invoke(this, updated);
        return CommandResult.GoHome();
    }

    private bool ValidateBookmarkData(string name, string bookmark, BookmarkType type)
    {
        return true;
    }
}
