// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FormsPageViewModel : PageViewModel
{
    private readonly ExtensionObject<IFormPage> _model;

    [ObservableProperty]
    public partial ObservableCollection<FormViewModel> Forms { get; set; } = [];

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public FormsPageViewModel(IFormPage model, TaskScheduler scheduler)
        : base(model, scheduler)
    {
        _model = new(model);
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchForms()
    {
        try
        {
            var newItems = _model.Unsafe!.Forms();

            Forms.Clear();

            foreach (var item in newItems)
            {
                FormViewModel viewModel = new(item, this);
                viewModel.InitializeProperties();
                Forms.Add(viewModel);
            }
        }
        catch (Exception ex)
        {
            ShowException(ex);
            throw;
        }
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

        // Should `Forms` be observable? That was what ended up footgunning widgets in DevHome, so :shurg:

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
