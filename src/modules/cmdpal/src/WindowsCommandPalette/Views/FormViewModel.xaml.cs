// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using AdaptiveCards.Templating;
using DeveloperCommandPalette;
using Microsoft.UI.Xaml;
using Microsoft.CmdPal.Extensions;
using Windows.Foundation;
using Windows.System;

namespace WindowsCommandPalette.Views;

public sealed class FormViewModel : System.ComponentModel.INotifyPropertyChanged
{
    internal readonly IForm form;
    internal AdaptiveCardParseResult? card;
    internal RenderedAdaptiveCard? RenderedAdaptiveCard;
    internal string TemplateJson = "{}";
    internal string DataJson = "{}";

    public event TypedEventHandler<object, SubmitFormArgs>? RequestSubmitForm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool ShouldDisplay => RenderedAdaptiveCard?.FrameworkElement != null;

    public FrameworkElement? RenderedChildElement => RenderedAdaptiveCard?.FrameworkElement;

    public FormViewModel(IForm form)
    {
        this.form = form;
    }

    internal void InitialRender()
    {
        // var t = new Task<bool>(() => {
        this.TemplateJson = this.form.TemplateJson();
        try
        {
            this.DataJson = this.form.DataJson();
        }
        catch (Exception)
        {
            this.DataJson = "{}";
        }

        // return true;
        // });
        // t.Start();
        // await t;
        AdaptiveCardTemplate template = new(TemplateJson);
        var cardJson = template.Expand(DataJson);
        this.card = AdaptiveCard.FromJsonString(cardJson);
    }

    internal void RenderToXaml(AdaptiveCardRenderer renderer)
    {
        if (this.card != null)
        {
            RenderedAdaptiveCard = renderer.RenderAdaptiveCard(card.AdaptiveCard);
            RenderedAdaptiveCard.Action += RenderedAdaptiveCard_Action;

            var handlers = this.PropertyChanged;
            handlers?.Invoke(this, new PropertyChangedEventArgs(nameof(ShouldDisplay)));
            handlers?.Invoke(this, new PropertyChangedEventArgs(nameof(RenderedChildElement)));
        }
    }

    private async void RenderedAdaptiveCard_Action(RenderedAdaptiveCard sender, AdaptiveActionEventArgs args)
    {
        if (args.Action is AdaptiveOpenUrlAction openUrlAction)
        {
            await Launcher.LaunchUriAsync(openUrlAction.Url);
        }
        else if (args.Action is AdaptiveSubmitAction submitAction)
        {
            // Get the data and inputs
            var data = submitAction.DataJson.Stringify();
            var inputs = args.Inputs.AsJson().Stringify();
            _ = data;
            _ = inputs;

            // Process them as desired
            var handlers = RequestSubmitForm;
            handlers?.Invoke(this, new() { FormData = inputs, Form = form });
        }
    }
}
