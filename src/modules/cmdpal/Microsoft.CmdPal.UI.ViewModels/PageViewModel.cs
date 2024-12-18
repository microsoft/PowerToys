// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class PageViewModel : ExtensionObjectViewModel, IPageContext
{
    public TaskScheduler Scheduler { get; private set; }

    private readonly ExtensionObject<IPage> _pageModel;

    [ObservableProperty]
    public partial bool IsInitialized { get; private set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsNested { get; set; } = true;

    // This is set from the SearchBar
    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Name { get; private set; } = string.Empty;

    public string Title { get => string.IsNullOrEmpty(field) ? Name : field; private set; } = string.Empty;

    public bool IsLoading { get; private set; } = true;

    public IconDataType Icon { get; private set; } = new(string.Empty);

    public PageViewModel(IPage model, TaskScheduler scheduler)
        : base(null)
    {
        _pageModel = new(model);
        Scheduler = scheduler;
        PageContext = this;
    }

    //// Run on background thread from ListPage.xaml.cs
    [RelayCommand]
    private Task<bool> InitializeAsync()
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
            ShowException(ex);
            return Task.FromResult(false);
        }

        IsInitialized = true;
        return Task.FromResult(true);
    }

    public override void InitializeProperties()
    {
        var page = _pageModel.Unsafe;
        if (page == null)
        {
            return; // throw?
        }

        Name = page.Name;
        IsLoading = page.IsLoading;
        Title = page.Title;
        Icon = page.Icon;

        // Let the UI know about our initial properties too.
        UpdateProperty(nameof(Name));
        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(IsLoading));
        UpdateProperty(nameof(Icon));

        page.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, PropChangedEventArgs args)
    {
        try
        {
            var propName = args.PropertyName;
            FetchProperty(propName);
        }
        catch (Exception e)
        {
            ShowException(e);
        }
    }

    partial void OnFilterChanged(string oldValue, string newValue) => OnFilterUpdated(newValue);

    protected virtual void OnFilterUpdated(string filter)
    {
        // The base page has no notion of data, so we do nothing here...
        // subclasses should override.
    }

    protected virtual void FetchProperty(string propertyName)
    {
        var model = this._pageModel.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

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
                this.IsLoading = model.IsLoading;
                break;
            case nameof(Icon):
                this.Icon = model.Icon;
                break;
        }

        UpdateProperty(propertyName);
    }

    public void ShowException(Exception ex)
    {
        Task.Factory.StartNew(
            () =>
        {
            ErrorMessage += $"{ex.Message}\n{ex.Source}\n{ex.StackTrace}\n\nThis is due to a bug in the extension's code.";
        },
            CancellationToken.None,
            TaskCreationOptions.None,
            Scheduler);
    }
}

public interface IPageContext
{
    public void ShowException(Exception ex);

    public TaskScheduler Scheduler { get; }
}
