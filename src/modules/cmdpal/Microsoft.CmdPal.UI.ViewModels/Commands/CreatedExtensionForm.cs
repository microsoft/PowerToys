// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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
        JsonObject? dataInput = JsonNode.Parse(data)?.AsObject();
        if (dataInput == null)
        {
            return CommandResult.KeepOpen();
        }

        string verb = dataInput["x"]?.AsValue()?.ToString() ?? string.Empty;
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
        string pathToSolution = Path.Combine(parts);
        ShellHelpers.OpenInShell(pathToSolution);
        return CommandResult.Hide();
    }

    private ICommandResult OpenDirectory()
    {
        string[] parts = [_path, _name];
        string pathToDir = Path.Combine(parts);
        ShellHelpers.OpenInShell(pathToDir);
        return CommandResult.Hide();
    }

    private ICommandResult CreateNew()
    {
        RaiseFormSubmit(null);
        return CommandResult.KeepOpen();
    }

    private static readonly string CardTemplate = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_success}}",
            "size": "large",
            "weight": "bolder",
            "style": "heading",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_created_in_text}}",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "${directory}",
            "fontType": "monospace"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_created_next_steps_title}}",
            "style": "heading",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_created_next_steps}}",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_created_next_steps_p2}}",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_created_next_steps_p3}}",
            "wrap": true
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "{{Properties.Resources.builtin_create_extension_open_solution}}",
            "data": {
                "x": "sln"
            }
        },
        {
            "type": "Action.Submit",
            "title": "{{Properties.Resources.builtin_create_extension_open_directory}}",
            "data": {
                "x": "dir"
            }
        },
        {
            "type": "Action.Submit",
            "title": "{{Properties.Resources.builtin_create_extension_create_another}}",
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
