// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Templating;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Windows.Data.Json;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FormViewModel(IForm _form, IPageContext context) : ExtensionObjectViewModel(context)
{
    private readonly ExtensionObject<IForm> _formModel = new(_form);

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

        TemplateJson = model.TemplateJson();
        StateJson = model.StateJson();
        DataJson = model.DataJson();

        AdaptiveCardTemplate template = new(TemplateJson);
        var cardJson = template.Expand(DataJson);
        Card = AdaptiveCard.FromJsonString(cardJson);

        // TODO catch and replace with our error card
        UpdateProperty(nameof(Card));
    }

    public void HandleSubmit(IAdaptiveActionElement action, JsonObject inputs)
    {
        if (action is AdaptiveOpenUrlAction openUrlAction)
        {
            _ = Launcher.LaunchUriAsync(openUrlAction.Url);
            return;
        }

        if (action is AdaptiveSubmitAction or AdaptiveExecuteAction)
        {
            // Get the data and inputs
            // var data = submitAction.DataJson.Stringify();
            var inputString = inputs.Stringify();

            // _ = data;
            _ = inputString;

            try
            {
                var model = _formModel.Unsafe!;
                if (model != null)
                {
                    var result = model.SubmitForm(inputString);

                    // TODO Handle results
                }
            }
            catch (Exception ex)
            {
                PageContext.ShowException(ex);
            }
        }
    }
}
