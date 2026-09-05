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
    private readonly ViewModelLifetime _lifetime = new();
    private INotifyPropChanged? _observableDetails;
    private bool _initialized;
    private List<ContentViewModel> _ownedContent = [];

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

    internal bool Represents(IDetails? model) => ReferenceEquals(_detailsModel.Unsafe, model);

    internal bool IsObservable => _observableDetails is not null;

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var propertyName = args.PropertyName;
            _lifetime.Run(() => FetchProperty(propertyName));
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
            case nameof(Details.Size):
                UpdateSize(model);
                break;
        }
    }

    private void RebuildMetadata(IDetails model)
    {
        var newMetadata = new List<DetailsElementViewModel>();
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
                        newMetadata.Add(vm);
                        vm.InitializeProperties();
                    }
                }
            }
        }
        catch
        {
            newMetadata.ForEach(vm => vm.SafeCleanup());
            throw;
        }

        var previous = Metadata;
        Metadata = newMetadata;
        previous.ForEach(vm => vm.SafeCleanup());
    }

    public override void InitializeProperties() => _lifetime.Run(InitializeDetails);

    private void InitializeDetails()
    {
        if (_initialized && _observableDetails is not null)
        {
            return;
        }

        var model = _detailsModel.Unsafe;
        if (model is null)
        {
            return;
        }

        try
        {
            if (_observableDetails is null && model is INotifyPropChanged observable)
            {
                observable.PropChanged += Model_PropChanged;
                _observableDetails = observable;
            }

            Title = model.Title ?? string.Empty;
            Body = model.Body ?? string.Empty;
            HeroImage = new(model.HeroImage);
            HeroImage.InitializeProperties();

            UpdateProperty(nameof(Title), nameof(Body), nameof(HeroImage));
            UpdateSize(model);
            RebuildMetadata(model);
            UpdateProperty(nameof(Metadata));
            RebuildContent(model);
            _initialized = true;
        }
        catch
        {
            _lifetime.Close(CleanupDetails);
            throw;
        }
    }

    private void UpdateSize(IDetails model)
    {
        Size = ContentSize.Small;
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

        UpdateProperty(nameof(Size));
    }

    private void RebuildContent(IDetails model)
    {
        List<ContentViewModel> content = [];
        try
        {
            if (model is IDetails2 details2)
            {
                foreach (var item in details2.GetContent())
                {
                    var viewModel = CommandPaletteContentPageViewModel.CreateViewModel(item, PageContext);
                    if (viewModel is not null)
                    {
                        content.Add(viewModel);
                        viewModel.InitializeProperties();
                    }
                }
            }
        }
        catch
        {
            content.ForEach(vm => vm.SafeCleanup());
            throw;
        }

        var previous = _ownedContent;
        Volatile.Write(ref _ownedContent, content);
        previous.ForEach(vm => vm.SafeCleanup());

        DoOnUiThread(
            () =>
            {
                if (_lifetime.IsClosed || !ReferenceEquals(Volatile.Read(ref _ownedContent), content))
                {
                    return;
                }

                ListHelpers.InPlaceUpdateList(Content, content);
                UpdateProperty(nameof(Content));
            });
    }

    protected override void UnsafeCleanup() => _lifetime.Close(CleanupDetails);

    private void CleanupDetails()
    {
        var observable = _observableDetails;
        _observableDetails = null;
        _initialized = false;
        try
        {
            if (observable is not null)
            {
                observable.PropChanged -= Model_PropChanged;
            }
        }
        finally
        {
            Metadata.ForEach(vm => vm.SafeCleanup());
            Metadata = [];
            var previous = _ownedContent;
            Volatile.Write(ref _ownedContent, []);
            previous.ForEach(vm => vm.SafeCleanup());
            var empty = _ownedContent;
            DoOnUiThread(() =>
            {
                if (ReferenceEquals(Volatile.Read(ref _ownedContent), empty))
                {
                    Content.Clear();
                }
            });
        }
    }
}
