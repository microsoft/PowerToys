// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public class GlobalLogPageContext : IPageContext
{
    public TaskScheduler Scheduler { get; private init; }

    public void ShowException(Exception ex, string? extensionHint)
    { /*do nothing*/
    }

    public GlobalLogPageContext()
    {
        Scheduler = TaskScheduler.FromCurrentSynchronizationContext();
    }
}
