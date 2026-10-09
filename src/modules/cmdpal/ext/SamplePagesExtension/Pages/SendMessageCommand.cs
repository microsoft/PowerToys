// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace SamplePagesExtension;

internal sealed partial class SendMessageCommand : InvokableCommand
{
    private static int sentMessages;

    public override ICommandResult Invoke()
    {
        var kind = MessageState.Info;
        switch (sentMessages % 4)
        {
            case 0: kind = MessageState.Info; break;
            case 1: kind = MessageState.Success; break;
            case 2: kind = MessageState.Warning; break;
            case 3: kind = MessageState.Error; break;
        }

        var message = new StatusMessage() { Message = $"I am status message no.{sentMessages++}", State = kind };
        ExtensionHost.ShowStatus(message, StatusContext.Page);
        return CommandResult.KeepOpen();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code sometimes makes more sense in a single file")]
internal sealed partial class SendSingleMessageItem : ListItem
{
    private readonly SingleMessageCommand _command;

    public SendSingleMessageItem()
        : base(new SingleMessageCommand())
    {
        Title = "I send a single message";
        Title = "This demonstrates both showing and hiding a single message";

        _command = (SingleMessageCommand)Command;
        _command.UpdateListItem += (sender, args) =>
        {
            Title = _command.Shown ? "Hide message" : "I send a single message";
        };
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code sometimes makes more sense in a single file")]
internal sealed partial class SingleMessageCommand : InvokableCommand
{
    public event TypedEventHandler<SingleMessageCommand, object> UpdateListItem;

    private readonly StatusMessage _myMessage = new() { Message = "I am a status message" };

    public bool Shown { get; private set; }

    public override ICommandResult Invoke()
    {
        if (Shown)
        {
            ExtensionHost.HideStatus(_myMessage);
        }
        else
        {
            ExtensionHost.ShowStatus(_myMessage, StatusContext.Page);
        }

        Shown = !Shown;
        Name = Shown ? "Hide" : "Show";
        UpdateListItem?.Invoke(this, null);
        return CommandResult.KeepOpen();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code sometimes makes more sense in a single file")]
internal sealed partial class IndeterminateProgressMessageCommand : InvokableCommand
{
    private readonly StatusMessage _myMessage = new()
    {
        Message = "Doing the thing...",
        Progress = new ProgressState() { IsIndeterminate = true },
    };

    private enum State
    {
        NotStarted,
        Started,
        Done,
    }

    private State _state;

    public IndeterminateProgressMessageCommand()
    {
        this.Name = "Do the thing";
    }

    public override ICommandResult Invoke()
    {
        if (_state == State.NotStarted)
        {
            ExtensionHost.ShowStatus(_myMessage, StatusContext.Page);
            _ = Task.Run(() =>
            {
                Thread.Sleep(3000);

                _state = State.Done;
                _myMessage.State = MessageState.Success;
                _myMessage.Message = "Did the thing!";
                _myMessage.Progress = null;

                Thread.Sleep(3000);
                ExtensionHost.HideStatus(_myMessage);
                _state = State.NotStarted;

                _myMessage.State = MessageState.Info;
                _myMessage.Message = "Doing the thing...";
                _myMessage.Progress = new ProgressState() { IsIndeterminate = true };
            });
            _state = State.Started;
        }
        else if (_state == State.Started)
        {
            ExtensionHost.ShowStatus(_myMessage, StatusContext.Page);
        }

        return CommandResult.KeepOpen();
    }
}
