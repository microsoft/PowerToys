// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Templating;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentFormViewModel(IFormContent _form, WeakReference<IPageContext> context) :
    ContentViewModel(context)
{
    private readonly ExtensionObject<IFormContent> _formModel = new(_form);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string TemplateJson { get; protected set; } = "{}";

    public string StateJson { get; protected set; } = "{}";

    public string DataJson { get; protected set; } = "{}";

    public AdaptiveCardParseResult? Card { get; private set; }

    private static string Serialize(string? s) =>
        JsonSerializer.Serialize(s, JsonSerializationContext.Default.String);

    private static bool TryBuildCard(
        string templateJson,
        string dataJson,
        out AdaptiveCardParseResult? card,
        out Exception? error)
    {
        card = null;
        error = null;

        try
        {
            var template = new AdaptiveCardTemplate(templateJson);
            var cardJson = template.Expand(dataJson);
            card = AdaptiveCard.FromJsonString(cardJson);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error building card from template: {Message}", ex.Message);
            error = ex;
            return false;
        }
    }

    public override void InitializeProperties()
    {
        var model = _formModel.Unsafe;
        if (model is null)
        {
            return;
        }

        TemplateJson = model.TemplateJson;
        StateJson = model.StateJson;
        DataJson = model.DataJson;

        if (TryBuildCard(TemplateJson, DataJson, out var builtCard, out var renderingError))
        {
            Card = builtCard;
            UpdateProperty(nameof(Card));
            return;
        }

        var errorPayload = $$"""
    {
        "error_message": {{Serialize(renderingError!.Message)}},
        "error_stack":   {{Serialize(renderingError.StackTrace)}},
        "inner_exception": {{Serialize(renderingError.InnerException?.Message)}},
        "template_json": {{Serialize(TemplateJson)}},
        "data_json":     {{Serialize(DataJson)}}
    }
    """;

        if (TryBuildCard(ErrorCardJson, errorPayload, out var errorCard, out var _))
        {
            Card = errorCard;
            UpdateProperty(nameof(Card));
            return;
        }

        UpdateProperty(nameof(Card));
    }

    public void HandleSubmit(IAdaptiveActionElement action, JsonObject inputs)
    {
        // BODGY circa GH #40979
        // Usually, you're supposed to try to cast the action to a specific
        // type, and use those objects to get the data you need.
        // However, there's something weird with AdaptiveCards and the way it
        // works when we consume it when built in Release, with AOT (and
        // trimming) enabled. Any sort of `action.As<IAdaptiveSubmitAction>()`
        // or similar will throw a System.InvalidCastException.
        //
        // Instead we have this horror show.
        //
        // The `action.ToJson()` blob ACTUALLY CONTAINS THE `type` field, which
        // we can use to determine what kind of action it is. Then we can parse
        // the JSON manually based on the type.
        var actionJson = action.ToJson();

        if (actionJson.TryGetValue("type", out var actionTypeValue))
        {
            var actionTypeString = actionTypeValue.GetString();
            Logger.LogTrace($"atString={actionTypeString}");

            var actionType = actionTypeString switch
            {
                "Action.Submit" => ActionType.Submit,
                "Action.Execute" => ActionType.Execute,
                "Action.OpenUrl" => ActionType.OpenUrl,
                _ => ActionType.Unsupported,
            };

            Logger.LogDebug($"{actionTypeString}->{actionType}");

            switch (actionType)
            {
                case ActionType.OpenUrl:
                    {
                        HandleOpenUrlAction(action, actionJson);
                    }

                    break;
                case ActionType.Submit:
                case ActionType.Execute:
                    {
                        HandleSubmitAction(action, actionJson, inputs);
                    }

                    break;
                default:
                    Logger.LogError($"{actionType} was an unexpected action `type`");
                    break;
            }
        }
        else
        {
            Logger.LogError($"actionJson.TryGetValue(type) failed");
        }
    }

    private void HandleOpenUrlAction(IAdaptiveActionElement action, JsonObject actionJson)
    {
        if (actionJson.TryGetValue("url", out var actionUrlValue))
        {
            var actionUrl = actionUrlValue.GetString() ?? string.Empty;
            if (Uri.TryCreate(actionUrl, default(UriCreationOptions), out var uri))
            {
                WeakReferenceMessenger.Default.Send<LaunchUriMessage>(new(uri));
            }
            else
            {
                Logger.LogError($"Failed to produce URI for {actionUrlValue}");
            }
        }
    }

    private void HandleSubmitAction(
        IAdaptiveActionElement action,
        JsonObject actionJson,
        JsonObject inputs)
    {
        var dataString = string.Empty;
        if (actionJson.TryGetValue("data", out var actionDataValue))
        {
            dataString = actionDataValue.Stringify() ?? string.Empty;
        }

        var inputString = inputs.Stringify();
        _ = Task.Run(() =>
        {
            try
            {
                var model = _formModel.Unsafe!;
                if (model != null)
                {
                    var result = model.SubmitForm(inputString, dataString);
                    Logger.LogDebug($"SubmitForm() returned {result}");
                    WeakReferenceMessenger.Default.Send<HandleCommandResultMessage>(new(new(result)));
                }
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        });
    }

    private static readonly string ErrorCardJson = """
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
            "type": "TextBlock",
            "text": "Error parsing form from extension",
            "wrap": true,
            "style": "heading",
            "size": "ExtraLarge",
            "weight": "Bolder",
            "color": "Attention"
        },
        {
            "type": "TextBlock",
            "wrap": true,
            "text": "${error_message}",
            "color": "Attention"
        },
        {
            "type": "TextBlock",
            "text": "${error_stack}",
            "fontType": "Monospace"
        },
        {
            "type": "TextBlock",
            "wrap": true,
            "text": "Inner exception:"
        },
        {
            "type": "TextBlock",
            "wrap": true,
            "text": "${inner_exception}",
            "color": "Attention"
        }
    ]
}
""";
}
