// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using AdaptiveCards.Templating;
using DeveloperCommandPalette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;
using Windows.System;
using Windows.UI.ViewManagement;

namespace WindowsCommandPalette.Views;

public sealed class FormPageViewModel : PageViewModel
{
    internal IFormPage Page => (IFormPage)this.pageAction;

    internal ObservableCollection<FormViewModel> Forms = new();

    public FormPageViewModel(IFormPage page)
        : base(page)
    {
    }

    internal async Task InitialRender()
    {
        var t = new Task<bool>(() =>
        {
            try
            {
                var f = this.Page.Forms();
                foreach (var form in f)
                {
                    var formVm = new FormViewModel(form);
                    formVm.RequestSubmitForm += RequestSubmitFormBubbler;
                    Forms.Add(formVm);
                }
            }
            catch (Exception)
            {
            }

            foreach (var form in this.Forms)
            {
                form.InitialRender();
            }

            return true;
        });
        t.Start();
        await t;
    }

    private void RequestSubmitFormBubbler(object sender, SubmitFormArgs args)
    {
        this.SubmitForm(args.FormData, args.Form);
    }
}
