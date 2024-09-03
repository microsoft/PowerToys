// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Bot.AdaptiveExpressions.Core;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using Windows.System;

namespace Run.Bookmarks;

internal sealed class OpenInTerminalAction : InvokableCommand
{
    private readonly string _folder;

    public OpenInTerminalAction(string folder)
    {
        this.Name = "Open in Terminal";
        this._folder = folder;
    }

    public override ICommandResult Invoke()
    {
        try
        {
            // Start Windows Terminal with the specified folder
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{_folder}\"",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching Windows Terminal: {ex.Message}");
        }

        return ActionResult.Dismiss();
    }
}


internal sealed class BookmarkData
{
    internal string name = string.Empty;
    internal string bookmark = string.Empty;
    internal string type = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(BookmarkData))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(BookmarkData))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string))]
internal sealed partial class BookmarkDataContext : JsonSerializerContext
{
}

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



        // Construct a new json blob with the name and url
        var json = string.Empty;

        // if the file exists, load it and append the new item
        if (File.Exists(BookmarksActionProvider.StateJsonPath()))
        {
            var state = File.ReadAllText(BookmarksActionProvider.StateJsonPath());
            var jsonState = JsonNode.Parse(state);
            var items = jsonState?["items"]?.AsArray();

            if (items != null)
            {
                // var items = jsonState["items"];
                var newItem = new JsonObject();
                newItem["name"] = formName;
                newItem["bookmark"] = formBookmark;
                var formData = new BookmarkData()
                {
                    name = formName.ToString(),
                    bookmark = formBookmark.ToString(),
                    type = bookmarkType,
                };

                items.Add(JsonSerializer.SerializeToNode(formData, typeof(BookmarkData), SourceGenerationContext.Default));

                json = jsonState?.ToString();
            }
        }
        else {
            json = $$"""
{
    "items": [
        {
        "name": "{{formName}}",
        "type": "{{bookmarkType}}",
        "bookmark": "{{formBookmark}}",
        "hasPlaceholder":"{{hasPlaceholder}}"
        }
    ]
}
""";
        }

        File.WriteAllText(BookmarksActionProvider.StateJsonPath(), json);
        AddedAction?.Invoke(this, null);
        return ActionResult.GoHome();
    }

}

internal sealed class AddBookmarkPage : Microsoft.Windows.CommandPalette.Extensions.Helpers.FormPage
{
    private readonly AddBookmarkForm _addBookmark = new();

    internal event TypedEventHandler<object, object?>? AddedAction {
        add => _addBookmark.AddedAction += value;
        remove => _addBookmark.AddedAction -= value;
    }

    public override IForm[] Forms() => [_addBookmark];

    public AddBookmarkPage()
    {
        this.Icon = new("\ued0e");
        this.Name = "Add a Bookmark";
    }
}

internal sealed class BookmarkPlaceholderForm: Microsoft.Windows.CommandPalette.Extensions.Helpers.Form
{
    private List<string> placeholderNames { get; init; }

    private readonly string _Bookmark = string.Empty;

    // TODO pass in an array of placeholders
    public BookmarkPlaceholderForm(string name, string url, string type) {
        _Bookmark = url;
        Regex r = new Regex(Regex.Escape("{") + "(.*?)" + Regex.Escape("}"));
        MatchCollection matches = r.Matches(url);
        placeholderNames = matches.Select(m => m.Groups[1].Value).ToList();
    }

    public override string TemplateJson()
    {
        var inputs = placeholderNames.Select(p => {
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

    public override ActionResult SubmitForm(string payload)
    {
        var target = _Bookmark;
        // parse the submitted JSON and then open the link
        var formInput = JsonNode.Parse(payload);
        var formObject = formInput?.AsObject();
        if (formObject == null)
        {
            return ActionResult.GoHome();
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

        return ActionResult.GoHome();
    }

}

internal sealed class BookmarkPlaceholderPage : Microsoft.Windows.CommandPalette.Extensions.Helpers.FormPage
{
    private readonly IForm _bookmarkPlaceholder;

    public override IForm[] Forms() => [_bookmarkPlaceholder];

    public BookmarkPlaceholderPage(string name, string url, string type)
    {
        _Name = name;
        Icon = new(UrlAction.IconFromUrl(url, type));
        _bookmarkPlaceholder = new BookmarkPlaceholderForm(name, url, type);
    }
}

public class UrlAction : InvokableCommand
{
    private bool _containsPlaceholder => _url.Contains('{') && _url.Contains('}');

    public string Type { get; }

    public string Url {  get; }

    private readonly string _url;

    public UrlAction(string name, string url, string type)
    {
        _url = url;
        Icon = new(IconFromUrl(_url, type));
        Name = name;
        Type = type;
        Url = url;
    }

    public override ActionResult Invoke()
    {
        var target = _url;
        try
        {
            Uri? uri = GetUri(target);
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

        return ActionResult.Dismiss();
    }

    internal static Uri? GetUri(string url)
    {
        Uri? uri;
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            if (!Uri.TryCreate("https://" + url, UriKind.Absolute, out uri))
            {
                return null;
            }
        }
        return uri;
    }

    internal static string IconFromUrl(string url, string type)
    {
        switch (type)
        {
            case "file":
                return "📄";
            case "folder":
                return "📁";
            case "web":
            default:
                // Get the base url up to the first placeholder
                var placeholderIndex = url.IndexOf('{');
                var baseString = placeholderIndex > 0 ? url.Substring(0, placeholderIndex) : url;
                try
                {
                    Uri? uri = GetUri(baseString);
                    if (uri != null)
                    {
                        var hostname = uri.Host;
                        var faviconUrl = $"{uri.Scheme}://{hostname}/favicon.ico";
                        return faviconUrl;
                    }
                }
                catch (UriFormatException)
                {
                    // return "🔗";
                }
                return "🔗";
        }
    }
}


public class BookmarksActionProvider : ICommandProvider
{
    public string DisplayName => $"Bookmarks";

    public IconDataType Icon => new(string.Empty);

    private readonly List<ICommand> _commands = [];
    private readonly AddBookmarkPage _addNewCommand = new();

    public BookmarksActionProvider()
    {
        _addNewCommand.AddedAction += _addNewCommand_AddedAction;

    }

    private void _addNewCommand_AddedAction(object sender, object? args)
    {

        _addNewCommand.AddedAction += _addNewCommand_AddedAction;
        _commands.Clear();
    }


#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    private void LoadCommands()
    {
        List<ICommand> collected = [];
        collected.Add(_addNewCommand);
        try
        {
            // Open state.json from the disk and read it
            var state = File.ReadAllText(BookmarksActionProvider.StateJsonPath());
            // Parse the JSON

            var json = JsonNode.Parse(state);
            var jsonObject = json?.AsObject();
            if (jsonObject == null)
            {
                return;
            }


            if (!jsonObject.ContainsKey("items"))
            {
                return;
            }
            var itemsJson = jsonObject["items"]?.AsArray();
            if (itemsJson == null) { return; }

            foreach (var item in itemsJson)
            {
                var nameToken = item?["name"];
                var urlToken = item?["bookmark"];
                var typeToken = item?["type"];
                if (nameToken == null || urlToken == null || typeToken == null)
                {
                    continue;
                }
                var name = nameToken.ToString();
                var url = urlToken.ToString();
                var type = typeToken.ToString();
                collected.Add((url.Contains('{') && url.Contains('}')) ? new BookmarkPlaceholderPage(name, url, type) : new UrlAction(name, url, type));
            }
        }
        catch (Exception ex)
        {
            // debug log error
            Console.WriteLine($"Error loading commands: {ex.Message}");
        }


        _commands.Clear();
        _commands.AddRange(collected);
    }

    public IListItem[] TopLevelCommands()
    {
        if (_commands.Count == 0)
        {
            LoadCommands();
        }
        return _commands.Select(action =>
        {
            var listItem = new ListItem(action);

            // Add actions for folder types
            if (action is UrlAction urlAction && urlAction.Type == "folder")
            {
                listItem.MoreCommands = [
                    new CommandContextItem(new OpenInTerminalAction(urlAction.Url))
                ];
            }
            //listItem.Subtitle = "Bookmark";
            if (action is AddBookmarkPage) { }
            else
            {
                listItem.Tags = [
                    new Tag() {
                        Text = "Bookmark",
                        //Icon = new("🔗"),
                        //Color=Windows.UI.Color.FromArgb(255, 255, 0, 255)
                    },
                    //new Tag() {
                    //    Text = "A test",
                    //    //Icon = new("🔗"),
                    //    Color=Windows.UI.Color.FromArgb(255, 255, 0, 0)
                    //}
                ];
            }

            return listItem;
        }).ToArray();
    }

    internal static string StateJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
        // Get the directory of the exe
        var directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        // now, the state is just next to the exe
        return System.IO.Path.Combine(directory, "state.json");
    }
}

