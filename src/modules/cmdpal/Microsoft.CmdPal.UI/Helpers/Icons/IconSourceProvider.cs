// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class IconSourceProvider : IIconSourceProvider
{
    private readonly IconLoaderService _loader;
    private readonly Size _iconSize;
    private readonly bool _isPriority;

    public IconSourceProvider(IconLoaderService loader, Size iconSize, bool isPriority = false)
    {
        _loader = loader;
        _iconSize = iconSize;
        _isPriority = isPriority;
    }

    public IconSourceProvider(IconLoaderService loader, int iconSize, bool isPriority = false)
        : this(loader, new Size(iconSize, iconSize), isPriority)
    {
    }

    public Task<IconSource?> GetIconSource(IconDataViewModel icon, double scale)
    {
        var tcs = new TaskCompletionSource<IconSource?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _loader.EnqueueLoad(
            icon.Icon,
            icon.FontFamily,
            icon.Data?.Unsafe,
            _iconSize,
            scale,
            tcs,
            _isPriority ? IconLoadPriority.High : IconLoadPriority.Low);

        return tcs.Task;
    }
}
