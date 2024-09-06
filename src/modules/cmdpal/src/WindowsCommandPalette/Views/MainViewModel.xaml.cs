// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CmdPal.Models;
using DeveloperCommandPalette;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.Ext.Calc;
using Microsoft.CmdPal.Ext.Settings;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;
using WindowsCommandPalette.BuiltinCommands;
using WindowsCommandPalette.BuiltinCommands.AllApps;

namespace WindowsCommandPalette.Views;

public sealed class MainViewModel : IDisposable
{
    private readonly QuitActionProvider _quitActionProvider = new();
    private readonly ReloadExtensionsActionProvider _reloadActionProvider = new();

    public AllAppsPage Apps { get; set; } = new();

    public event TypedEventHandler<object, object?>? QuitRequested { add => _quitActionProvider.QuitRequested += value; remove => _quitActionProvider.QuitRequested -= value; }

    public ObservableCollection<ActionsProviderWrapper> ActionsProvider { get; set; } = [];

    public ObservableCollection<ExtensionObject<IListItem>> TopLevelCommands { get; set; } = [];

    public List<ICommandProvider> BuiltInCommands { get; set; } = [];

    public bool Loaded { get; set; }

    public bool LoadingExtensions { get; set; }

    public bool LoadedApps { get; set; }

    public event TypedEventHandler<object, object?>? HideRequested;

    public event TypedEventHandler<object, object?>? SummonRequested;

    public event TypedEventHandler<object, object?>? AppsReady;

    internal MainViewModel()
    {
        BuiltInCommands.Add(new BookmarksActionProvider());
        BuiltInCommands.Add(new CalculatorActionProvider());
        BuiltInCommands.Add(new SettingsActionProvider());
        BuiltInCommands.Add(_quitActionProvider);
        BuiltInCommands.Add(_reloadActionProvider);

        ResetTopLevel();

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

    private string[] _recentCommandHashes = [];

    public IEnumerable<IListItem> RecentActions => TopLevelCommands
        .Select(i => i.Unsafe)
        .Where((i) =>
        {
            if (i != null)
            {
                try
                {
                    return _recentCommandHashes.Contains(CreateHash(i.Title, i.Subtitle));
                }
                catch (COMException)
                {
                    return false;
                }
            }

            return false;
        }).Select(i => i!);

    public IEnumerable<IListItem> AppItems => LoadedApps ? Apps.GetItems().First().Items : [];

    public IEnumerable<ExtensionObject<IListItem>> Everything => TopLevelCommands
        .Concat(AppItems.Select(i => new ExtensionObject<IListItem>(i)))
        .Where(i =>
        {
            var v = i != null;
            return v;
        });

    public IEnumerable<ExtensionObject<IListItem>> Recent => _recentCommandHashes
        .Select(hash =>
            Everything
                .Where(i =>
                {
                    try
                    {
                        var o = i.Unsafe;
                        return CreateHash(o.Title, o.Subtitle) == hash;
                    }
                    catch (COMException)
                    {
                        return false;
                    }
                })
                .FirstOrDefault())
        .Where(i => i != null)
        .Select(i => i!);

    public bool IsRecentCommand(MainListItem item)
    {
        try
        {
            foreach (var wraprer in Recent)
            {
                if (wraprer.Unsafe == item)
                {
                    return true;
                }
            }
        }
        catch (COMException)
        {
            return false;
        }

        return false;
    }

    internal void PushRecentAction(ICommand action)
    {
        foreach (var wrapped in Everything)
        {
            try
            {
                var listItem = wrapped?.Unsafe;
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
            catch (COMException)
            { /* log something */
            }
        }
    }

    public void Dispose()
    {
        _quitActionProvider.Dispose();
        _reloadActionProvider.Dispose();
    }
}
