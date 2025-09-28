// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common.Commands;

public sealed partial class ConfirmableCommand : InvokableCommand
{
    private readonly IInvokableCommand? _command;

    public Func<bool>? IsConfirmationRequired { get; init; }

    public required string ConfirmationTitle { get; init; }

    public required string ConfirmationMessage { get; init; }

    public required IInvokableCommand Command
    {
        get => _command!;
        init
        {
            if (_command is INotifyPropChanged oldNotifier)
            {
                oldNotifier.PropChanged -= InnerCommand_PropChanged;
            }

            _command = value;

            if (_command is INotifyPropChanged notifier)
            {
                notifier.PropChanged += InnerCommand_PropChanged;
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Icon));
        }
    }

    public override string Name
    {
        get => (_command as Command)?.Name ?? base.Name;
        set
        {
            if (_command is Command cmd)
            {
                cmd.Name = value;
            }
            else
            {
                base.Name = value;
            }
        }
    }

    public override string Id
    {
        get => (_command as Command)?.Id ?? base.Id;
        set
        {
            var previous = Id;
            if (_command is Command cmd)
            {
                cmd.Id = value;
            }
            else
            {
                base.Id = value;
            }

            if (previous != Id)
            {
                OnPropertyChanged(nameof(Id));
            }
        }
    }

    public override IconInfo Icon
    {
        get => (_command as Command)?.Icon ?? base.Icon;
        set
        {
            if (_command is Command cmd)
            {
                cmd.Icon = value;
            }
            else
            {
                base.Icon = value;
            }
        }
    }

    public ConfirmableCommand()
    {
        // Allow init-only construction
    }

    [SetsRequiredMembers]
    public ConfirmableCommand(IInvokableCommand command, string confirmationTitle, string confirmationMessage, Func<bool>? isConfirmationRequired = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationMessage);
        ArgumentNullException.ThrowIfNull(confirmationMessage);

        IsConfirmationRequired = isConfirmationRequired;
        ConfirmationTitle = confirmationTitle;
        ConfirmationMessage = confirmationMessage;
        Command = command;
    }

    private void InnerCommand_PropChanged(object sender, IPropChangedEventArgs args)
    {
        var property = args.PropertyName;

        if (string.IsNullOrEmpty(property) || property == nameof(Name))
        {
            OnPropertyChanged(nameof(Name));
        }

        if (string.IsNullOrEmpty(property) || property == nameof(Id))
        {
            OnPropertyChanged(nameof(Id));
        }

        if (string.IsNullOrEmpty(property) || property == nameof(Icon))
        {
            OnPropertyChanged(nameof(Icon));
        }
    }

    public override ICommandResult Invoke()
    {
        var showConfirmationDialog = IsConfirmationRequired?.Invoke() ?? true;
        if (showConfirmationDialog)
        {
            return CommandResult.Confirm(new ConfirmationArgs
            {
                Title = ConfirmationTitle,
                Description = ConfirmationMessage,
                PrimaryCommand = Command,
                IsPrimaryCommandCritical = true,
            });
        }
        else
        {
            return Command.Invoke(this) ?? CommandResult.Dismiss();
        }
    }
}
