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
    IRecipient<UpdateCurrentFilterIdMessage>
{
    private readonly IconInfoViewModel filterIcon = new(new IconInfo("\uE71C"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFilterName))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIcon))]
    [NotifyPropertyChangedFor(nameof(ShouldShowFilters))]
    [NotifyPropertyChangedFor(nameof(SelectedFilter))]
    public partial IFilterItemViewModel[] Filters { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFilterName))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIcon))]
    [NotifyPropertyChangedFor(nameof(SelectedFilter))]
    public partial string CurrentFilterId { get; private set; } = string.Empty;

    public FilterItemViewModel? SelectedFilter => Filters
                                                        .OfType<FilterItemViewModel>()
                                                        .FirstOrDefault(item => CurrentFilterId == item.Id);

    public bool ShouldShowFilters => Filters is not null && Filters.Length > 0;

    public IconInfoViewModel? SelectedFilterIcon
    {
        get
        {
            if (SelectedFilter is not null)
            {
                if (SelectedFilter.Icon is null || !SelectedFilter.Icon.HasIcon(true))
                {
                    // If the filter item doesn't have an icon, use the default filter icon
                    return filterIcon;
                }

                return SelectedFilter.Icon;
            }

            return filterIcon;
        }
    }

    public Visibility HasIcon(IconInfoViewModel? icon) => icon?.HasIcon(true) ?? false ? Visibility.Visible : Visibility.Collapsed;

    public string SelectedFilterName => SelectedFilter is null
        ? "Filters"
        : SelectedFilter.Name;

    public bool IsSelected(FilterItemViewModel filterItemViewModel) => SelectedFilter?.Id == filterItemViewModel.Id;

    public FiltersViewModel()
    {
        filterIcon.InitializeProperties();

        WeakReferenceMessenger.Default.Register<UpdateFiltersMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateCurrentFilterIdMessage>(this);
    }

    public void Receive(UpdateFiltersMessage message)
    {
        IFilterItemViewModel[] newFilters = [];
        var newSelectedFilterId = string.Empty;

        if (message.Filters is not null)
        {
            var filters = message.Filters;

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
                                            return new SeparatorViewModel() as IFilterItemViewModel;
                                        }
                                    })
                                    .ToArray();
            }

            newSelectedFilterId = message.CurrentFilterId;
        }

        Filters = newFilters;
        CurrentFilterId = newSelectedFilterId;
    }

    public void Receive(UpdateCurrentFilterIdMessage message)
    {
        CurrentFilterId = message.CurrentFilterId;
    }
}
