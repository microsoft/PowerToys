// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Win32;

namespace DeveloperCommandPalette;

public sealed class MainViewModel
{
    internal readonly AllApps.AllAppsPage apps = new();
    internal readonly ObservableCollection<ActionsProviderWrapper> CommandsProviders = new();
    internal readonly ObservableCollection<IListItem> TopLevelCommands = [];

    internal readonly List<ICommandProvider> _builtInCommands = [];

    internal bool Loaded;
    internal bool LoadingExtensions;
    internal bool LoadedApps;

    public event TypedEventHandler<object, object?>? HideRequested;
    public event TypedEventHandler<object, object?>? SummonRequested;
    public event TypedEventHandler<object, object?>? AppsReady;

    internal MainViewModel()
    {
        _builtInCommands.Add(new Run.Bookmarks.BookmarksActionProvider());
        _builtInCommands.Add(new Calculator.CalculatorActionProvider());
        _builtInCommands.Add(new Run.Settings.SettingsActionProvider());
        _builtInCommands.Add(new QuitActionProvider());

        ResetTopLevel();

        // On a background thread, warm up the app cache since we want it more often than not
        new Task(() => {
            var _ = AllApps.AppCache.Instance.Value;
            LoadedApps = true;
            AppsReady?.Invoke(this, null);
        }).Start();
    }
    public void ResetTopLevel()
    {
        TopLevelCommands.Clear();
        TopLevelCommands.Add(new ListItem(apps));
    }

    internal void RequestHide()
    {
        var handlers = HideRequested;
        handlers?.Invoke(this, null);
    }

    public void Summon(){
        var handlers = SummonRequested;
        handlers?.Invoke(this, null);
    }

    private static string CreateHash(string? title, string? subtitle)
    {
        return title + subtitle;
    }
    private string[] _recentCommandHashes = [];// ["SpotifySpotify", "All Apps", "GitHub Issues", "Microsoft/GithubBookmark"];
    public IEnumerable<IListItem> RecentActions => TopLevelCommands.Where(i => i != null && _recentCommandHashes.Contains(CreateHash(i.Title, i.Subtitle)));
    public IEnumerable<IListItem> AppItems => LoadedApps? apps.GetItems().First().Items : [];
    public IEnumerable<IListItem> Everything => TopLevelCommands.Concat(AppItems).Where(i => i!= null);
    public IEnumerable<IListItem> Recent => _recentCommandHashes.Select(hash => Everything.Where(i => CreateHash(i.Title, i.Subtitle) == hash ).FirstOrDefault()).Where(i => i != null).Select(i=>i!);

    internal void PushRecentAction(ICommand action)
    {
        IEnumerable<IListItem> topLevel = TopLevelCommands;
        if (LoadedApps)
        {
            topLevel = topLevel.Concat(AppItems);
        }

        foreach (var listItem in topLevel)
        {
            if (listItem != null && listItem.Command == action)
            {
                // Found it, awesome.
                var hash = CreateHash(listItem.Title, listItem.Subtitle);
                // Remove the old one and push the new one to the front
                var recent = new List<string>([hash]).Concat(_recentCommandHashes.Where(h => h != hash)).Take(5).ToArray();
                _recentCommandHashes = recent.ToArray();
                return;
            }
        }
    }
}

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private string _log = "";
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
        _ = LoadExtensions();

        RootFrame.Navigate(typeof(ListPage), rootListVm, new DrillInNavigationTransitionInfo());
    }
    private void _HackyBadClearFilter()
    {
        // BODGY but I don't care, cause i'm throwing this all out
        if ((this.RootFrame.Content as Page)?.FindName("FilterBox") is TextBox tb) {
            tb.Text = "";
            tb.Focus(FocusState.Programmatic);
        }


        _ = LoadAllCommands();
    }
    private void ViewModel_SummonRequested(object sender, object? args)
    {
        if (!RootFrame.CanGoBack)
        {
            _HackyBadClearFilter();
        }
    }
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }
    private async Task LoadAllCommands()
    {
        ViewModel.ResetTopLevel();
        // Load builtins syncronously...
        await LoadBuiltinCommandsAsync();
        // ...and extensions on a fire_and_forget
        _ = LoadExtensions();

    }
    public async Task LoadBuiltinCommandsAsync()
    {
        // Load commands from builtins
        // TODO! I don't understand async enough to get why this has to be ConfigureAwait(false)
        foreach (var provider in ViewModel._builtInCommands)
        {
            var wrapper = new ActionsProviderWrapper(provider);
            ViewModel.CommandsProviders.Add(wrapper);
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
        else if (action is IListPage listPage) {
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
        foreach (var provider in ViewModel.CommandsProviders)
        {
            if (!provider.IsExtension) continue;
            foreach (var item in provider.TopLevelItems)
            {
                if (action == item.Command)
                {
                    provider.AllowSetForeground(true);
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
        if (res == null) return;

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
        if (!RootFrame.CanGoBack) {
            ViewModel.RequestHide();
            return;
        }
        RootFrame.GoBack();
        if (!RootFrame.CanGoBack){
            _HackyBadClearFilter();
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
            _HackyBadClearFilter();
        }
    }

    private void AppendLog(string message)
    {
        _log += message + "\n";
    }

    private async Task LoadExtensions()
    {
        if (ViewModel != null) ViewModel.LoadingExtensions = true;
        // Get extensions for us:
        AppExtensionCatalog extensionCatalog = AppExtensionCatalog.Open("com.microsoft.windows.commandpalette");
        IReadOnlyList<AppExtension> extensions = await extensionCatalog.FindAllAsync();
        foreach (var extension in extensions)
        {
            var name = extension.DisplayName;
            var id = extension.Id;
            var pfn = extension.Package.Id.FamilyName;

            var (provider, classIds) = await ExtensionLoader.GetExtensionPropertiesAsync(extension);
            if (provider == null || classIds.Count == 0)
            {
                continue;
            }

            AppendLog($"Found Extension:{name}, {id}, {pfn}->");

            foreach (var classId in classIds)
            {
                _ = LoadExtensionClassObject(extension, classId);
            }
        }


        if (ViewModel != null)
        {
            ViewModel.LoadingExtensions = false;
            ViewModel.Loaded = true;
        }
    }

    private async Task LoadExtensionClassObject(AppExtension extension, string classId)
    {
        AppendLog($"\t{classId}");
        try
        {
            var extensionWrapper = new ExtensionWrapper(extension, classId);
            await extensionWrapper.StartExtensionAsync();
            var wrapper = new ActionsProviderWrapper(extensionWrapper);
            ViewModel.CommandsProviders.Add(wrapper);

            await LoadTopLevelCommandsFromProvider(wrapper);
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
        }
    }

    private async Task LoadTopLevelCommandsFromProvider(ActionsProviderWrapper actionProvider)
    {
        // TODO! do this better async

        await actionProvider.LoadTopLevelCommands().ConfigureAwait(false);
        foreach (var i in actionProvider.TopLevelItems)
        {
            ViewModel.TopLevelCommands.Add(i);
        }
    }

    private void GoToList(IListPage page)
    {
        var listVm = new ListPageViewModel(page) { Nested = true };
        InitializePage(listVm);
        DispatcherQueue.TryEnqueue(() => {
            RootFrame.Navigate(typeof(ListPage), listVm, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        });
    }
    private void GoToMarkdown(IMarkdownPage page)
    {
        var mdVm = new MarkdownPageViewModel(page) { Nested = true };
        InitializePage(mdVm);
        DispatcherQueue.TryEnqueue(() => {
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

sealed class ActionsProviderWrapper
{
    public bool IsExtension => extensionWrapper != null;
    private readonly bool isValid;
    private ICommandProvider ActionProvider { get; }
    private readonly ExtensionWrapper? extensionWrapper;
    private IListItem[] _topLevelItems = [];
    public IListItem[] TopLevelItems => _topLevelItems;

    public ActionsProviderWrapper(ICommandProvider provider) {
        ActionProvider = provider;
        isValid = true;
    }
    public ActionsProviderWrapper(ExtensionWrapper extension)
    {
        extensionWrapper = extension;
        var extensionImpl = extension.GetExtensionObject();
        if (extensionImpl?.GetProvider(ProviderType.Commands) is not ICommandProvider provider) throw new ArgumentException("extension didn't actually implement ICommandProvider");
        ActionProvider = provider;
        isValid = true;
    }
    public async Task LoadTopLevelCommands()
    {
        if (!isValid) return;

        var t = new Task<IListItem[]>(() => ActionProvider.TopLevelCommands());
        t.Start();
        var commands = await t.ConfigureAwait(false);

        // On a BG thread here

        if (commands != null)
        {
            _topLevelItems = commands;
        }

    }

    public void AllowSetForeground(bool allow)
    {
        if (!IsExtension)
        {
            return;
        }
        var iextn = extensionWrapper?.GetExtensionObject();
        unsafe
        {
            PInvoke.CoAllowSetForegroundWindow(iextn);
        }
    }
}
