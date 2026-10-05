// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Used to do a command - navigate to a page or invoke it
/// </summary>
public record PerformCommandMessage
{
    public ExtensionObject<ICommand> Command { get; }

    public object? CommandContext { get; }

    public SourceContext? Context { get; private set; }

    public bool WithAnimation { get; set; } = true;

    public bool TransientPage { get; set; }

    public Func<ICommandResult, bool>? ResultHandler { get; set; }

    /// <summary>
    /// Optional callback raised by <see cref="ShellViewModel"/> just before a
    /// <see cref="ShowConfirmationMessage"/> is dispatched for this command's
    /// result. Lets the sender prepare UI (for example, the dock uses this to
    /// open the cmdpal window anchored at the invoking dock item so that the
    /// confirmation dialog appears in the right place).
    /// </summary>
    public Action? OnBeforeShowConfirmation { get; set; }

    /// <summary>
    /// When set, and the command turns out to be a page, the main window is
    /// summoned before navigating. Used by senders that run while the palette
    /// is hidden (for example the toast's action button).
    /// </summary>
    public bool ShowWindowIfPage { get; set; }

    /// <summary>
    /// Gets or sets initial state that must be applied before a list page fetches its first items.
    /// </summary>
    public ListPageLaunchOptions? ListPageOptions { get; set; }

    public PerformCommandMessage(ExtensionObject<ICommand> command, PageViewModel? sourcePage = null)
    {
        Command = command;
        Context = sourcePage is null ? null : new(sourcePage);
    }

    public PerformCommandMessage(ExtensionObject<ICommand> command, ExtensionObject<IListItem> context, PageViewModel? sourcePage = null)
        : this(command, sourcePage)
    {
        CommandContext = context.Unsafe;
    }

    public PerformCommandMessage(ExtensionObject<ICommand> command, ExtensionObject<ICommandItem> context, PageViewModel? sourcePage = null)
        : this(command, sourcePage)
    {
        CommandContext = context.Unsafe;
    }

    public PerformCommandMessage(ExtensionObject<ICommand> command, ExtensionObject<ICommandContextItem> context, PageViewModel? sourcePage = null)
        : this(command, sourcePage)
    {
        CommandContext = context.Unsafe;
    }

    public PerformCommandMessage(CommandItemViewModel contextCommand)
        : this(
            contextCommand.Command.Model,
            contextCommand.Model,
            contextCommand.PageContext.TryGetTarget(out var pageContext) ? pageContext as PageViewModel : null)
    {
    }

    public PerformCommandMessage(ConfirmResultViewModel vm, PageViewModel? sourcePage = null)
        : this(vm.PrimaryCommand.Model, sourcePage)
    {
    }
}
