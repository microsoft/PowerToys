// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.Extensions;

namespace WindowsCommandPalette.Views;

public sealed class FormPageViewModel : PageViewModel
{
    internal IFormPage Page => (IFormPage)this.PageAction;

    public ObservableCollection<FormViewModel> Forms { get; set; } = new();

    public FormPageViewModel(IFormPage page)
        : base(page)
    {
    }

    internal async Task InitialRender()
    {
        // Starting on the main thread...

        // Queue fetching the forms on the BG thread.
        var t = new Task<IForm[]>(() =>
        {
            try
            {
                var forms = this.Page.Forms();
                return forms;
            }
            catch (Exception)
            {
            }

            return [];
        });
        t.Start();
        var forms = await t; // get back on the main thread

        // Back on the main thread here
        foreach (var form in forms)
        {
            var formVm = new FormViewModel(form);
            formVm.RequestSubmitForm += RequestSubmitFormBubbler;
            Forms.Add(formVm); // This needs to be done on the main thread
        }

        foreach (var form in this.Forms)
        {
            form.InitialRender(); // This needs to be done on the main thread
        }
    }

    private void RequestSubmitFormBubbler(object sender, SubmitFormArgs args)
    {
        this.SubmitForm(args.FormData, args.Form);
    }
}
