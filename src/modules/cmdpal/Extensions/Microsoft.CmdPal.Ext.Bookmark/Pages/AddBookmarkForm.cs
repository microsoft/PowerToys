// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class AddBookmarkForm : FormContent
{
    private readonly BookmarkData? _bookmark;

    internal event TypedEventHandler<object, BookmarkData>? AddedCommand;

    public AddBookmarkForm(BookmarkData? bookmark)
    {
        _bookmark = bookmark;
        var name = bookmark?.Name ?? string.Empty;
        var url = bookmark?.Bookmark ?? string.Empty;
        TemplateJson = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
            "type": "Input.Text",
            "style": "text",
            "id": "bookmark",
            "value": {{EncodeString(url)}},
            "label": {{EncodeString(Resources.bookmarks_form_bookmark_label)}},
            "isRequired": true,
            "errorMessage": {{EncodeString(Resources.bookmarks_form_bookmark_required)}},
            "placeholder": {{EncodeString(Resources.bookmarks_form_bookmark_placeholder)}}
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "name",
            "label": {{EncodeString(Resources.bookmarks_form_name_label)}},
            "value": {{EncodeString(name)}},
            "isRequired": false
        },
        {
            "type": "RichTextBlock",
            "inlines": [
                {
                    "type": "TextRun",
                    "text": {{EncodeString(Resources.bookmarks_form_hint_text1)}},
                    "isSubtle": true,
                    "size": "Small"
                },
                {
                    "type": "TextRun",
                    "text": " ",
                    "isSubtle": true,
                    "size": "Small"
                },
                {
                    "type": "TextRun",
                    "text": {{EncodeString(Resources.bookmarks_form_hint_text2)}},
                    "fontType": "Monospace",
                    "size": "Small"
                },
                {
                    "type": "TextRun",
                    "text": " ",
                    "isSubtle": true,
                    "size": "Small"
                },
                {
                    "type": "TextRun",
                    "text": {{EncodeString(Resources.bookmarks_form_hint_text3)}},
                    "isSubtle": true,
                    "size": "Small"
                },
                {
                    "type": "TextRun",
                    "text": " ",
                    "isSubtle": true,
                    "size": "Small"
                },
                {
                    "type": "TextRun",
                    "text": {{EncodeString(Resources.bookmarks_form_hint_text4)}},
                    "fontType": "Monospace",
                    "size": "Small"
                }
            ]
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": {{EncodeString(Resources.bookmarks_form_save)}},
            "data": {
                "name": "name",
                "bookmark": "bookmark"
            }
        }
    ]
}
""";
    }

    private static string EncodeString(string s) => JsonSerializer.Serialize(s, BookmarkSerializationContext.Default.String);

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput is null)
        {
            return CommandResult.GoHome();
        }

        // get the name and url out of the values
        var formName = formInput["name"] ?? string.Empty;
        var formBookmark = formInput["bookmark"] ?? string.Empty;
        AddedCommand?.Invoke(this, new BookmarkData(formName.ToString(), formBookmark.ToString()) { Id = _bookmark?.Id ?? Guid.Empty });
        return CommandResult.GoHome();
    }
}
