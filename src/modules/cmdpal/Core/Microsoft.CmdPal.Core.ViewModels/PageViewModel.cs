// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class PageViewModel : ExtensionObjectViewModel, IPageContext
{
    public TaskScheduler Scheduler { get; private set; }

    private readonly ExtensionObject<IPage> _pageModel;

    public bool IsLoading => ModelIsLoading || (!IsInitialized);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    public virtual partial bool IsInitialized { get; protected set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; protected set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsNested { get; set; } = true;

    // This is set from the SearchBar
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSuggestion))]
    public partial string SearchTextBox { get; set; } = string.Empty;

    [ObservableProperty]
    public virtual partial string PlaceholderText { get; private set; } = "Type here to search...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSuggestion))]
    public virtual partial string TextToSuggest { get; protected set; } = string.Empty;

    public bool ShowSuggestion => !string.IsNullOrEmpty(TextToSuggest) && TextToSuggest != SearchTextBox;

    [ObservableProperty]
    public partial AppExtensionHost ExtensionHost { get; private set; }

    public bool HasStatusMessage => MostRecentStatusMessage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial StatusMessageViewModel? MostRecentStatusMessage { get; private set; } = null;

    public ObservableCollection<StatusMessageViewModel> StatusMessages => ExtensionHost.StatusMessages;

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Name { get; protected set; } = string.Empty;

    public string Title { get => string.IsNullOrEmpty(field) ? Name : field; protected set; } = string.Empty;

    public string Id { get; protected set; } = string.Empty;

    // This property maps to `IPage.IsLoading`, but we want to expose our own
    // `IsLoading` property as a combo of this value and `IsInitialized`
    public bool ModelIsLoading { get; protected set; } = true;

    public bool HasSearchBox { get; protected set; } = true;

    public bool HasFilters { get; protected set; }

    public IconInfoViewModel Icon { get; protected set; }

    public PageViewModel(IPage? model, TaskScheduler scheduler, AppExtensionHost extensionHost)
        : base((IPageContext?)null)
    {
        _pageModel = new(model);
        Scheduler = scheduler;
        PageContext = new(this);
        ExtensionHost = extensionHost;
        Icon = new(null);

        ExtensionHost.StatusMessages.CollectionChanged += StatusMessages_CollectionChanged;
        UpdateHasStatusMessage();
    }

    private void StatusMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdateHasStatusMessage();

    private void UpdateHasStatusMessage()
    {
        if (ExtensionHost.StatusMessages.Any())
        {
            var last = ExtensionHost.StatusMessages.Last();
            MostRecentStatusMessage = last;
        }
        else
        {
            MostRecentStatusMessage = null;
        }
    }

    //// Run on background thread from ListPage.xaml.cs
    [RelayCommand]
    internal Task<bool> InitializeAsync()
    {
        // TODO: We may want a SemaphoreSlim lock here.

        // TODO: We may want to investigate using some sort of AsyncEnumerable or populating these as they come into the UI layer
        //       Though we have to think about threading here and circling back to the UI thread with a TaskScheduler.
        try
        {
            InitializeProperties();
        }
        catch (Exception ex)
        {
            ShowException(ex, _pageModel?.Unsafe?.Name);
            return Task.FromResult(false);
        }

        // Notify we're done back on the UI Thread.
        Task.Factory.StartNew(
            () =>
            {
                IsInitialized = true;

                // TODO: Do we want an event/signal here that the Page Views can listen to? (i.e. ListPage setting the selected index to 0, however, in async world the user may have already started navigating around page...)
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            Scheduler);
        return Task.FromResult(true);
    }

    public override void InitializeProperties()
    {
        var page = _pageModel.Unsafe;
        if (page is null)
        {
            return; // throw?
        }

        Id = page.Id;
        Name = page.Name;
        ModelIsLoading = page.IsLoading;
        Title = page.Title;
        Icon = new(page.Icon);
        Icon.InitializeProperties();

        HasSearchBox = page is IListPage;

        // Let the UI know about our initial properties too.
        UpdateProperty(nameof(Name));
        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(ModelIsLoading));
        UpdateProperty(nameof(IsLoading));
        UpdateProperty(nameof(Icon));
        UpdateProperty(nameof(HasSearchBox));

        page.PropChanged += Model_PropChanged;
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
            ShowException(ex, _pageModel?.Unsafe?.Name);
        }
    }

    partial void OnSearchTextBoxChanged(string oldValue, string newValue) => OnSearchTextBoxUpdated(newValue);

    protected virtual void OnSearchTextBoxUpdated(string searchTextBox)
    {
        // The base page has no notion of data, so we do nothing here...
        // subclasses should override.
    }

    protected virtual void FetchProperty(string propertyName)
    {
        var model = this._pageModel.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        var updateProperty = true;
        switch (propertyName)
        {
            case nameof(Name):
                this.Name = model.Name ?? string.Empty;
                UpdateProperty(nameof(Title));
                break;
            case nameof(Title):
                this.Title = model.Title ?? string.Empty;
                break;
            case nameof(IsLoading):
                this.ModelIsLoading = model.IsLoading;
                UpdateProperty(nameof(ModelIsLoading));
                break;
            case nameof(Icon):
                this.Icon = new(model.Icon);
                break;
            default:
                updateProperty = false;
                break;
        }

        // GH #38829: If we always UpdateProperty here, then there's a possible
        // race condition, where we raise the PropertyChanged(SearchText)
        // before the subclass actually retrieves the new SearchText from the
        // model. In that race situation, if the UI thread handles the
        // PropertyChanged before ListViewModel fetches the SearchText, it'll
        // think that the old search text is the _new_ value.
        if (updateProperty)
        {
            UpdateProperty(propertyName);
        }
    }

    public new void ShowException(Exception ex, string? extensionHint = null)
    {
        // Set the extensionHint to the Page Title (if we have one, and one not provided).
        // extensionHint ??= _pageModel?.Unsafe?.Title;
        extensionHint ??= ExtensionHost.GetExtensionDisplayName() ?? Title;
        Task.Factory.StartNew(
            () =>
            {
                var message = DiagnosticsHelper.BuildExceptionMessage(ex, extensionHint);
                ErrorMessage += message;
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            Scheduler);
    }

    public override string ToString() => $"{Title} ViewModel";

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        ExtensionHost.StatusMessages.CollectionChanged -= StatusMessages_CollectionChanged;

        var model = _pageModel.Unsafe;
        if (model is not null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }
}

public interface IPageContext
{
    void ShowException(Exception ex, string? extensionHint = null);

    TaskScheduler Scheduler { get; }
}

public interface IPageViewModelFactoryService
{
    /// <summary>
    /// Creates a new instance of the page view model for the given page type.
    /// </summary>
    /// <param name="page">The page for which to create the view model.</param>
    /// <param name="nested">Indicates whether the page is not the top-level page.</param>
    /// <param name="host">The command palette host that will host the page (for status messages)</param>
    /// <returns>A new instance of the page view model.</returns>
    PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host);
}
