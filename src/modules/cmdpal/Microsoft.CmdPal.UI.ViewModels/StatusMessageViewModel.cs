// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class StatusMessageViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IStatusMessage> _model;

    public string Message { get; private set; } = string.Empty;

    public MessageState State { get; private set; } = MessageState.Info;

    public string ExtensionPfn { get; set; } = string.Empty;

    public StatusMessageViewModel(IStatusMessage message, IPageContext context)
        : base(context)
    {
        _model = new(message);
    }

    public override void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        Message = model.Message;
        State = model.State;

        model.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, PropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            PageContext.ShowException(ex);
        }
    }

    protected virtual void FetchProperty(string propertyName)
    {
        var model = this._model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Message):
                this.Message = model.Message;
                break;
            case nameof(State):
                this.State = model.State;
                break;
        }

        UpdateProperty(propertyName);
    }
}
