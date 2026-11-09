// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public record ShowToastMessage(
    string Message,
    IconInfoViewModel? Icon = null,
    CommandViewModel? Command = null)
{
    public TimeSpan? Duration { get; init; }

    public TimeSpan VisibleDuration => Duration ?? TimeSpan.FromMilliseconds(Command is not null ? 5000 : 2500);
}
