// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class StatusMessage : BaseObservable, IStatusMessage
{
    public virtual string Message
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Message));
        }
    }

= string.Empty;

    public virtual MessageState State
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(State));
        }
    }

= MessageState.Info;

    public virtual IProgressState? Progress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Progress));
        }
    }
}
