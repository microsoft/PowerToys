// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class LogMessage : BaseObservable, ILogMessage
{
    private MessageState _messageState = MessageState.Info;

    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged(nameof(Message));
        }
    }

    public MessageState State
    {
        get => _messageState;
        set
        {
            _messageState = value;
            OnPropertyChanged(nameof(State));
        }
    }
}
