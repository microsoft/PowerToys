// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentImageViewModel : ContentViewModel
{
    public ExtensionObject<IImageContent> Model { get; }

    public IconInfoViewModel Image { get; protected set; } = new(null);

    public double MaxWidth { get; protected set; } = double.PositiveInfinity;

    public double MaxHeight { get; protected set; } = double.PositiveInfinity;

    public ContentImageViewModel(IImageContent content, WeakReference<IPageContext> context)
        : base(context)
    {
        Model = new ExtensionObject<IImageContent>(content);
    }

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        Image = new IconInfoViewModel(model.Image);
        Image.InitializeProperties();

        MaxWidth = model.MaxWidth <= 0 ? double.PositiveInfinity : model.MaxWidth;
        MaxHeight = model.MaxHeight <= 0 ? double.PositiveInfinity : model.MaxHeight;

        UpdateProperty(nameof(Image), nameof(MaxWidth), nameof(MaxHeight));
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
            case nameof(Image):
                Image = new IconInfoViewModel(model.Image);
                Image.InitializeProperties();
                UpdateProperty(propertyName);
                break;

            case nameof(IImageContent.MaxWidth):
                MaxWidth = model.MaxWidth <= 0 ? double.PositiveInfinity : model.MaxWidth;
                UpdateProperty(propertyName);
                break;

            case nameof(IImageContent.MaxHeight):
                MaxHeight = model.MaxHeight <= 0 ? double.PositiveInfinity : model.MaxHeight;
                UpdateProperty(propertyName);
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
