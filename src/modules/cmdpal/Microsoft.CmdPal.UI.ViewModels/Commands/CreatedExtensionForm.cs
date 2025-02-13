// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class CreatedExtensionForm : NewExtensionFormBase
{
    public CreatedExtensionForm(string name, string displayName, string path)
    {
        TemplateJson = CardTemplate;
        DataJson = $$"""
{
    "name": {{JsonSerializer.Serialize(name)}},
    "directory": {{JsonSerializer.Serialize(path)}},
    "displayName": {{JsonSerializer.Serialize(displayName)}}
}
""";
        _name = name;
        _displayName = displayName;
        _path = path;
    }

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        var dataInput = JsonNode.Parse(data)?.AsObject();
        if (dataInput == null)
        {
            return CommandResult.KeepOpen();
        }

        var verb = dataInput["x"]?.AsValue()?.ToString() ?? string.Empty;
        return verb switch
        {
            "sln" => OpenSolution(),
            "dir" => OpenDirectory(),
            "new" => CreateNew(),
            _ => CommandResult.KeepOpen(),
        };
    }

    private ICommandResult OpenSolution()
    {
        string[] parts = [_path, _name, $"{_name}.sln"];
        var pathToSolution = Path.Combine(parts);
        ShellHelpers.OpenInShell(pathToSolution);
        return CommandResult.GoHome();
    }

    private ICommandResult OpenDirectory()
    {
        string[] parts = [_path, _name];
        var pathToDir = Path.Combine(parts);
        ShellHelpers.OpenInShell(pathToDir);
        return CommandResult.GoHome();
    }

    private ICommandResult CreateNew()
    {
        RaiseFormSubmit(null);
        return CommandResult.KeepOpen();
    }

    private static readonly string CardTemplate = """
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "Successfully created your new extension!",
            "size": "large",
            "weight": "bolder",
            "style": "heading",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "Your new extension \"${displayName}\" was created in:",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "${directory}",
            "fontType": "monospace"
        },
        {
            "type": "TextBlock",
            "text": "Next steps",
            "style": "heading",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "Now that your extension project has been created, open the solution up in Visual Studio to start writing your extension code.",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "Navigate to `${name}Page.cs` to start adding items to the list, or to `${name}CommandsProvider.cs` to add new commands.",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "Once you're ready to test deploy the package locally with Visual Studio, then run the \"Reload\" command in the Command Palette to load your new extension.",
            "wrap": true
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Open Solution",
            "data": {
                "x": "sln"
            }
        },
        {
            "type": "Action.Submit",
            "title": "Open directory",
            "data": {
                "x": "dir"
            }
        },
        {
            "type": "Action.Submit",
            "title": "Create another",
            "data": {
                "x": "new"
            }
        }
    ]
}
""";

    private readonly string _name;
    private readonly string _displayName;
    private readonly string _path;
}
