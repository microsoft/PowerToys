// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class NewExtensionForm : NewExtensionFormBase
{
    private static readonly string _creatingText = "Creating new extension...";
    private readonly IExtensionTemplateService _extensionTemplateService;
    private readonly StatusMessage _creatingMessage = new()
    {
        Message = _creatingText,
        Progress = new ProgressState() { IsIndeterminate = true },
    };

    public NewExtensionForm()
        : this(new ExtensionTemplateService())
    {
    }

    private NewExtensionForm(IExtensionTemplateService extensionTemplateService)
    {
        ArgumentNullException.ThrowIfNull(extensionTemplateService);

        _extensionTemplateService = extensionTemplateService;
        TemplateJson = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": {{FormatJsonString(Properties.Resources.builtin_create_extension_page_title)}},
            "size": "medium",
            "weight": "bolder"
        },
        {
            "type": "Input.Text",
            "label": {{FormatJsonString(Properties.Resources.builtin_create_extension_name_label)}},
            "isRequired": true,
            "errorMessage": {{FormatJsonString(Properties.Resources.builtin_create_extension_name_required)}},
            "id": "ExtensionName",
            "placeholder": "ExtensionName",
            "regex": "^[a-zA-Z_][a-zA-Z0-9_]*$"
        },
        {
            "type": "TextBlock",
            "text": {{FormatJsonString(Properties.Resources.builtin_create_extension_name_description)}},
            "wrap": true,
            "size": "small",
            "isSubtle": true,
            "spacing": "none"
        },
        {
            "type": "Input.Text",
            "label": {{FormatJsonString(Properties.Resources.builtin_create_extension_display_name_label)}},
            "isRequired": true,
            "errorMessage": {{FormatJsonString(Properties.Resources.builtin_create_extension_display_name_required)}},
            "id": "DisplayName",
            "placeholder": "My new extension",
            "spacing": "medium"
        },
        {
            "type": "TextBlock",
            "text": {{FormatJsonString(Properties.Resources.builtin_create_extension_display_name_description)}},
            "wrap": true,
            "size": "small",
            "isSubtle": true,
            "spacing": "none"
        },
        {
            "type": "Input.Text",
            "label": {{FormatJsonString(Properties.Resources.builtin_create_extension_directory_label)}},
            "isRequired": true,
            "errorMessage": {{FormatJsonString(Properties.Resources.builtin_create_extension_directory_required)}},
            "id": "OutputPath",
            "placeholder": "C:\\users\\me\\dev",
            "spacing": "medium"
        },
        {
            "type": "TextBlock",
            "text": {{FormatJsonString(Properties.Resources.builtin_create_extension_directory_description)}},
            "wrap": true,
            "size": "small",
            "isSubtle": true,
            "spacing": "none"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": {{FormatJsonString(Properties.Resources.builtin_create_extension_submit)}},
            "associatedInputs": "auto"
        }
    ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput is null)
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
            _extensionTemplateService.CreateExtension(extensionName, displayName, outputPath);

            BuiltinsExtensionHost.Instance.HideStatus(_creatingMessage);

            RaiseFormSubmit(new CreatedExtensionForm(extensionName, displayName, outputPath));
        }
        catch (Exception e)
        {
            _creatingMessage.State = MessageState.Error;
            _creatingMessage.Progress = null;
            _creatingMessage.Message = $"Error: {e.Message}";
        }

        return CommandResult.KeepOpen();
    }

    private string FormatJsonString(string str) =>

        // Escape the string for JSON
        JsonSerializer.Serialize(str, JsonSerializationContext.Default.String);
}
