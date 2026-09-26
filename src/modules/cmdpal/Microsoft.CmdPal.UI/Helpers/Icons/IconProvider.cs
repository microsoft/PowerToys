// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Common async event handler provides the cache lookup function for the <see cref="IconBox.SourceRequested"/> deferred event.
/// </summary>
public static partial class IconProvider
{
    private static readonly Uri AppIconFallbackUri = new("ms-appx:///Assets/Icons/AppIconFallback.svg");

    private static IIconSourceProvider _provider16 = null!;
    private static IIconSourceProvider _provider20 = null!;
    private static IIconSourceProvider _provider32 = null!;
    private static IIconSourceProvider _provider64 = null!;
    private static IIconSourceProvider _provider256 = null!;
    private static IIconSourceProvider _providerUnbound = null!;
    private static ImageIconSource? _appIconFallbackSource;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _provider16 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size16);
        _provider20 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size20);
        _provider32 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size32);
        _provider64 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size64);
        _provider256 = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Size256);
        _providerUnbound = serviceProvider.GetRequiredKeyedService<IIconSourceProvider>(WellKnownIconSize.Unbound);
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
            var iconData = args.Key switch
            {
                IconDataViewModel value => value,
                IconInfoViewModel value => value.IconForTheme(args.Theme == ElementTheme.Light),
                _ => null,
            };
            if (iconData is not null && AppIconProtocol.IsProtocol(iconData.Icon))
            {
                args.FallbackSource = _appIconFallbackSource ??= new ImageIconSource
                {
                    ImageSource = new SvgImageSource(AppIconFallbackUri),
                };
                args.ExpectsImageSource = true;
            }

            args.Value = iconData is null
                ? null
                : await service.GetIconSource(
                    iconData,
                    args.Scale,
                    args.Diagnostics,
                    args,
                    args.Theme);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to provide icon source", ex);
            args.Value = null;
        }
        finally
        {
            deferral.Complete();
        }
    }

#pragma warning disable IDE0060 // Remove unused parameter
    public static void SourceRequested16(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider16, args);

    public static void SourceRequested20(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider20, args);

    public static void SourceRequested32(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider32, args);

    public static void SourceRequested64(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider64, args);

    public static void SourceRequested256(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_provider256, args);

    public static void SourceRequestedOriginal(IconBox sender, SourceRequestedEventArgs args)
        => SourceRequestedCore(_providerUnbound, args);
#pragma warning restore IDE0060 // Remove unused parameter
}
