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
    private readonly DockBandSettings _bandSettings;
    private readonly DockSettings _dockSettings;
    private readonly Action _saveSettings;

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    private bool _showLabels = true;
    private bool? _showLabelsSnapshot;

    public string Id => _rootItem.Command.Id;

    /// <summary>
    /// Gets or sets a value indicating whether labels are shown for items in this band.
    /// This is a preview value - call <see cref="SaveShowLabels"/> to persist or
    /// <see cref="RestoreShowLabels"/> to discard changes.
    /// </summary>
    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            if (_showLabels != value)
            {
                _showLabels = value;
                foreach (var item in Items)
                {
                    item.ShowLabel = value;
                }
            }
        }
    }

    /// <summary>
    /// Takes a snapshot of the current ShowLabels value before editing.
    /// </summary>
    internal void SnapshotShowLabels()
    {
        _showLabelsSnapshot = _showLabels;
    }

    /// <summary>
    /// Saves the current ShowLabels value to settings.
    /// </summary>
    internal void SaveShowLabels()
    {
        _bandSettings.ShowLabels = _showLabels;
        _showLabelsSnapshot = null;
    }

    /// <summary>
    /// Restores the ShowLabels value from the snapshot.
    /// </summary>
    internal void RestoreShowLabels()
    {
        if (_showLabelsSnapshot.HasValue)
        {
            ShowLabels = _showLabelsSnapshot.Value;
            _showLabelsSnapshot = null;
        }
    }

    internal DockBandViewModel(
        CommandItemViewModel commandItemViewModel,
        WeakReference<IPageContext> errorContext,
        DockBandSettings settings,
        DockSettings dockSettings,
        Action saveSettings)
        : base(errorContext)
    {
        _rootItem = commandItemViewModel;
        _bandSettings = settings;
        _dockSettings = dockSettings;
        _saveSettings = saveSettings;

        _showLabels = settings.ResolveShowLabels(dockSettings.ShowLabels);
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
    private bool _showLabel = true;

    public bool ShowLabel
    {
        get => _showLabel;
        internal set
        {
            if (_showLabel != value)
            {
                _showLabel = value;
                UpdateProperty(nameof(ShowLabel));
                UpdateProperty(nameof(HasText));
                UpdateProperty(nameof(Title));
                UpdateProperty(nameof(Subtitle));
            }
        }
    }

    public override string Title => _showLabel ? ItemTitle : string.Empty;

    public new string Subtitle => _showLabel ? base.Subtitle : string.Empty;

    public override bool HasText => _showLabel ? base.HasText : false;

    /// <summary>
    /// Gets the tooltip for the dock item, which includes the title and
    /// subtitle. If it doesn't have one part, it just returns the other.
    /// </summary>
    /// <remarks>
    /// Trickery: in the case one is empty, we can just concatenate, and it will
    /// always only be the one that's non-empty
    /// </remarks>
    public string Tooltip =>
        !string.IsNullOrEmpty(Title) && !string.IsNullOrEmpty(Subtitle) ?
            $"{Title}\n{Subtitle}" :
            Title + Subtitle;

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
