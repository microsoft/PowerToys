// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class NewExtensionForm : NewExtensionFormBase
{
    private static readonly string _creatingText = "Creating new extension...";
    private readonly StatusMessage _creatingMessage = new()
    {
        Message = _creatingText,
        Progress = new ProgressState() { IsIndeterminate = true },
    };

    public NewExtensionForm()
    {
        TemplateJson = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_page_title}}",
            "size": "large"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_page_text}}",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_name_header}}",
            "weight": "bolder",
            "size": "default"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_name_description}}",
            "wrap": true
        },
        {
            "type": "Input.Text",
            "label": "{{Properties.Resources.builtin_create_extension_name_label}}",
            "isRequired": true,
            "errorMessage": "{{Properties.Resources.builtin_create_extension_name_required}}",
            "id": "ExtensionName",
            "placeholder": "ExtensionName",
            "regex": "^[^\\s]+$"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_display_name_header}}",
            "weight": "bolder",
            "size": "default"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_display_name_description}}",
            "wrap": true
        },
        {
            "type": "Input.Text",
            "label": "{{Properties.Resources.builtin_create_extension_display_name_label}}",
            "isRequired": true,
            "errorMessage": "{{Properties.Resources.builtin_create_extension_display_name_required}}",
            "id": "DisplayName",
            "placeholder": "My new extension"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_directory_header}}",
            "weight": "bolder",
            "size": "default"
        },
        {
            "type": "TextBlock",
            "text": "{{Properties.Resources.builtin_create_extension_directory_description}}",
            "wrap": true
        },
        {
            "type": "Input.Text",
            "label": "{{Properties.Resources.builtin_create_extension_directory_label}}",
            "isRequired": true,
            "errorMessage": "{{Properties.Resources.builtin_create_extension_directory_required}}",
            "id": "OutputPath",
            "placeholder": "C:\\users\\me\\dev"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "{{Properties.Resources.builtin_create_extension_submit}}",
            "associatedInputs": "auto"
        }
    ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput == null)
        {
            return CommandResult.KeepOpen();
        }

        var extensionName = formInput["ExtensionName"]?.AsValue()?.ToString() ?? string.Empty;
        var displayName = formInput["DisplayName"]?.AsValue()?.ToString() ?? string.Empty;
        var outputPath = formInput["OutputPath"]?.AsValue()?.ToString() ?? string.Empty;

        _creatingMessage.State = MessageState.Info;
        _creatingMessage.Message = _creatingText;
        _creatingMessage.Progress = new ProgressState() { IsIndeterminate = true };
        BuiltinsExtensionHost.Instance.ShowStatus(_creatingMessage, StatusContext.Extension);

        try
        {
            CreateExtension(extensionName, displayName, outputPath);

            BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);

            RaiseFormSubmit(new CreatedExtensionForm(extensionName, displayName, outputPath));
        }
        catch (Exception e)
        {
            BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);

            _creatingMessage.State = MessageState.Error;
            _creatingMessage.Message = $"Error: {e.Message}";
        }

        return CommandResult.KeepOpen();
    }

    private void CreateExtension(string extensionName, string newDisplayName, string outputPath)
    {
        var newGuid = Guid.NewGuid().ToString();

        // Unzip `template.zip` to a temp dir:
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Does the output path exist?
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Microsoft.CmdPal.UI.ViewModels\\Assets\\template.zip");
        ZipFile.ExtractToDirectory(assetsPath, tempDir);

        var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            // Replace all the instances of `FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF` with a new random guid:
            text = text.Replace("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", newGuid);

            // Then replace all the `TemplateCmdPalExtension` with `extensionName`
            text = text.Replace("TemplateCmdPalExtension", extensionName);

            // Then replace all the `TemplateDisplayName` with `newDisplayName`
            text = text.Replace("TemplateDisplayName", newDisplayName);

            // We're going to write the file to the same relative location in the output path
            var relativePath = Path.GetRelativePath(tempDir, file);

            var newFileName = Path.Combine(outputPath, relativePath);

            // if the file name had `TemplateCmdPalExtension` in it, replace it with `extensionName`
            newFileName = newFileName.Replace("TemplateCmdPalExtension", extensionName);

            // Make sure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(newFileName)!);

            File.WriteAllText(newFileName, text);

            // Delete the old file
            File.Delete(file);
        }

        // Delete the temp dir
        Directory.Delete(tempDir, true);
    }
}
