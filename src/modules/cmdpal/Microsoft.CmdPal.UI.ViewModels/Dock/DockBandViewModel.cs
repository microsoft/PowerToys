// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

#pragma warning disable SA1402 // File may only contain a single type

public sealed partial class DockBandViewModel : ExtensionObjectViewModel
{
    private readonly CommandItemViewModel _rootItem;

    public ObservableCollection<CommandItemViewModel> Items { get; } = new();

    internal DockBandViewModel(CommandItemViewModel commandItemViewModel, WeakReference<IPageContext> errorContext)
        : base(errorContext)
    {
        _rootItem = commandItemViewModel;
    }

    private void InitializeFromList(IListPage list)
    {
        var items = list.GetItems();
        foreach (var item in items)
        {
            var newItemVm = new CommandItemViewModel(new(item), this.PageContext);
            newItemVm.SlowInitializeProperties();
            Items.Add(newItemVm);
        }
    }

    public override void InitializeProperties()
    {
        var command = _rootItem.Command;
        var list = command.Model.Unsafe as IListPage;
        if (list is not null)
        {
            InitializeFromList(list);
        }
        else
        {
            Items.Add(_rootItem);
        }
    }
}

#pragma warning restore SA1402 // File may only contain a single type
