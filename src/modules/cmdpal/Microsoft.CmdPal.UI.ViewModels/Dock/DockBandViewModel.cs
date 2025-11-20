// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

#pragma warning disable SA1402 // File may only contain a single type

public sealed partial class DockBandViewModel : ExtensionObjectViewModel
{
    private readonly CommandItemViewModel _rootItem;

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    private bool _showLabels = true;

    public string Id => _rootItem.Command.Id;

    internal DockBandViewModel(CommandItemViewModel commandItemViewModel, WeakReference<IPageContext> errorContext, DockBandSettings settings)
        : base(errorContext)
    {
        _rootItem = commandItemViewModel;

        _showLabels = settings.ShowLabels ?? true;
    }

    private void InitializeFromList(IListPage list)
    {
        var items = list.GetItems();
        var newViewModels = new List<DockItemViewModel>();
        foreach (var item in items)
        {
            var newItemVm = new DockItemViewModel(new(item), this.PageContext, _showLabels);
            newItemVm.SlowInitializeProperties();
            newViewModels.Add(newItemVm);
        }

        DoOnUiThread(() =>
        {
            ListHelpers.InPlaceUpdateList(Items, newViewModels, out var removed);
        });

        // TODO! dispose removed VMs
    }

    public override void InitializeProperties()
    {
        var command = _rootItem.Command;
        var list = command.Model.Unsafe as IListPage;
        if (list is not null)
        {
            InitializeFromList(list);
            list.ItemsChanged += HandleItemsChanged;
        }
        else
        {
            DoOnUiThread(() =>
             {
                 var dockItem = new DockItemViewModel(_rootItem, _showLabels);
                 dockItem.SlowInitializeProperties();
                 Items.Add(dockItem);
             });
        }
    }

    private void HandleItemsChanged(object sender, IItemsChangedEventArgs args)
    {
        if (_rootItem.Command.Model.Unsafe is IListPage p)
        {
            InitializeFromList(p);
        }
    }
}

public partial class DockItemViewModel : CommandItemViewModel
{
    private readonly bool _showLabel = true;

    public override string Title => ItemTitle;

    public override bool HasText => _showLabel ? base.HasText : false;

    public DockItemViewModel(CommandItemViewModel root, bool showLabel)
        : this(root.Model, root.PageContext, showLabel)
    {
    }

    public DockItemViewModel(ExtensionObject<ICommandItem> item, WeakReference<IPageContext> errorContext, bool showLabel)
        : base(item, errorContext)
    {
        _showLabel = showLabel;
    }
}


#pragma warning restore SA1402 // File may only contain a single type
