// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class StatusMessageViewModel : ExtensionObjectViewModel
{
    public ExtensionObject<IStatusMessage> Model { get; }

    public string Message { get; private set; } = string.Empty;

    public MessageState State { get; private set; } = MessageState.Info;

    public ProgressViewModel? Progress { get; private set; }

    public bool HasProgress => Progress != null;

    public StatusMessageViewModel(IStatusMessage message, WeakReference<IPageContext> context)
        : base(context)
    {
        Model = new(message);
    }

    public override void InitializeProperties()
    {
        IStatusMessage? model = Model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        Message = model.Message;
        State = model.State;
        IProgressState modelProgress = model.Progress;
        if (modelProgress != null)
        {
            Progress = new(modelProgress, this.PageContext);
            Progress.InitializeProperties();
            UpdateProperty(nameof(HasProgress));
        }

        model.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected virtual void FetchProperty(string propertyName)
    {
        IStatusMessage? model = this.Model.Unsafe;
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
            case nameof(Progress):
                IProgressState modelProgress = model.Progress;
                if (modelProgress != null)
                {
                    Progress = new(modelProgress, this.PageContext);
                    Progress.InitializeProperties();
                }
                else
                {
                    Progress = null;
                }

                UpdateProperty(nameof(HasProgress));
                break;
        }

        UpdateProperty(propertyName);
    }
}
