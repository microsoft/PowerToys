// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class CommandInvokingEventArgs(CommandItemViewModel command, PerformCommandMessage message) : EventArgs
{
    public CommandItemViewModel Command { get; } = command;

    public PerformCommandMessage Message { get; } = message;

    /// <summary>
    /// Gets or sets a value indicating whether a subscriber has taken over delivery of
    /// <see cref="Message"/>. When set, the context menu does not send it.
    /// </summary>
    public bool Handled { get; set; }
}
