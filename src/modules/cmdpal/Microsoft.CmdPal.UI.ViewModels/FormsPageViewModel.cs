// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FormsPageViewModel : PageViewModel
{
    private readonly ExtensionObject<IFormPage> _model;

    [ObservableProperty]
    public partial ObservableCollection<FormViewModel> Forms { get; set; } = [];

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public FormsPageViewModel(IFormPage model, TaskScheduler scheduler, CommandPaletteHost host)
        : base(model, scheduler, host)
    {
        _model = new(model);
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchForms()
    {
        var newForms = new List<FormViewModel>();
        try
        {
            var newItems = _model.Unsafe!.Forms();

            foreach (var item in newItems)
            {
                FormViewModel viewModel = new(item, this);
                viewModel.InitializeProperties();

                newForms.Add(viewModel);
            }
        }
        catch (Exception ex)
        {
            ShowException(ex);
            throw;
        }

        // Now, back to a UI thread to update the observable collection
        Task.Factory.StartNew(
            () =>
            {
                ListHelpers.InPlaceUpdateList(Forms, newForms);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            PageContext.Scheduler);
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var listPage = _model.Unsafe;
        if (listPage == null)
        {
            return; // throw?
        }

        FetchForms();
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this._model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        // Do we really not have any here?

        // Should `Forms` be observable? That was what ended up footgunning widgets in DevHome, so :shrug:

        // switch (propertyName)
        // {
        //     case nameof(ShowDetails):
        //         this.ShowDetails = model.ShowDetails;
        //         break;
        //     case nameof(PlaceholderText):
        //         this.PlaceholderText = model.PlaceholderText;
        //         break;
        // }
        UpdateProperty(propertyName);
    }
}
