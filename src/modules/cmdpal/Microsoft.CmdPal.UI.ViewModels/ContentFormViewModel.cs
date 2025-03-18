// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Templating;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
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

    public override void InitializeProperties()
    {
        var model = _formModel.Unsafe;
        if (model == null)
        {
            return;
        }

        try
        {
            TemplateJson = model.TemplateJson;
            StateJson = model.StateJson;
            DataJson = model.DataJson;

            AdaptiveCardTemplate template = new(TemplateJson);
            var cardJson = template.Expand(DataJson);
            Card = AdaptiveCard.FromJsonString(cardJson);
        }
        catch (Exception e)
        {
            // If we fail to parse the card JSON, then display _our own card_
            // with the exception
            AdaptiveCardTemplate template = new(ErrorCardJson);

            // todo: we could probably stick Card.Errors in there too
            var dataJson = $$"""
{
    "error_message": {{JsonSerializer.Serialize(e.Message)}},
    "error_stack": {{JsonSerializer.Serialize(e.StackTrace)}},
    "inner_exception": {{JsonSerializer.Serialize(e.InnerException?.Message)}},
    "template_json": {{JsonSerializer.Serialize(TemplateJson)}},
    "data_json": {{JsonSerializer.Serialize(DataJson)}}
}
""";
            var cardJson = template.Expand(dataJson);
            Card = AdaptiveCard.FromJsonString(cardJson);
        }

        UpdateProperty(nameof(Card));
    }

    public void HandleSubmit(IAdaptiveActionElement action, JsonObject inputs)
    {
        if (action is AdaptiveOpenUrlAction openUrlAction)
        {
            WeakReferenceMessenger.Default.Send<LaunchUriMessage>(new(openUrlAction.Url));
            return;
        }

        if (action is AdaptiveSubmitAction or AdaptiveExecuteAction)
        {
            // Get the data and inputs
            var dataString = (action as AdaptiveSubmitAction)?.DataJson.Stringify() ?? string.Empty;
            var inputString = inputs.Stringify();

            _ = Task.Run(() =>
            {
                try
                {
                    var model = _formModel.Unsafe!;
                    if (model != null)
                    {
                        var result = model.SubmitForm(inputString, dataString);
                        WeakReferenceMessenger.Default.Send<HandleCommandResultMessage>(new(new(result)));
                    }
                }
                catch (Exception ex)
                {
                    ShowException(ex);
                }
            });
        }
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
