// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Host view model for an extension <see cref="ITabbedPage"/>. It renders a strip
/// of tabs where each tab is its own independent <see cref="IPage"/>
/// (a list, dynamic list or content page in v1).
/// </summary>
/// <remarks>
/// The tabbed page and its <see cref="ITab"/> metadata (title/icon/badge) load
/// eagerly so the strip renders immediately. A tab's <em>page</em> is only turned
/// into a child <see cref="PageViewModel"/> the first time that tab is activated,
/// then cached until the whole tabbed page is disposed. The shared search box is
/// forwarded to the active tab; the bottom command bar is driven by the active
/// child exactly as if that page had been opened on its own.
/// </remarks>
public partial class TabbedPageViewModel : PageViewModel
{
    private readonly ExtensionObject<ITabbedPage> _model;
    private readonly IPageViewModelFactoryService _factory;
    private readonly Dictionary<string, PageViewModel> _childCache = [];

    private static readonly string _fallbackPlaceholder = "Type here to search...";

    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the tab strip should be shown. A tabbed
    /// page with a single tab still renders the strip so the badge/title are
    /// visible, but a page that produced no tabs hides it entirely.
    /// </summary>
    public bool HasTabs => Tabs.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnsupportedPlaceholder))]
    public partial TabViewModel? SelectedTab { get; set; }

    /// <summary>
    /// Gets the child page view model for the currently active tab, or
    /// <see langword="null"/> when the active tab hosts an unsupported page type
    /// (in which case the host shows a placeholder).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnsupportedPlaceholder))]
    public partial PageViewModel? ActiveChild { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the host should show the "unsupported
    /// tab" placeholder, which happens when a tab is selected but its page type
    /// can't be rendered in v1.
    /// </summary>
    public bool ShowUnsupportedPlaceholder => SelectedTab is not null && ActiveChild is null;

    /// <summary>
    /// Gets a value indicating whether the active tab's page is still loading.
    /// This drives the in-host progress bar that sits beneath the tab strip, and
    /// is distinct from the shell-level progress bar that reflects the tabbed
    /// page loading its own tab set.
    /// </summary>
    [ObservableProperty]
    public partial bool ActiveTabIsLoading { get; private set; }

    /// <inheritdoc/>
    public override string PlaceholderText => ActiveChild?.PlaceholderText ?? _fallbackPlaceholder;

    public TabbedPageViewModel(ITabbedPage model, TaskScheduler scheduler, AppExtensionHost host, ICommandProviderContext providerContext, IPageViewModelFactoryService factory)
        : base(model, scheduler, host, providerContext)
    {
        _model = new(model);
        _factory = factory;
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        var newTabs = BuildTabViewModels(model.GetTabs());

        // The shared search box stays visible for the whole tabbed page as long
        // as any tab supports search; it is inert on non-searchable tabs.
        HasSearchBox = AnyTabSearchable(newTabs);
        UpdateProperty(nameof(HasSearchBox));

        DoOnUiThread(() =>
        {
            ListHelpers.InPlaceUpdateList(Tabs, newTabs);
            UpdateProperty(nameof(HasTabs));

            // First tab is the default active tab.
            SelectedTab = Tabs.Count > 0 ? Tabs[0] : null;
        });

        model.ItemsChanged += Model_ItemsChanged;
    }

    private List<TabViewModel> BuildTabViewModels(ITab[]? tabs)
    {
        List<TabViewModel> result = [];
        if (tabs is null)
        {
            return result;
        }

        foreach (var tab in tabs)
        {
            if (tab is null)
            {
                continue;
            }

            var vm = new TabViewModel(tab, PageContext);
            vm.InitializeProperties();
            result.Add(vm);
        }

        return result;
    }

    private bool AnyTabSearchable(IEnumerable<TabViewModel> tabs)
    {
        foreach (var tab in tabs)
        {
            if (tab.Page is IListPage)
            {
                return true;
            }
        }

        return false;
    }

    //// Dynamic tab set: re-read GetTabs() and preserve the active tab by identity ////
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args)
    {
        try
        {
            var model = _model.Unsafe;
            if (model is null)
            {
                return;
            }

            var newTabs = BuildTabViewModels(model.GetTabs());
            var searchable = AnyTabSearchable(newTabs);
            var activeId = SelectedTab?.TabId;

            DoOnUiThread(() =>
            {
                // Drop cached children for tabs that no longer exist.
                var keepIds = new HashSet<string>(newTabs.Select(t => t.TabId));
                var staleIds = _childCache.Keys.Where(id => !keepIds.Contains(id)).ToList();
                foreach (var id in staleIds)
                {
                    DisposeChild(_childCache[id]);
                    _childCache.Remove(id);
                }

                foreach (var old in Tabs)
                {
                    old.SafeCleanup();
                }

                ListHelpers.InPlaceUpdateList(Tabs, newTabs);
                HasSearchBox = searchable;
                UpdateProperty(nameof(HasSearchBox));
                UpdateProperty(nameof(HasTabs));

                // Preserve the active tab when it still exists, else fall back to
                // the first tab.
                TabViewModel? next = null;
                if (!string.IsNullOrEmpty(activeId))
                {
                    next = Tabs.FirstOrDefault(t => t.TabId == activeId);
                }

                next ??= Tabs.Count > 0 ? Tabs[0] : null;

                if (ReferenceEquals(next, SelectedTab))
                {
                    // Selection object is unchanged; make sure the content still
                    // reflects it (the underlying page instance may be new).
                    ActivateTab(next);
                }
                else
                {
                    SelectedTab = next;
                }
            });
        }
        catch (Exception ex)
        {
            ShowException(ex, _model?.Unsafe?.Name);
        }
    }

    partial void OnSelectedTabChanged(TabViewModel? oldValue, TabViewModel? newValue) => ActivateTab(newValue);

    private void ActivateTab(TabViewModel? tab)
    {
        DetachActiveChildLoading(ActiveChild);

        if (tab is null)
        {
            ActiveChild = null;
            ActiveTabIsLoading = false;
            UpdateProperty(nameof(PlaceholderText));
            return;
        }

        var child = GetOrCreateChild(tab);
        ActiveChild = child;

        if (child is null)
        {
            // Unsupported page type; the host renders a placeholder for this tab.
            ActiveTabIsLoading = false;
            UpdateProperty(nameof(PlaceholderText));
            return;
        }

        AttachActiveChildLoading(child);
        ActiveTabIsLoading = child.IsLoading;

        // Forward the shared search text to a searchable child so the active
        // tab reflects the current query.
        if (child.HasSearchBox)
        {
            child.SearchTextBox = SearchTextBox;
        }

        UpdateProperty(nameof(PlaceholderText));
    }

    private PageViewModel? GetOrCreateChild(TabViewModel tab)
    {
        if (_childCache.TryGetValue(tab.TabId, out var cached))
        {
            return cached;
        }

        var page = tab.Page;
        if (page is null)
        {
            return null;
        }

        var child = _factory.TryCreatePageViewModel(page, true, ExtensionHost, ProviderContext);
        if (child is null)
        {
            // Unsupported page type; cache nothing and let the host show a
            // placeholder.
            return null;
        }

        child.IsRootPage = false;
        child.HasBackButton = false;
        _childCache[tab.TabId] = child;

        InitializeChild(child);
        return child;
    }

    private void InitializeChild(PageViewModel child)
    {
        // Mirror ShellViewModel.LoadPageViewModelAsync: initialize on a
        // background thread so the tab strip stays responsive. The child view
        // model marshals IsInitialized/property updates back onto the UI thread.
        _ = Task.Run(() =>
        {
            try
            {
                child.InitializeCommand.Execute(null);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        });
    }

    private void AttachActiveChildLoading(PageViewModel child) => child.PropertyChanged += ActiveChild_PropertyChanged;

    private void DetachActiveChildLoading(PageViewModel? child)
    {
        if (child is not null)
        {
            child.PropertyChanged -= ActiveChild_PropertyChanged;
        }
    }

    private void ActiveChild_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not PageViewModel child || !ReferenceEquals(child, ActiveChild))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(IsLoading):
                ActiveTabIsLoading = child.IsLoading;
                break;
            case nameof(PlaceholderText):
                UpdateProperty(nameof(PlaceholderText));
                break;
        }
    }

    protected override void OnSearchTextBoxUpdated(string searchTextBox)
    {
        // Forward the shared query to the active tab when it supports search;
        // otherwise the box is inert for this tab.
        if (ActiveChild is { HasSearchBox: true } child)
        {
            child.SearchTextBox = searchTextBox;
        }
    }

    private void DisposeChild(PageViewModel child)
    {
        DetachActiveChildLoading(child);
        child.SafeCleanup();
        if (child is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }

        DetachActiveChildLoading(ActiveChild);
        ActiveChild = null;

        foreach (var child in _childCache.Values)
        {
            DetachActiveChildLoading(child);
            child.SafeCleanup();
            if (child is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _childCache.Clear();

        foreach (var tab in Tabs)
        {
            tab.SafeCleanup();
        }
    }
}
