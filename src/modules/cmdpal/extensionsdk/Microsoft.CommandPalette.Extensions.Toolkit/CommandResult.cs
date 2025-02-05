// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandResult : ICommandResult
{
    public ICommandResultArgs? Args { get; private set; }

    public CommandResultKind Kind { get; private set; } = CommandResultKind.Dismiss;

    public static CommandResult Dismiss()
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.Dismiss,
        };
    }

    public static CommandResult GoHome()
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.GoHome,
            Args = null,
        };
    }

    public static CommandResult GoBack()
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.GoBack,
            Args = null,
        };
    }

    public static CommandResult Hide()
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.Hide,
            Args = null,
        };
    }

    public static CommandResult KeepOpen()
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.KeepOpen,
            Args = null,
        };
    }

    public static CommandResult GoToPage(GoToPageArgs args)
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.GoToPage,
            Args = args,
        };
    }

    public static CommandResult ShowToast(ToastArgs args)
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.ShowToast,
            Args = args,
        };
    }

    public static CommandResult ShowToast(string message)
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.ShowToast,
            Args = new ToastArgs() { Message = message },
        };
    }

    public static CommandResult Confirm(ConfirmationArgs args)
    {
        return new CommandResult()
        {
            Kind = CommandResultKind.Confirm,
            Args = args,
        };
    }
}
