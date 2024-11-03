// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels;

namespace Microsoft.CmdPal.UI.Pages;

/// <summary>
/// This class encapsulates the data we load from built-in providers and extensions to use within the same extension-UI system for a <see cref="ListPage"/>.
/// TODO: Need to think about how we structure/interop for the page -> section -> item between the main setup, the extensions, and our viewmodels.
/// </summary>
public partial class MainListPage : DynamicListPage
{
    private readonly IListItem[] _items;

    // TODO: Thinking we may want a separate MainViewModel from the ShellViewModel and/or a CommandService/Provider
    // which holds the TopLevelCommands and anything that needs to access those functions...
    public MainListPage(ShellViewModel shellViewModel)
    {
        _items = shellViewModel.TopLevelCommands.Select(w => w.Unsafe).Where(li => li != null).ToArray();
    }

    public override IListItem[] GetItems() => _items;
}
