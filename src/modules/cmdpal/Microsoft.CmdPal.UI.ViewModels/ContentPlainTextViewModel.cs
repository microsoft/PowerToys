// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentPlainTextViewModel : ContentViewModel
{
    private ExtensionObject<IPlainTextContent> Model { get; }

    public string? Text { get; protected set; }

    public bool WordWrapEnabled { get; protected set; }

    public bool UseMonospace { get; protected set; }

    public ContentPlainTextViewModel(IPlainTextContent content, WeakReference<IPageContext> context)
        : base(context)
    {
        Model = new ExtensionObject<IPlainTextContent>(content);
    }

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        Text = model.Text;
        WordWrapEnabled = model.WrapWords;
        UseMonospace = model.FontFamily == FontFamily.Monospace;
        UpdateProperty(nameof(Text), nameof(WordWrapEnabled), nameof(UseMonospace));
        model.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var propName = args.PropertyName;
            FetchProperty(propName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    private void FetchProperty(string propertyName)
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(IPlainTextContent.FontFamily):
                // RPC:
                var incomingUseMonospace = model.FontFamily == FontFamily.Monospace;

                // local:
                if (incomingUseMonospace != UseMonospace)
                {
                    UseMonospace = incomingUseMonospace;
                    UpdateProperty(nameof(UseMonospace));
                }

                break;

            case nameof(IPlainTextContent.WrapWords):
                // RPC:
                var incomingWrap = model.WrapWords;

                // local:
                if (WordWrapEnabled != incomingWrap)
                {
                    WordWrapEnabled = model.WrapWords;
                    UpdateProperty(nameof(WordWrapEnabled));
                }

                break;

            case nameof(IPlainTextContent.Text):
                // RPC:
                var incomingText = model.Text;

                // local:
                if (incomingText != Text)
                {
                    Text = incomingText;
                    UpdateProperty(nameof(Text));
                }

                break;
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();
        var model = Model.Unsafe;
        if (model is not null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }
}
