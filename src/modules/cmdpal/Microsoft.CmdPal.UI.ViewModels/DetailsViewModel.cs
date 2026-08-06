// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

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
    public List<DetailsElementViewModel> Metadata => _metadata;

    private List<DetailsElementViewModel> _metadata = [];

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
        }
    }

    private void RebuildMetadata(IDetails model)
    {
        var newMetadata = new List<DetailsElementViewModel>();
        var transferred = false;

        try
        {
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
                        // Track first, so we can clean up if we fail to transfer all elements
                        newMetadata.Add(vm);
                        vm.InitializeProperties();
                    }
                }
            }

            ReplaceMetadata(newMetadata);
            transferred = true;
        }
        finally
        {
            // When failed half-way through, cleanup up new VMs.
            if (!transferred)
            {
                foreach (var vm in newMetadata)
                {
                    vm.SafeCleanup();
                }
            }
        }
    }

    /// <summary>
    /// Replaces <see cref="Metadata"/> with a newly built list, cleaning up the elements being dropped.
    /// </summary>
    private void ReplaceMetadata(List<DetailsElementViewModel> metadata)
    {
        var replaced = Interlocked.Exchange(ref _metadata, metadata);

        if (ReferenceEquals(replaced, metadata))
        {
            return;
        }

        foreach (var element in replaced)
        {
            element.SafeCleanup();
        }
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

        ReplaceMetadata([]);
    }
}
