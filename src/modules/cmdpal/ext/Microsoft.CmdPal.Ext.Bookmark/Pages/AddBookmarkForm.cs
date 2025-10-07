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
            "value": {{JsonSerializer.Serialize(url, BookmarkSerializationContext.Default.String)}},
            "label": "{{Resources.bookmarks_form_bookmark_label}}",
            "isRequired": true,
            "errorMessage": "{{Resources.bookmarks_form_bookmark_required}}"
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "name",
            "label": "{{Resources.bookmarks_form_name_label}}",
            "value": {{JsonSerializer.Serialize(name, BookmarkSerializationContext.Default.String)}},
            "isRequired": false,
            "errorMessage": "{{Resources.bookmarks_form_name_required}}"
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
