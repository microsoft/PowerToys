// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed class AddBookmarkForm : Form
{
    internal event TypedEventHandler<object, object?>? AddedAction;

    public override string TemplateJson()
    {
        var json = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
        "type": "Input.Text",
        "style": "text",
        "id": "name",
        "label": "Name",
        "isRequired": true,
        "errorMessage": "Name is required"
        },
        {
        "type": "Input.Text",
        "style": "text",
        "id": "bookmark",
        "label": "URL or File Path",
        "isRequired": true,
        "errorMessage": "URL or File Path is required"
        }
    ],
    "actions": [
        {
        "type": "Action.Submit",
        "title": "Save",
        "data": {
            "name": "name",
            "bookmark": "bookmark"
        }
        }
    ]
}
""";
        return json;
    }

    public override string DataJson() => throw new NotImplementedException();

    public override string StateJson() => throw new NotImplementedException();

    public override ActionResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput == null)
        {
            return ActionResult.GoHome();
        }

        // get the name and url out of the values
        var formName = formInput["name"] ?? string.Empty;
        var formBookmark = formInput["bookmark"] ?? string.Empty;
        var hasPlaceholder = formBookmark.ToString().Contains('{') && formBookmark.ToString().Contains('}');

        // Determine the type of the bookmark
        string bookmarkType;

        if (formBookmark.ToString().StartsWith("http://", StringComparison.OrdinalIgnoreCase) || formBookmark.ToString().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            bookmarkType = "web";
        }
        else if (File.Exists(formBookmark.ToString()))
        {
            bookmarkType = "file";
        }
        else if (Directory.Exists(formBookmark.ToString()))
        {
            bookmarkType = "folder";
        }
        else
        {
            // Default to web if we can't determine the type
            bookmarkType = "web";
        }

        var formData = new BookmarkData()
        {
            Name = formName.ToString(),
            Bookmark = formBookmark.ToString(),
            Type = bookmarkType,
        };

        // Construct a new json blob with the name and url
        var jsonPath = BookmarksActionProvider.StateJsonPath();
        var data = Bookmarks.ReadFromFile(jsonPath);

        data.Data.Add(formData);

        Bookmarks.WriteToFile(jsonPath, data);

        AddedAction?.Invoke(this, null);
        return ActionResult.GoHome();
    }
}
