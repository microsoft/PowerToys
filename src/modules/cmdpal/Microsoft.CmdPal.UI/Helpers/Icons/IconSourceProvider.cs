// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
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
        IIconRequestDemand? demand = null)
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
}
