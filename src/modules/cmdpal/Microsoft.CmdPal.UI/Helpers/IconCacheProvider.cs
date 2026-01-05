// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.UI.Dispatching;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Common async event handler provides the cache lookup function for the <see cref="IconBox.SourceRequested"/> deferred event.
/// </summary>
public static partial class IconCacheProvider
{
    private static readonly IconCacheService IconService20 = new(DispatcherQueue.GetForCurrentThread(), new Size(20, 20), 1024);
    private static readonly IconCacheService IconService64 = new(DispatcherQueue.GetForCurrentThread(), new Size(64, 64), 64);

#pragma warning disable IDE0060 // Remove unused parameter
    public static async void SourceRequested(IconBox sender, SourceRequestedEventArgs args)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (args.Key is null)
        {
            return;
        }

        if (args.Key is IconDataViewModel iconData)
        {
            var deferral = args.GetDeferral();
            args.Value = await IconService20.GetIconSource(iconData, args.Scale);

            deferral.Complete();
        }
        else if (args.Key is IconInfoViewModel iconInfo)
        {
            var deferral = args.GetDeferral();

            // per ElementTheme docs: on Windows, setting RequestedTheme to Default will always result in "Dark" being the theme
            var data = args.Theme == Microsoft.UI.Xaml.ElementTheme.Light ? iconInfo.Light : iconInfo.Dark;
            args.Value = await IconService20.GetIconSource(data, args.Scale);

            deferral.Complete();
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public static async void SourceRequestedJumbo(IconBox sender, SourceRequestedEventArgs args)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (args.Key is null)
        {
            return;
        }

        if (args.Key is IconDataViewModel iconData)
        {
            var deferral = args.GetDeferral();

            args.Value = await IconService64.GetIconSource(iconData, args.Scale);

            deferral.Complete();
        }
        else if (args.Key is IconInfoViewModel iconInfo)
        {
            var deferral = args.GetDeferral();

            // per ElementTheme docs: on Windows, setting RequestedTheme to Default will always result in "Dark" being the theme
            var data = args.Theme == Microsoft.UI.Xaml.ElementTheme.Light ? iconInfo.Light : iconInfo.Dark;
            args.Value = await IconService64.GetIconSource(data, args.Scale);

            deferral.Complete();
        }
    }
}
