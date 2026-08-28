// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IDetails> _detailsModel;
    private INotifyPropChanged? _observableDetails;
    private bool _isSubscribed;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public IconInfoViewModel HeroImage { get; private set; } = new(null);

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public ContentSize? Size { get; private set; } = ContentSize.Small;

    // Metadata is an array of IDetailsElement,
    //   where IDetailsElement = {IDetailsTags, IDetailsLink, IDetailsSeparator}
    public List<DetailsElementViewModel> Metadata { get; private set; } = [];

    public ObservableCollection<ContentViewModel> Content { get; } = [];

    public DetailsViewModel(IDetails details, WeakReference<IPageContext> context)
        : base(context)
    {
        _detailsModel = new(details);
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    private void FetchProperty(string propertyName)
    {
        var model = _detailsModel.Unsafe;
        if (model is null)
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(IDetails.Title):
                Title = model.Title ?? string.Empty;
                UpdateProperty(nameof(Title));
                break;
            case nameof(IDetails.Body):
                Body = model.Body ?? string.Empty;
                UpdateProperty(nameof(Body));
                break;
            case nameof(IDetails.HeroImage):
                HeroImage = new(model.HeroImage);
                HeroImage.InitializeProperties();
                UpdateProperty(nameof(HeroImage));
                break;
            case nameof(IDetails.Metadata):
                RebuildMetadata(model);
                UpdateProperty(nameof(Metadata));
                break;

            // here be dragons: IDetails2 exposes a method GetContent() to build
            // the content object. But the property change comes in under the name
            // "Content". So yes, this intentionally uses the toolkit's property name
            case nameof(Details.Content):
                RebuildContent(model);
                break;
        }
    }

    private void RebuildMetadata(IDetails model)
    {
        var newMetadata = new List<DetailsElementViewModel>();
        var meta = model.Metadata;
        if (meta is not null)
        {
            foreach (var element in meta)
            {
                DetailsElementViewModel? vm = element.Data switch
                {
                    IDetailsSeparator => new DetailsSeparatorViewModel(element, this.PageContext),
                    IDetailsLink => new DetailsLinkViewModel(element, this.PageContext),
                    IDetailsCommands => new DetailsCommandsViewModel(element, this.PageContext),
                    IDetailsTags => new DetailsTagsViewModel(element, this.PageContext),
                    _ => null,
                };
                if (vm is not null)
                {
                    vm.InitializeProperties();
                    newMetadata.Add(vm);
                }
            }
        }

        Metadata = newMetadata;
    }

    public override void InitializeProperties()
    {
        var model = _detailsModel.Unsafe;
        if (model is null)
        {
            return;
        }

        // Subscribe to PropChanged if the model supports it (only subscribe once)
        if (!_isSubscribed && model is INotifyPropChanged observable)
        {
            observable.PropChanged += Model_PropChanged;
            _observableDetails = observable;
            _isSubscribed = true;
        }

        Title = model.Title ?? string.Empty;
        Body = model.Body ?? string.Empty;
        HeroImage = new(model.HeroImage);
        HeroImage.InitializeProperties();

        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(Body));
        UpdateProperty(nameof(HeroImage));

        if (model is IExtendedAttributesProvider provider)
        {
            if (provider.GetProperties()?.TryGetValue("Size", out var rawValue) == true)
            {
                if (rawValue is int sizeAsInt)
                {
                    Size = (ContentSize)sizeAsInt;
                }
            }
        }

        Size ??= ContentSize.Small;

        UpdateProperty(nameof(Size));

        RebuildMetadata(model);
        RebuildContent(model);
    }

    private void RebuildContent(IDetails model)
    {
        List<ContentViewModel> content = [];
        if (model is IDetails2 details2)
        {
            foreach (var item in details2.GetContent())
            {
                var viewModel = CommandPaletteContentPageViewModel.CreateViewModel(item, PageContext);
                if (viewModel is not null)
                {
                    viewModel.InitializeProperties();
                    content.Add(viewModel);
                }
            }
        }

        // Now, back to a UI thread to update the observable collection
        DoOnUiThread(
            () =>
            {
                ListHelpers.InPlaceUpdateList(Content, content);
                UpdateProperty(nameof(Content));
            });
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        if (_isSubscribed && _observableDetails is not null)
        {
            _observableDetails.PropChanged -= Model_PropChanged;
            _observableDetails = null;
            _isSubscribed = false;
        }
    }
}
