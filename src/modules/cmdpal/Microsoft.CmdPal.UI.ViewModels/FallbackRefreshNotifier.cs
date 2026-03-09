// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class FallbackRefreshNotifier
{
    private static readonly ThrottledDebouncedAction RefreshAction = new(
        () => WeakReferenceMessenger.Default.Send<UpdateFallbackItemsMessage>(),
        TimeSpan.FromMilliseconds(25));

    public static void RequestRefresh()
    {
        RefreshAction.Invoke();
    }
}
