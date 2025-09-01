// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.Terminal.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

public sealed class IconCacheService(DispatcherQueue dispatcherQueue)
{
    public Task<IconSource?> GetIconSource(IconDataViewModel icon) =>

        // todo: actually implement a cache of some sort
        IconToSource(icon);

    private async Task<IconSource?> IconToSource(IconDataViewModel icon)
    {
        try
        {
            if (!string.IsNullOrEmpty(icon.Icon))
            {
                var source = IconPathConverter.IconSourceMUX(icon.Icon, false, icon.FontFamily);
                return source;
            }
            else if (icon.Data is not null)
            {
                try
                {
                    return await StreamToIconSource(icon.Data.Unsafe!);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to load icon from stream: " + ex);
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private async Task<IconSource?> StreamToIconSource(IRandomAccessStreamReference iconStreamRef)
    {
        if (iconStreamRef is null)
        {
            return null;
        }

        var bitmap = await IconStreamToBitmapImageAsync(iconStreamRef);
        var icon = new ImageIconSource() { ImageSource = bitmap };
        return icon;
    }

    private async Task<BitmapImage> IconStreamToBitmapImageAsync(IRandomAccessStreamReference iconStreamRef)
    {
        // Return the bitmap image via TaskCompletionSource. Using WCT's EnqueueAsync does not suffice here, since if
        // we're already on the thread of the DispatcherQueue then it just directly calls the function, with no async involved.
        return await TryEnqueueAsync(dispatcherQueue, async () =>
        {
            using var bitmapStream = await iconStreamRef.OpenReadAsync();
            var itemImage = new BitmapImage();
            await itemImage.SetSourceAsync(bitmapStream);
            return itemImage;
        });
    }

    private static Task<T> TryEnqueueAsync<T>(DispatcherQueue dispatcher, Func<Task<T>> function)
    {
        var completionSource = new TaskCompletionSource<T>();

        var enqueued = dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, async void () =>
        {
            try
            {
                var result = await function();
                completionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        });

        if (!enqueued)
        {
            completionSource.SetException(new InvalidOperationException("Failed to enqueue the operation on the UI dispatcher"));
        }

        return completionSource.Task;
    }
}
