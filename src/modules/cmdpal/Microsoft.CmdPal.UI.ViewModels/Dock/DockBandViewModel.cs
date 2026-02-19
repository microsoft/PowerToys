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

    private bool _showTitles = true;
    private bool _showSubtitles = true;
    private bool? _showTitlesSnapshot;
    private bool? _showSubtitlesSnapshot;

    public string Id => _rootItem.Command.Id;

    /// <summary>
    /// Gets or sets a value indicating whether titles are shown for items in this band.
    /// This is a preview value - call <see cref="SaveLabelSettings"/> to persist or
    /// <see cref="RestoreLabelSettings"/> to discard changes.
    /// </summary>
    public bool ShowTitles
    {
        get => _showTitles;
        set
        {
            if (_showTitles != value)
            {
                _showTitles = value;
                foreach (var item in Items)
                {
                    item.ShowTitle = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether subtitles are shown for items in this band.
    /// This is a preview value - call <see cref="SaveLabelSettings"/> to persist or
    /// <see cref="RestoreLabelSettings"/> to discard changes.
    /// </summary>
    public bool ShowSubtitles
    {
        get => _showSubtitles;
        set
        {
            if (_showSubtitles != value)
            {
                _showSubtitles = value;
                foreach (var item in Items)
                {
                    item.ShowSubtitle = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether labels (both titles and subtitles) are shown.
    /// Provided for backward compatibility - setting this sets both ShowTitles and ShowSubtitles.
    /// </summary>
    public bool ShowLabels
    {
        get => _showTitles && _showSubtitles;
        set
        {
            ShowTitles = value;
            ShowSubtitles = value;
        }
    }

    /// <summary>
    /// Takes a snapshot of the current label settings before editing.
    /// </summary>
    internal void SnapshotShowLabels()
    {
        _showTitlesSnapshot = _showTitles;
        _showSubtitlesSnapshot = _showSubtitles;
    }

    /// <summary>
    /// Saves the current label settings to settings.
    /// </summary>
    internal void SaveShowLabels()
    {
        _bandSettings.ShowTitles = _showTitles;
        _bandSettings.ShowSubtitles = _showSubtitles;
        _showTitlesSnapshot = null;
        _showSubtitlesSnapshot = null;
    }

    /// <summary>
    /// Restores the label settings from the snapshot.
    /// </summary>
    internal void RestoreShowLabels()
    {
        if (_showTitlesSnapshot.HasValue)
        {
            ShowTitles = _showTitlesSnapshot.Value;
            _showTitlesSnapshot = null;
        }

        if (_showSubtitlesSnapshot.HasValue)
        {
            ShowSubtitles = _showSubtitlesSnapshot.Value;
            _showSubtitlesSnapshot = null;
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

        _showTitles = settings.ResolveShowTitles(dockSettings.ShowLabels);
        _showSubtitles = settings.ResolveShowSubtitles(dockSettings.ShowLabels);
    }

    private void InitializeFromList(IListPage list)
    {
        var items = list.GetItems();
        var newViewModels = new List<DockItemViewModel>();
        foreach (var item in items)
        {
            var newItemVm = new DockItemViewModel(new(item), this.PageContext, _showTitles, _showSubtitles);
            newItemVm.SlowInitializeProperties();
            newViewModels.Add(newItemVm);
        }

        List<DockItemViewModel> removed = new();
        DoOnUiThread(() =>
        {
            ListHelpers.InPlaceUpdateList(Items, newViewModels, out removed);
        });

        foreach (var removedItem in removed)
        {
            removedItem.SafeCleanup();
        }
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
                var dockItem = new DockItemViewModel(_rootItem, _showTitles, _showSubtitles);
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

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        var command = _rootItem.Command;
        if (command.Model.Unsafe is IListPage list)
        {
            list.ItemsChanged -= HandleItemsChanged;
        }

        foreach (var item in Items)
        {
            item.SafeCleanup();
        }
    }
}

public partial class DockItemViewModel : CommandItemViewModel
{
    private bool _showTitle = true;
    private bool _showSubtitle = true;

    public bool ShowTitle
    {
        get => _showTitle;
        internal set
        {
            if (_showTitle != value)
            {
                _showTitle = value;
                UpdateProperty(nameof(ShowTitle));
                UpdateProperty(nameof(ShowLabel));
                UpdateProperty(nameof(HasText));
                UpdateProperty(nameof(Title));
            }
        }
    }

    public bool ShowSubtitle
    {
        get => _showSubtitle;
        internal set
        {
            if (_showSubtitle != value)
            {
                _showSubtitle = value;
                UpdateProperty(nameof(ShowSubtitle));
                UpdateProperty(nameof(ShowLabel));
                UpdateProperty(nameof(Subtitle));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether labels are shown (either titles or subtitles).
    /// Setting this sets both ShowTitle and ShowSubtitle.
    /// </summary>
    public bool ShowLabel
    {
        get => _showTitle || _showSubtitle;
        internal set
        {
            ShowTitle = value;
            ShowSubtitle = value;
        }
    }

    public override string Title => _showTitle ? ItemTitle : string.Empty;

    public new string Subtitle => _showSubtitle ? base.Subtitle : string.Empty;

    public override bool HasText => (_showTitle && !string.IsNullOrEmpty(ItemTitle)) || (_showSubtitle && !string.IsNullOrEmpty(base.Subtitle));

    /// <summary>
    /// Gets the tooltip for the dock item, which includes the title and
    /// subtitle. If it doesn't have one part, it just returns the other.
    /// </summary>
    /// <remarks>
    /// Trickery: in the case one is empty, we can just concatenate, and it will
    /// always only be the one that's non-empty
    /// </remarks>
    public string Tooltip =>
        !string.IsNullOrEmpty(ItemTitle) && !string.IsNullOrEmpty(base.Subtitle) ?
            $"{ItemTitle}\n{base.Subtitle}" :
            ItemTitle + base.Subtitle;

    public DockItemViewModel(CommandItemViewModel root, bool showTitle, bool showSubtitle)
        : this(root.Model, root.PageContext, showTitle, showSubtitle)
    {
    }

    public DockItemViewModel(ExtensionObject<ICommandItem> item, WeakReference<IPageContext> errorContext, bool showTitle, bool showSubtitle)
        : base(item, errorContext)
    {
        _showTitle = showTitle;
        _showSubtitle = showSubtitle;
    }
}


#pragma warning restore SA1402 // File may only contain a single type
