// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.Ext.Calc;
using Microsoft.CmdPal.Ext.Registry;
using Microsoft.CmdPal.Ext.Settings;
using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CmdPal.Ext.WindowsServices;
using Microsoft.CmdPal.Ext.WindowsSettings;
using Microsoft.CmdPal.Ext.WindowsTerminal;
using Microsoft.CmdPal.Ext.WindowWalker;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;
using WindowsCommandPalette.BuiltinCommands;
using WindowsCommandPalette.Models;

namespace WindowsCommandPalette.Views;

public sealed class MainViewModel : IDisposable
{
    private readonly QuitCommandProvider _quitCommandProvider = new();
    private readonly ReloadExtensionsCommandProvider _reloadCommandProvider = new();

    public AllAppsPage Apps { get; set; } = new();

    public event TypedEventHandler<object, object?>? QuitRequested { add => _quitCommandProvider.QuitRequested += value; remove => _quitCommandProvider.QuitRequested -= value; }

    public ObservableCollection<CommandProviderWrapper> ActionsProvider { get; set; } = [];

    public ObservableCollection<ExtensionObject<ICommandItem>> TopLevelCommands { get; set; } = [];

    public List<ICommandProvider> BuiltInCommands { get; set; } = [];

    public bool Loaded { get; set; }

    public bool LoadingExtensions { get; set; }

    public bool LoadedApps { get; set; }

    public event TypedEventHandler<object, object?>? HideRequested;

    public event TypedEventHandler<object, object?>? SummonRequested;

    public event TypedEventHandler<object, object?>? AppsReady;

    public event TypedEventHandler<object, ICommand?>? GoToCommandRequested;

    private readonly Dictionary<string, CommandAlias> _aliases = new();

    internal MainViewModel()
    {
        BuiltInCommands.Add(new BookmarksCommandProvider());
        BuiltInCommands.Add(new CalculatorCommandProvider());
        BuiltInCommands.Add(new SettingsCommandProvider());
        BuiltInCommands.Add(_quitCommandProvider);
        BuiltInCommands.Add(_reloadCommandProvider);
        BuiltInCommands.Add(new WindowsTerminalCommandsProvider());
        BuiltInCommands.Add(new WindowsServicesCommandsProvider());
        BuiltInCommands.Add(new RegistryCommandsProvider());
        BuiltInCommands.Add(new WindowsSettingsCommandsProvider());
        BuiltInCommands.Add(new ShellCommandsProvider());
        BuiltInCommands.Add(new WindowWalkerCommandsProvider());

        ResetTopLevel();

        PopulateAliases();

        // On a background thread, warm up the app cache since we want it more often than not
        new Task(() =>
        {
            _ = AppCache.Instance.Value;

            LoadedApps = true;
            AppsReady?.Invoke(this, null);
        }).Start();
    }

    public void ResetTopLevel()
    {
        TopLevelCommands.Clear();
        TopLevelCommands.Add(new(new ListItem(Apps)));
    }

    internal void RequestHide()
    {
        var handlers = HideRequested;
        handlers?.Invoke(this, null);
    }

    public void Summon()
    {
        var handlers = SummonRequested;
        handlers?.Invoke(this, null);
    }

    private static string CreateHash(string? title, string? subtitle)
    {
        return title + subtitle;
    }

    public IEnumerable<IListItem> AppItems => LoadedApps ? Apps.GetItems() : [];

    // Okay this is definitely bad - Evaluating this re-wraps every app in the list with a new wrapper, holy fuck that's stupid
    public IEnumerable<ExtensionObject<ICommandItem>> Everything => TopLevelCommands
        .Concat(AppItems.Select(i => new ExtensionObject<ICommandItem>(i)))
        .Where(i =>
        {
            var v = i != null;
            return v;
        });

    public void Dispose()
    {
        _quitCommandProvider.Dispose();
        _reloadCommandProvider.Dispose();
    }

    private void AddAlias(CommandAlias a)
    {
        _aliases.Add(a.SearchPrefix, a);
    }

    public bool CheckAlias(string searchText)
    {
        // var foundAliias = searchText == "vd";
        // var aliasTarget = "com.zadjii.VirtualDesktopsList";
        if (_aliases.TryGetValue(searchText, out var alias))
        {
            try
            {
                foreach (var listItemWrapper in this.TopLevelCommands)
                {
                    var li = listItemWrapper.Unsafe;
                    if (li == null)
                    {
                        continue;
                    }

                    var id = li.Command?.Id;
                    if (!string.IsNullOrEmpty(id) && id == alias.CommandId)
                    {
                        GoToCommandRequested?.Invoke(this, li.Command);
                        return true;
                    }
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private void PopulateAliases()
    {
        this.AddAlias(new CommandAlias("vd", "com.zadjii.VirtualDesktopsList", true));
        this.AddAlias(new CommandAlias(":", "com.microsoft.cmdpal.registry", true));
        this.AddAlias(new CommandAlias("$", "com.microsoft.cmdpal.windowsSettings", true));
        this.AddAlias(new CommandAlias("=", "com.microsoft.cmdpal.calculator", true));
        this.AddAlias(new CommandAlias(">", "com.microsoft.cmdpal.shell", true));
        this.AddAlias(new CommandAlias("<", "com.microsoft.cmdpal.windowwalker", true));
    }
}
