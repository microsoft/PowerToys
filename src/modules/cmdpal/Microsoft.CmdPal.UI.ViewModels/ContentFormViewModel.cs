// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AdaptiveCards.ObjectModel.WinUI3;
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

        TemplateJson = model.TemplateJson;
        StateJson = model.StateJson;
        DataJson = model.DataJson;

        try
        {
            // Parse the template and data JSON
            var templateJsonObject = JsonObject.Parse(TemplateJson);
            var dataJsonObject = JsonObject.Parse(DataJson);

            // Manually replace placeholders in the template with data
            foreach (var key in dataJsonObject.Keys)
            {
                if (templateJsonObject.ContainsKey(key))
                {
                    templateJsonObject[key] = dataJsonObject[key];
                }
            }

            // Serialize the modified template back to a JSON string
            var cardJson = templateJsonObject.Stringify();

            // Construct the AdaptiveCard
            Card = AdaptiveCard.FromJsonString(cardJson);
        }
        catch (Exception e)
        {
            // Handle errors (similar to your existing error handling)
            var serializeString = (string? s) => JsonSerializer.Serialize(s, JsonSerializationContext.Default.String);

            var dataJson = $$"""  
                {  
                   "error_message": {{serializeString(e.Message)}},  
                   "error_stack": {{serializeString(e.StackTrace)}},  
                   "inner_exception": {{serializeString(e.InnerException?.Message)}},  
                   "template_json": {{serializeString(TemplateJson)}},  
                   "data_json": {{serializeString(DataJson)}}  
                }  
                """;

            // Directly construct the error card JSON string
            var errorCardJson = $$"""  
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
                           "text": {{serializeString(e.Message)}},  
                           "color": "Attention"  
                       },  
                       {  
                           "type": "TextBlock",  
                           "text": {{serializeString(e.StackTrace)}},  
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
                           "text": {{serializeString(e.InnerException?.Message)}},  
                           "color": "Attention"  
                       }  
                   ]  
                }  
                """;

            // Create the AdaptiveCard object from the JSON string
            Card = AdaptiveCard.FromJsonString(errorCardJson);
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
}
