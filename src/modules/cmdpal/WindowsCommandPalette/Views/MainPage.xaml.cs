// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.Extensions;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace WindowsCommandPalette.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private string _log = string.Empty;

    public MainViewModel ViewModel { get; } = new MainViewModel();

    public MainPage()
    {
        this.InitializeComponent();
        this.ViewModel.SummonRequested += ViewModel_SummonRequested;
        var rootListVm = new ListPageViewModel(new MainListPage(ViewModel));
        InitializePage(rootListVm);

        // TODO! make this async: it was originally on Page_Loaded and was async from there
        // LoadAllCommands().Wait();
        LoadBuiltinCommandsAsync().Wait();

        var extensionService = Application.Current.GetService<IExtensionService>();
        if (extensionService != null)
        {
            extensionService.OnExtensionsChanged += ExtensionService_OnExtensionsChanged;
        }

        _ = LoadExtensions();

        RootFrame.Navigate(typeof(ListPage), rootListVm, new DrillInNavigationTransitionInfo());
    }

    private void ExtensionService_OnExtensionsChanged(object? sender, EventArgs e)
    {
        _ = LoadAllCommands();
    }

    private void HackyBadClearFilter()
    {
        // BODGY but I don't care, cause i'm throwing this all out
        if ((this.RootFrame.Content as Page)?.FindName("FilterBox") is TextBox tb)
        {
            tb.Text = string.Empty;
            tb.Focus(FocusState.Programmatic);
        }

        _ = LoadAllCommands();
    }

    private void ViewModel_SummonRequested(object sender, object? args)
    {
        if (!RootFrame.CanGoBack)
        {
            HackyBadClearFilter();
        }
    }

    private async Task LoadAllCommands()
    {
        ViewModel.ResetTopLevel();

        // Load builtins synchronously...
        await LoadBuiltinCommandsAsync();

        // ...and extensions on a fire_and_forget
        _ = LoadExtensions();
    }

    public async Task LoadBuiltinCommandsAsync()
    {
        // Load commands from builtins
        // TODO! I don't understand async enough to get why this has to be ConfigureAwait(false)
        foreach (var provider in ViewModel.BuiltInCommands)
        {
            var wrapper = new CommandProviderWrapper(provider);
            ViewModel.ActionsProvider.Add(wrapper);

            await LoadTopLevelCommandsFromProvider(wrapper).ConfigureAwait(false);
        }
    }

    private void InitializePage(PageViewModel vm)
    {
        _log += "i'm just doing this so I don't get fined";
        vm.RequestDoAction += InvokeActionHandler;
        vm.RequestSubmitForm += SubmitFormHandler;
        vm.RequestGoBack += RequestGoBackHandler;
    }

    private void InvokeActionHandler(object sender, ActionViewModel args)
    {
        var action = args.Command;
        ViewModel.PushRecentAction(action);
        TryAllowForeground(action);
        if (action is IInvokableCommand invokable)
        {
            HandleResult(invokable.Invoke());
            return;
        }
        else if (action is IListPage listPage)
        {
            GoToList(listPage);
            return;
        }
        else if (action is IMarkdownPage mdPage)
        {
            GoToMarkdown(mdPage);
            return;
        }
        else if (action is IFormPage formPage)
        {
            GoToForm(formPage);
            return;
        }

        // This is bad
        // TODO! handle this with some sort of badly authored extension error
        throw new NotImplementedException();
    }

    private void TryAllowForeground(ICommand action)
    {
        foreach (var provider in ViewModel.ActionsProvider)
        {
            if (!provider.IsExtension)
            {
                continue;
            }

            foreach (var item in provider.TopLevelItems)
            {
                // TODO! We really need a better "SafeWrapper<T>" object that can make sure
                // that an extension object is alive when we call things on it.
                // Case in point: this. If the extension was killed while we're open, then
                // COM calls on it crash (and then we just do nothing)
                try
                {
                    if (action == item.Command)
                    {
                        provider.AllowSetForeground(true);
                        return;
                    }
                }
                catch (COMException e)
                {
                    AppendLog(e.Message);
                }
            }
        }
    }

    private void SubmitFormHandler(object sender, SubmitFormArgs args)
    {
        var formData = args.FormData;
        var form = args.Form;
        var result = form.SubmitForm(formData);
        HandleResult(result);
    }

    private void HandleResult(ICommandResult? res)
    {
        if (res == null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (res.Kind == CommandResultKind.Dismiss)
            {
                DoGoHome();
                ViewModel.RequestHide();
            }
            else if (res.Kind == CommandResultKind.GoHome)
            {
                DoGoHome();
            }
        });
    }

    private void RequestGoBackHandler(object sender, object args)
    {
        if (!RootFrame.CanGoBack)
        {
            ViewModel.RequestHide();
            return;
        }

        RootFrame.GoBack();
        if (!RootFrame.CanGoBack)
        {
            HackyBadClearFilter();
        }
    }

    private void RequestGoHomeHandler(object sender, object args)
    {
        DoGoHome();
    }

    private void DoGoHome()
    {
        while (RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }

        if (!RootFrame.CanGoBack)
        {
            HackyBadClearFilter();
        }
    }

    private void AppendLog(string message)
    {
        _log += message + "\n";
    }

    private async Task LoadExtensions()
    {
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.LoadingExtensions = true;

        var extnService = Application.Current.GetService<IExtensionService>();
        if (extnService != null)
        {
            var extensions = await extnService.GetInstalledExtensionsAsync(ProviderType.Commands, includeDisabledExtensions: false);
            foreach (var extension in extensions)
            {
                if (extension == null)
                {
                    continue;
                }

                await LoadActionExtensionObject(extension);
            }
        }

        if (ViewModel != null)
        {
            ViewModel.LoadingExtensions = false;
            ViewModel.Loaded = true;
        }
    }

    private async Task LoadActionExtensionObject(IExtensionWrapper extension)
    {
        try
        {
            await extension.StartExtensionAsync();
            var wrapper = new CommandProviderWrapper(extension);
            ViewModel.ActionsProvider.Add(wrapper);
            await LoadTopLevelCommandsFromProvider(wrapper);
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
        }
    }

    private async Task LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        // TODO! do this better async
        await commandProvider.LoadTopLevelCommands().ConfigureAwait(false);
        foreach (var i in commandProvider.TopLevelItems)
        {
            ViewModel.TopLevelCommands.Add(new(i));
        }
    }

    private void GoToList(IListPage page)
    {
        var listVm = new ListPageViewModel(page) { Nested = true };
        InitializePage(listVm);
        DispatcherQueue.TryEnqueue(() =>
        {
            RootFrame.Navigate(typeof(ListPage), listVm, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        });
    }

    private void GoToMarkdown(IMarkdownPage page)
    {
        var mdVm = new MarkdownPageViewModel(page) { Nested = true };
        InitializePage(mdVm);
        DispatcherQueue.TryEnqueue(() =>
        {
            RootFrame.Navigate(typeof(MarkdownPage), mdVm, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        });
    }

    private void GoToForm(IFormPage page)
    {
        var formVm = new FormPageViewModel(page) { Nested = true };
        InitializePage(formVm);
        DispatcherQueue.TryEnqueue(() =>
        {
            RootFrame.Navigate(typeof(FormPage), formVm, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        });
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (RootFrame.CanGoBack)
            {
                RootFrame.GoBack();
            }
            else
            {
                ViewModel.RequestHide();
            }
        }
    }
}
