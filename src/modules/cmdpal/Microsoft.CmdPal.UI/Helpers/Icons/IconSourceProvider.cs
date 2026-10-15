// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class IconSourceProvider : IIconSourceProvider
{
    private readonly IIconLoaderService _loader;
    private readonly Size _iconSize;
    private readonly bool _isPriority;

    public IconSourceProvider(IIconLoaderService loader, Size iconSize, bool isPriority = false)
    {
        _loader = loader;
        _iconSize = iconSize;
        _isPriority = isPriority;
    }

    public IconSourceProvider(IIconLoaderService loader, int iconSize, bool isPriority = false)
        : this(loader, new Size(iconSize, iconSize), isPriority)
    {
    }

    public Task<IconSource?> GetIconSource(
        IconDataViewModel icon,
        double scale,
        IconRequestMeasurement diagnostics = default,
        IIconRequestDemand? demand = null,
        ElementTheme theme = ElementTheme.Default)
    {
        var tcs = new TaskCompletionSource<IconSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        IconLoadMeasurement? loadDiagnostics = null;

        try
        {
            var streamReference = icon.Data?.Unsafe;
            loadDiagnostics = IconLoadDiagnostics.CreateLoad(
                diagnostics,
                icon.Icon,
                streamReference is not null,
                _iconSize.Width,
                _iconSize.Height,
                scale);
            diagnostics.RecordProviderResolution(IconProviderResolution.NewLoad, loadDiagnostics);

            if (icon.Icon is { } iconString
                && (streamReference is null
                    || Microsoft.CommandPalette.Extensions.Toolkit.ShellItemIconProtocol.IsProtocol(iconString)))
            {
                if (TryGetShellItemRequest(
                        iconString,
                        out var shellRequest,
                        out var locatedIcon,
                        out var locationCacheHit))
                {
                    var shellDiagnostics = IconLoadDiagnostics.BeginShellIconRequest(shellRequest);
                    if (locationCacheHit)
                    {
                        shellDiagnostics.LocationCacheHit();
                    }
                    else
                    {
                        shellDiagnostics.LocationCacheMiss();
                    }

                    shellDiagnostics.CanonicalNewLoad();
                    var shellDemand = new IconLoadDemand();
                    shellDemand.Attach(demand);
                    if (!_loader.TryEnqueueShellItemLoad(
                            shellRequest,
                            locatedIcon,
                            _iconSize,
                            scale,
                            tcs,
                            _isPriority ? IconLoadPriority.High : IconLoadPriority.Low,
                            loadDiagnostics,
                            shellDemand,
                            shellDiagnostics: shellDiagnostics))
                    {
                        tcs.TrySetException(new ObjectDisposedException(nameof(IIconLoaderService)));
                    }

                    return tcs.Task;
                }
            }

            if (_loader.TryLoadGlyph(icon.Icon, icon.FontFamily, _iconSize, scale, out var glyph) && glyph is not null)
            {
                loadDiagnostics?.CompleteDirectGlyph(glyph);
                tcs.TrySetResult(glyph);
                return tcs.Task;
            }

            var loadDemand = new IconLoadDemand();
            loadDemand.Attach(demand);

            if (!_loader.TryEnqueueLoad(
                    icon.Icon,
                    icon.FontFamily,
                    streamReference,
                    _iconSize,
                    scale,
                    theme,
                    tcs,
                    _isPriority ? IconLoadPriority.High : IconLoadPriority.Low,
                    loadDiagnostics,
                    loadDemand))
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(IIconLoaderService)));
            }
        }
        catch (Exception ex)
        {
            loadDiagnostics?.Rejected();
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    internal bool TryGetShellItemRequest(
        string iconString,
        out ShellItemIconRequest request,
        out LocatedShellIcon? locatedIcon,
        out bool locationCacheHit)
    {
        var canProbeBeforeClassification =
            Microsoft.CommandPalette.Extensions.Toolkit.ShellItemIconProtocol.IsProtocol(iconString)
            || iconString.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
        if (canProbeBeforeClassification
            && _loader.ShellIconLocations.TryGet(iconString, out var cachedLocation))
        {
            request = cachedLocation.Request;
            locatedIcon = cachedLocation;
            locationCacheHit = true;
            return true;
        }

        locatedIcon = null;
        locationCacheHit = false;
        if (!ShellItemIconRequestClassifier.TryClassify(iconString, out request))
        {
            return false;
        }

        // Protocol and file-URI requests already probed by their submitted cache key.
        // Legacy paths have to be classified first so ordinary bitmap paths and glyphs
        // keep their existing loaders, then they can share the same cross-size alias cache.
        if (!canProbeBeforeClassification
            && _loader.ShellIconLocations.TryGet(request, out cachedLocation))
        {
            locatedIcon = cachedLocation;
            locationCacheHit = true;
        }

        return true;
    }
}
