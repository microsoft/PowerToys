// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class PageViewModel : ExtensionObjectViewModel
{
    protected TaskScheduler Scheduler { get; private set; }

    private readonly ExtensionObject<IPage> _pageModel;

    [ObservableProperty]
    public partial bool IsInitialized { get; private set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raisee the PropertyChanged
    // on the UI thread.
    public string Name { get; private set; } = string.Empty;

    public bool Loading { get; private set; } = true;

    public PageViewModel(IPage model, TaskScheduler scheduler)
    {
        _pageModel = new(model);
        Scheduler = scheduler;
    }

    //// Run on background thread from ListPage.xaml.cs
    [RelayCommand]
    private Task<bool> InitializeAsync()
    {
        // TODO: We may want a SemaphoreSlim lock here.

        // TODO: We may want to investigate using some sort of AsyncEnumerable or populating these as they come in to the UI layer
        //       Though we have to think about threading here and circling back to the UI thread with a TaskScheduler.
        try
        {
            InitializeProperties();
        }
        catch (Exception)
        {
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
        Loading = page.Loading;

        // Let the UI know about our initial properties too.
        UpdateProperty(nameof(Name));
        UpdateProperty(nameof(Loading));

        page.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, PropChangedEventArgs args)
    {
        try
        {
            var propName = args.PropertyName;
            FetchProperty(propName);
        }
        catch (Exception)
        {
            // TODO log? throw?
        }
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
                break;
            case nameof(Loading):
                this.Loading = model.Loading;
                break;
        }

        UpdateProperty(propertyName);
    }

    protected void UpdateProperty(string propertyName) => Task.Factory.StartNew(() => { OnPropertyChanged(propertyName); }, CancellationToken.None, TaskCreationOptions.None, Scheduler);

    protected void ShowException(Exception ex)
    {
        Task.Factory.StartNew(
            () =>
        {
            ErrorMessage = $"{ex.Message}\n{ex.Source}\n{ex.StackTrace}\n\nThis is due to a bug in the extension's code.";
            WeakReferenceMessenger.Default.Send<ShowExceptionMessage>(new(ex));
        },
            CancellationToken.None,
            TaskCreationOptions.None,
            Scheduler);
    }
}
