// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.Pages;

/// <summary>
/// This class encapsulates the data we load from built-in providers and extensions to use within the same extension-UI system for a <see cref="ListPage"/>.
/// TODO: Need to think about how we structure/interop for the page -> section -> item between the main setup, the extensions, and our viewmodels.
/// </summary>
public partial class MainListPage : DynamicListPage
{
    private readonly IServiceProvider _serviceProvider;

    private readonly ObservableCollection<TopLevelCommandWrapper> _commands;

    public MainListPage(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        var tlcManager = _serviceProvider.GetService<TopLevelCommandManager>();

        // reference the TLC collection directly... maybe? TODO is this a good idea ot a terrible one?
        _commands = tlcManager!.TopLevelCommands;
        _commands.CollectionChanged += Commands_CollectionChanged;
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseItemsChanged(_commands.Count);

    public override IListItem[] GetItems() => _commands
        .Select(tlc => tlc)
        .ToArray();

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        /* handle changes to the filter text here */
    }
}
