// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Windows.Foundation;

namespace DeveloperCommandPalette;

public class PageViewModel
{
    private bool nested;

    public bool Nested { get => nested; set => nested = value; }

    protected IPage pageAction { get; }

    // public IPage PageAction { get => pageAction; set => pageAction = value; }
    public ActionViewModel Command { get; }

    public event TypedEventHandler<object, ActionViewModel>? RequestDoAction;

    public event TypedEventHandler<object, SubmitFormArgs>? RequestSubmitForm;

    public event TypedEventHandler<object, object>? RequestGoBack;

    protected PageViewModel(IPage page)
    {
        this.pageAction = page;
        this.Command = new(page);
    }

    public void DoAction(ActionViewModel action)
    {
        var handlers = RequestDoAction;
        handlers?.Invoke(this, action);
    }

    public void GoBack()
    {
        var handlers = RequestGoBack;
        handlers?.Invoke(this, new());
    }

    public void SubmitForm(string formData, IForm form)
    {
        var handlers = RequestSubmitForm;
        handlers?.Invoke(this, new() { FormData = formData, Form = form });
    }
}
