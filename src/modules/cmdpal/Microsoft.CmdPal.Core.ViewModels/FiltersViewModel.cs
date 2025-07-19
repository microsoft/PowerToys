// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FiltersViewModel : ObservableObject,
    IRecipient<UpdateFiltersMessage>,
    IRecipient<UpdateCurrentFilterIdsMessage>
{
    private readonly IconInfoViewModel filterIcon = new(new IconInfo("\uE71C"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFilterName))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIcon))]
    [NotifyPropertyChangedFor(nameof(ShouldShowFilters))]
    [NotifyPropertyChangedFor(nameof(SelectedFilters))]
    public partial IFilterItemViewModel[] Filters { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFilterName))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIcon))]
    [NotifyPropertyChangedFor(nameof(SelectedFilters))]
    public partial string[] CurrentFilterIds { get; private set; } = [];

    [ObservableProperty]
    public partial bool MultipleSelectionsEnabled { get; private set; }

    public IFilterItemViewModel[] SelectedFilters => Filters
                                                        .OfType<FilterItemViewModel>()
                                                        .Where(item => CurrentFilterIds.Contains(item.Id))
                                                        .ToArray();

    public bool ShouldShowFilters => Filters is not null && Filters.Length > 0;

    public IconInfoViewModel? SelectedFilterIcon
    {
        get
        {
            if (CurrentFilterIds.Length == 1)
            {
                var item = Filters
                                .OfType<FilterItemViewModel>()
                                .FirstOrDefault(f => f.Id == CurrentFilterIds[0]);

                if (item?.Icon is null || !item.Icon.HasIcon(true))
                {
                    // If the filter item doesn't have an icon, use the default filter icon
                    return filterIcon;
                }

                return item.Icon;
            }

            return filterIcon;
        }
    }

    public Visibility HasIcon(IconInfoViewModel? icon) => icon?.HasIcon(true) ?? false ? Visibility.Visible : Visibility.Collapsed;

    public string SelectedFilterName
    {
        get
        {
            if (CurrentFilterIds.Length == 0)
            {
                return "Filters";
            }
            else if (CurrentFilterIds.Length == 1)
            {
                var item = Filters
                                .OfType<FilterItemViewModel>()
                                .FirstOrDefault(f => f.Id == CurrentFilterIds[0]);

                return item?.Name ?? string.Empty;
            }
            else
            {
                var selected = Filters
                                .OfType<FilterItemViewModel>()
                                .Where(f => CurrentFilterIds.Any(c => c == f.Id))
                                .Select(item => item.Name)
                                .ToList();
                var label = string.Join(", ", selected);

                if (label.Length > 15)
                {
                    label = $"{selected[0]} & {selected.Count - 1} more";
                }

                return label;
            }
        }
    }

    public bool IsSelected(FilterItemViewModel filterItemViewModel) => CurrentFilterIds.Contains(filterItemViewModel.Id);

    public FiltersViewModel()
    {
        filterIcon.InitializeProperties();

        WeakReferenceMessenger.Default.Register<UpdateFiltersMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateCurrentFilterIdsMessage>(this);
    }

    public void Receive(UpdateFiltersMessage message)
    {
        IFilterItemViewModel[] newFilters = [];
        string[] newSelectedFilterIds = [];

        var multiSelectEnabled = false;

        if (message.Filters is not null)
        {
            var filters = message.Filters;
            multiSelectEnabled = message.IsMultiSelect;

            if (filters is not null)
            {
                newFilters = filters.Select(filter =>
                                    {
                                        if (filter is IFilter filterItem)
                                        {
                                            return new FilterItemViewModel(filterItem) as IFilterItemViewModel;
                                        }
                                        else
                                        {
                                            return new SeparatorViewModel();
                                        }
                                    })
                                    .ToArray();
            }

            newSelectedFilterIds = message.CurrentFilterIds;
        }

        MultipleSelectionsEnabled = multiSelectEnabled;
        Filters = newFilters;
        CurrentFilterIds = newSelectedFilterIds;
    }

    public void Receive(UpdateCurrentFilterIdsMessage message)
    {
        CurrentFilterIds = message.CurrentFilterIds;
    }

    public void SelectOne(string filterId)
    {
        CurrentFilterIds = [filterId];
        WeakReferenceMessenger.Default.Send<UpdateCurrentFilterIdsMessage>(new(CurrentFilterIds));
    }

    public void UpdateCurrentFilterIds(string[] newFilterIds, string[] removeFilterIds)
    {
        CurrentFilterIds = CurrentFilterIds.Except(removeFilterIds).ToArray();
        CurrentFilterIds = CurrentFilterIds.Concat(newFilterIds).ToArray();
        WeakReferenceMessenger.Default.Send<UpdateCurrentFilterIdsMessage>(new(CurrentFilterIds));
    }

    public bool IsSelected(string filterId) => CurrentFilterIds.Contains(filterId);
}
