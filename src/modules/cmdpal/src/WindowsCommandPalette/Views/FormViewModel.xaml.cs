// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using AdaptiveCards.Templating;
using DeveloperCommandPalette;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.System;

namespace WindowsCommandPalette.Views;

public sealed class FormViewModel : INotifyPropertyChanged
{
    private readonly IForm _form;
    private AdaptiveCardParseResult? _card;
    private RenderedAdaptiveCard? _renderedAdaptiveCard;
    private string _templateJson = "{}";
    private string _dataJson = "{}";

    public event TypedEventHandler<object, SubmitFormArgs>? RequestSubmitForm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool ShouldDisplay => _renderedAdaptiveCard?.FrameworkElement != null;

    public FrameworkElement? RenderedChildElement => _renderedAdaptiveCard?.FrameworkElement;

    public FormViewModel(IForm form)
    {
        this._form = form;
    }

    internal void InitialRender()
    {
        // var t = new Task<bool>(() => {
        this._templateJson = this._form.TemplateJson();

        try
        {
            this._dataJson = this._form.DataJson();
        }
        catch (Exception)
        {
            this._dataJson = "{}";
        }

        // return true;
        // });
        // t.Start();
        // await t;
        AdaptiveCardTemplate template = new(_templateJson);
        var cardJson = template.Expand(_dataJson);
        this._card = AdaptiveCard.FromJsonString(cardJson);
    }

    internal void RenderToXaml(AdaptiveCardRenderer renderer)
    {
        if (this._card != null)
        {
            _renderedAdaptiveCard = renderer.RenderAdaptiveCard(_card.AdaptiveCard);
            _renderedAdaptiveCard.Action += RenderedAdaptiveCard_Action;

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
            handlers?.Invoke(this, new() { FormData = inputs, Form = _form });
        }
    }
}
