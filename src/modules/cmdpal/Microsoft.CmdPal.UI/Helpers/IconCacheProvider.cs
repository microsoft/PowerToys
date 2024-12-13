// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ExtViews;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Common async event handler provides the cache lookup function for the <see cref="IconBox.SourceRequested"/> deferred event.
/// </summary>
public static partial class IconCacheProvider
{
    private static readonly IconCacheService IconService = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

#pragma warning disable IDE0060 // Remove unused parameter
    public static async void SourceRequested(IconBox sender, SourceRequestedEventArgs args)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (args.Key == null)
        {
            return;
        }

        if (args.Key is IconDataType iconData)
        {
            var deferral = args.GetDeferral();

            args.Value = await IconService.GetIconSource(iconData);

            deferral.Complete();
        }
    }
}
