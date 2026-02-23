// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Common async event handler provides the cache lookup function for the <see cref="IconBox.SourceRequested"/> deferred event.
/// </summary>
public static partial class IconCacheProvider
{
    /*
      Memory Usage Considerations (raw estimates):
      | Icon Size | Per Icon | Count |    Total | Per Icon @ 200% | Total @ 200% | Per Icon @ 300% | Total @ 300% |
      | --------- | -------: | ----: | -------: | --------------: | -----------: | --------------: | -----------: |
      | 20×20     |   1.6 KB |  1024 |   1.6 MB |          6.4 KB |       6.4 MB |         14.4 KB |      14.4 MB |
      | 32×32     |   4.0 KB |   512 |   2.0 MB |           16 KB |       8.0 MB |         36.0 KB |      18.0 MB |
      | 48×48     |   9.0 KB |   256 |   2.3 MB |           36 KB |       9.0 MB |         81.0 KB |      20.3 MB |
      | 64×64     |  16.0 KB |    64 |   1.0 MB |           64 KB |       4.0 MB |        144.0 KB |       9.0 MB |
      | 256×256   | 256.0 KB |    64 |  16.0 MB |            1 MB |      64.0 MB |          2.3 MB |       144 MB |
    */

    private static IIconSourceProvider _provider20 = null!;
    private static IIconSourceProvider _provider32 = null!;
    private static IIconSourceProvider _provider64 = null!;
    private static IIconSourceProvider _provider256 = null!;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _provider20 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size20);
        _provider32 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size32);
        _provider64 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size64);
        _provider256 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size256);
    }

    private static async void SourceRequestedCore(IIconSourceProvider service, SourceRequestedEventArgs args)
    {
        if (args.Key is null)
        {
            return;
        }

        var deferral = args.GetDeferral();

        try
        {
            args.Value = args.Key switch
            {
                IconDataViewModel iconData => await service.GetIconSource(iconData, args.Scale),
                IconInfoViewModel iconInfo => await service.GetIconSource(
                    args.Theme == Microsoft.UI.Xaml.ElementTheme.Light ? iconInfo.Light : iconInfo.Dark,
                    args.Scale),
                _ => null,
            };
        }
        finally
        {
            deferral.Complete();
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public static void SourceRequested20(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider20, args);

    public static void SourceRequested32(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider32, args);

    public static void SourceRequested64(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider64, args);

    public static void SourceRequested256(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider256, args);
#pragma warning restore IDE0060 // Remove unused parameter
}
