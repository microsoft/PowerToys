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
            "text": "Create your new extension",
            "size": "large"
        },
        {
            "type": "TextBlock",
            "text": "Use this page to create a new extension project.",
            "wrap": true
        },
        {
            "type": "TextBlock",
            "text": "Extension name",
            "weight": "bolder",
            "size": "default"
        },
        {
            "type": "TextBlock",
            "text": "This is the name of your new extension project. It should be a valid C# class name. Best practice is to also include the word 'Extension' in the name.",
            "wrap": true
        },
        {
            "type": "Input.Text",
            "label": "Extension name",
            "isRequired": true,
            "errorMessage": "Extension name is required, without spaces",
            "id": "ExtensionName",
            "placeholder": "ExtensionName",
            "regex": "^[^\\s]+$"
        },
        {
            "type": "TextBlock",
            "text": "Display name",
            "weight": "bolder",
            "size": "default"
        },
        {
            "type": "TextBlock",
            "text": "The name of your extension as users will see it.",
            "wrap": true
        },
        {
            "type": "Input.Text",
            "label": "Display name",
            "isRequired": true,
            "errorMessage": "Display name is required",
            "id": "DisplayName",
            "placeholder": "My new extension"
        },
        {
            "type": "TextBlock",
            "text": "Output path",
            "weight": "bolder",
            "size": "default"
        },
        {
            "type": "TextBlock",
            "text": "Where should the new extension be created? This path will be created if it doesn't exist",
            "wrap": true
        },
        {
            "type": "Input.Text",
            "label": "Output path",
            "isRequired": true,
            "errorMessage": "Output path is required",
            "id": "OutputPath",
            "placeholder": "C:\\users\\me\\dev"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Create extension",
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
        BuiltinsExtensionHost.Instance.ShowStatus(_creatingMessage);

        try
        {
            CreateExtension(extensionName, displayName, outputPath);

            // _creatingMessage.Progress = null;
            // _creatingMessage.State = MessageState.Success;
            // _creatingMessage.Message = $"Successfully created extension";
            BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);

            // BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);
            RaiseFormSubmit(new CreatedExtensionForm(extensionName, displayName, outputPath));

            // _toast.Message.State = MessageState.Success;
            // _toast.Message.Message = $"Successfully created extension";
            // _toast.Show();
        }
        catch (Exception e)
        {
            BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);

            _creatingMessage.State = MessageState.Error;
            _creatingMessage.Message = $"Error: {e.Message}";

            // _toast.Show();
        }

        // _ = Task.Run(() =>
        // {
        //    Thread.Sleep(2500);
        //    BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);
        // });
        return CommandResult.KeepOpen();
    }

    private void CreateExtension(string extensionName, string newDisplayName, string outputPath)
    {
        var newGuid = Guid.NewGuid().ToString();

        // Unzip `template.zip` to a temp dir:
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Console.WriteLine($"Extracting to {tempDir}");

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

            Console.WriteLine($"  Processing {file}");

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

            Console.WriteLine($"  Wrote {newFileName}");

            // Delete the old file
            File.Delete(file);
        }

        // Delete the temp dir
        Directory.Delete(tempDir, true);
    }
}
