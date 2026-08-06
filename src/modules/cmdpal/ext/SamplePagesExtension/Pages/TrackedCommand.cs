// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

/// <summary>
/// A command whose release by the host is observable.
/// </summary>
/// <remarks>
/// Used both as a list item's primary command and behind each context item, so
/// the counters separate "the host released the item" from "the host released
/// the command hanging off it". It also exercises the weak listener that
/// <c>CommandItem.Command</c> attaches to whatever command it is given.
/// </remarks>
internal sealed partial class TrackedCommand : InvokableCommand
{
    public TrackedCommand(string name)
    {
        Name = name;
        LeakTracker.Commands.OnCreated();
    }

    ~TrackedCommand() => LeakTracker.Commands.OnReleased();

    public override ICommandResult Invoke() => CommandResult.KeepOpen();
}
