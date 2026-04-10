// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ControlsPageViewModel : PageViewModel
{
    private readonly ExtensionObject<IControlsPage> _model;

    [ObservableProperty]
    public partial ObservableCollection<ControlsSectionViewModel> Sections { get; set; } = [];

    public ControlsPageViewModel(IControlsPage model, TaskScheduler scheduler, AppExtensionHost host, ICommandProviderContext providerContext)
        : base(model, scheduler, host, providerContext)
    {
        _model = new(model);
        PreferredWidth = 200;
    }

    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchSections();

    private void FetchSections()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        List<ControlsSectionViewModel> newSections = [];
        try
        {
            var sections = model.GetSections();
            foreach (var section in sections)
            {
                var items = new List<ControlItemViewModel>();
                foreach (var item in section.GetItems())
                {
                    var vm = new ControlItemViewModel(item, PageContext);
                    vm.InitializeProperties();
                    items.Add(vm);
                }

                newSections.Add(new ControlsSectionViewModel(section.Title, items));
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, _model?.Unsafe?.Name);
            throw;
        }

        DoOnUiThread(() =>
        {
            // Clean up old VMs
            foreach (var section in Sections)
            {
                foreach (var item in section.Items)
                {
                    item.SafeCleanup();
                }
            }

            Sections.Clear();
            foreach (var section in newSections)
            {
                Sections.Add(section);
            }
        });
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        FetchSections();
        model.ItemsChanged += Model_ItemsChanged;
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }

        foreach (var section in Sections)
        {
            foreach (var item in section.Items)
            {
                item.SafeCleanup();
            }
        }
    }
}
