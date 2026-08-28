// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IDetails> _detailsModel;
    private readonly ContentCollectionViewModel _content;
    private readonly Lock _lifetimeGate = new();
    private int _presentationReferences;
    private bool _cleanupRequested;
    private INotifyPropChanged? _observableDetails;
    private bool _initialized;
    private volatile bool _stopped;
    private ContentHeaderViewModel? _header;

    public IconInfoViewModel HeroImage { get; private set; } = new(null);

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public ContentSize? Size { get; private set; } = ContentSize.Small;

    public List<DetailsElementViewModel> Metadata { get; private set; } = [];

    public ObservableCollection<ContentViewModel> Content => _content.Items;

    public bool IsContentOnly { get; }

    public bool IsLegacy => !IsContentOnly;

    public DetailsViewModel(IDetails details, WeakReference<IPageContext> context)
        : base(context)
    {
        _detailsModel = new(details);
        _content = new(context);
        _content.Updated += Content_Updated;
        IsContentOnly = details is IDetails2;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        if (_stopped)
        {
            return;
        }

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

        if (IsContentOnly)
        {
            // GetContent is announced using the toolkit's Content property name.
            if (propertyName == nameof(ContentDetails.Content))
            {
                RebuildContent((IDetails2)model);
            }

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
                break;
        }
    }

    private void RebuildMetadata(IDetails model)
    {
        var next = new List<DetailsElementViewModel>();
        try
        {
            foreach (var element in model.Metadata ?? [])
            {
                DetailsElementViewModel? vm = element.Data switch
                {
                    IDetailsSeparator => new DetailsSeparatorViewModel(element, PageContext),
                    IDetailsLink => new DetailsLinkViewModel(element, PageContext),
                    IDetailsCommands => new DetailsCommandsViewModel(element, PageContext),
                    IDetailsTags => new DetailsTagsViewModel(element, PageContext),
                    _ => null,
                };
                if (vm is not null)
                {
                    next.Add(vm);
                    vm.InitializeProperties();
                }
            }
        }
        catch
        {
            foreach (var item in next)
            {
                item.SafeCleanup();
            }

            throw;
        }

        var previous = Metadata;
        Metadata = next;
        foreach (var item in previous)
        {
            item.SafeCleanup();
        }

        UpdateProperty(nameof(Metadata));
    }

    public override void InitializeProperties()
    {
        if (_initialized || _stopped || _detailsModel.Unsafe is not { } model)
        {
            return;
        }

        _initialized = true;
        try
        {
            if (model is INotifyPropChanged observable)
            {
                _observableDetails = observable;
                observable.PropChanged += Model_PropChanged;
            }

            if (model is IExtendedAttributesProvider provider &&
                provider.GetProperties()?.TryGetValue("Size", out var rawValue) == true &&
                rawValue is int sizeAsInt)
            {
                Size = (ContentSize)sizeAsInt;
            }

            UpdateProperty(nameof(Size));

            if (model is IDetails2 contentDetails)
            {
                RebuildContent(contentDetails);
            }
            else
            {
                Title = model.Title ?? string.Empty;
                Body = model.Body ?? string.Empty;
                HeroImage = new(model.HeroImage);
                HeroImage.InitializeProperties();
                UpdateProperty(nameof(Title), nameof(Body), nameof(HeroImage));
                RebuildMetadata(model);
            }
        }
        catch
        {
            SafeCleanup();
            throw;
        }
    }

    private void RebuildContent(IDetails2 model) => _content.Update(model.GetContent());

    private void Content_Updated(object? sender, EventArgs e)
    {
        if (_stopped)
        {
            return;
        }

        DetachHeader();
        _header = Content.OfType<ContentHeaderViewModel>().FirstOrDefault();
        if (_header is not null)
        {
            _header.PropertyChanged += Header_PropertyChanged;
        }

        UpdateHeaderAccessibility();
    }

    private void Header_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_stopped && e.PropertyName is nameof(ContentHeaderViewModel.Title) or nameof(ContentHeaderViewModel.Subtitle))
        {
            UpdateHeaderAccessibility();
        }
    }

    private void UpdateHeaderAccessibility()
    {
        // The landmark follows the rendered header, without fetching legacy fields.
        Title = _header?.Title ?? string.Empty;
        Body = _header?.Subtitle ?? string.Empty;
        UpdateProperty(nameof(Title), nameof(Body));
    }

    private void DetachHeader()
    {
        if (_header is not null)
        {
            _header.PropertyChanged -= Header_PropertyChanged;
            _header = null;
        }
    }

    // A page can release its Details while the shell still displays them during
    // its debounce. Keep the observers alive until the last pane has detached.
    internal IDisposable? TryAcquirePresentation()
    {
        lock (_lifetimeGate)
        {
            if (_stopped)
            {
                return null;
            }

            _presentationReferences++;
            return new PresentationReference(this);
        }
    }

    private void ReleasePresentation()
    {
        lock (_lifetimeGate)
        {
            _presentationReferences--;
            if (_presentationReferences != 0 || !_cleanupRequested || _stopped)
            {
                return;
            }

            _stopped = true;
        }

        // The shell releases its reference on the UI thread. Revoke extension
        // events in the background, after the bindings have detached.
        _ = Task.Run(base.SafeCleanup);
    }

    public override void SafeCleanup()
    {
        lock (_lifetimeGate)
        {
            _cleanupRequested = true;
            if (_stopped || _presentationReferences > 0)
            {
                return;
            }

            _stopped = true;
        }

        // Event revocation can call extension code. Never do it under the gate.
        base.SafeCleanup();
    }

    private sealed partial class PresentationReference(DetailsViewModel owner) : IDisposable
    {
        private DetailsViewModel? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleasePresentation();
    }

    protected override void UnsafeCleanup()
    {
        _content.Updated -= Content_Updated;
        _content.SafeCleanup();
        DoOnUiThread(DetachHeader);
        foreach (var item in Metadata)
        {
            item.SafeCleanup();
        }

        Metadata = [];
        if (_observableDetails is { } observable)
        {
            _observableDetails = null;
            observable.PropChanged -= Model_PropChanged;
        }

        base.UnsafeCleanup();
    }
}
