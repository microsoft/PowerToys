// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

#nullable enable

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleUpdatingIconPage : ListPage, INotifyItemsChanged, IDisposable
{
    private const int IconSize = 64;
    private const int FrameCount = 60;
    private readonly object _lifecycle = new();
    private readonly ListItem _normalItem = new(new NoOpCommand())
    {
        Title = "Updating PNG progress",
        Subtitle = "A new PNG stream every 500 ms",
        Icon = new IconInfo("\uE916"),
    };

    private readonly ListItem _delayedItem = new(new NoOpCommand())
    {
        Title = "Updating PNG progress with slow loading",
        Subtitle = "An 800 ms load should still advance while updates arrive every 500 ms",
        Icon = new IconInfo("\uE916"),
    };

    private readonly ListItem _pauseItem;
    private readonly IListItem[] _items;
    private readonly Timer _timer;
    private TypedEventHandler<object, IItemsChangedEventArgs>? _itemsChangedHandlers;
    private int _subscribers;
    private int _updating;
    private int _frame;
    private volatile bool _paused;
    private volatile bool _disposed;

    public SampleUpdatingIconPage()
    {
        Name = Title = "Updating Progress Icons";
        Icon = new IconInfo("\uE916");
        PlaceholderText = "Scroll down and back to check recycled icons";
        _pauseItem = new ListItem(new AnonymousCommand(TogglePause)
        {
            Name = "Toggle animation",
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = "Pause animation",
            Icon = new IconInfo("\uE769"),
        };

        _items = new IListItem[83];
        _items[0] = _normalItem;
        _items[1] = _delayedItem;
        _items[2] = _pauseItem;
        for (var i = 3; i < _items.Length; i++)
        {
            _items[i] = new ListItem(new NoOpCommand())
            {
                Title = $"Static row {i - 2}",
                Subtitle = "Scroll back to the progress icons; this row should keep its folder icon",
                Icon = new IconInfo("\uE8B7"),
            };
        }

        _timer = new Timer(UpdateFrame, null, Timeout.Infinite, Timeout.Infinite);
    }

    event TypedEventHandler<object, IItemsChangedEventArgs> INotifyItemsChanged.ItemsChanged
    {
        add
        {
            lock (_lifecycle)
            {
                if (_disposed || value is null)
                {
                    return;
                }

                var hadSubscribers = _itemsChangedHandlers is not null;
                _itemsChangedHandlers += value;
                ItemsChanged += value;
                Volatile.Write(ref _subscribers, _itemsChangedHandlers.GetInvocationList().Length);
                if (!hadSubscribers)
                {
                    _timer.Change(0, 500);
                }
            }
        }

        remove
        {
            lock (_lifecycle)
            {
                var remainingHandlers = (TypedEventHandler<object, IItemsChangedEventArgs>?)Delegate.Remove(_itemsChangedHandlers, value);
                if (ReferenceEquals(remainingHandlers, _itemsChangedHandlers))
                {
                    return;
                }

                _itemsChangedHandlers = remainingHandlers;
                ItemsChanged -= value;
                Volatile.Write(ref _subscribers, remainingHandlers?.GetInvocationList().Length ?? 0);
                if (_subscribers == 0 && !_disposed)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }
    }

    public override IListItem[] GetItems()
    {
        return _items;
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            if (_itemsChangedHandlers is not null)
            {
                foreach (var handler in _itemsChangedHandlers.GetInvocationList())
                {
                    ItemsChanged -= (TypedEventHandler<object, IItemsChangedEventArgs>)handler;
                }

                _itemsChangedHandlers = null;
                Volatile.Write(ref _subscribers, 0);
            }
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Title = _paused ? "Resume animation" : "Pause animation";
    }

    private async void UpdateFrame(object? state)
    {
        if (_disposed || _paused || Volatile.Read(ref _subscribers) == 0 || Interlocked.Exchange(ref _updating, 1) != 0)
        {
            return;
        }

        try
        {
            var frame = (_frame + 1) % (FrameCount + 1);
            var stream = await CreateFrameAsync(frame).ConfigureAwait(false);
            if (_disposed || _paused || Volatile.Read(ref _subscribers) == 0)
            {
                stream.Dispose();
                return;
            }

            // Fresh identities exercise uncached loads; production animations should cache a bounded set of frames.
            // Published streams remain alive through their references while the host may still open them.
            var normal = new IconInfo(new IconData(new ProgressStreamReference(stream, delayMilliseconds: 0)));
            var delayed = new IconInfo(new IconData(new ProgressStreamReference(stream, delayMilliseconds: 800)));
            _frame = frame;
            _normalItem.Icon = normal;
            _delayedItem.Icon = delayed;
            _normalItem.Subtitle = $"Frame {frame}/{FrameCount}; fresh PNG stream every 500 ms";
            _delayedItem.Subtitle = $"Latest frame {frame}/{FrameCount}; 800 ms loads should keep advancing without flashing";
        }
        catch (Exception exception)
        {
            _paused = true;
            _normalItem.Subtitle = $"Image generation failed: {exception.Message}";
            _pauseItem.Title = "Retry animation";
        }
        finally
        {
            Volatile.Write(ref _updating, 0);
        }
    }

    private static async Task<InMemoryRandomAccessStream> CreateFrameAsync(int frame)
    {
        var pixels = new byte[IconSize * IconSize * 4];
        var sweep = frame * Math.Tau / FrameCount;
        for (var y = 0; y < IconSize; y++)
        {
            for (var x = 0; x < IconSize; x++)
            {
                var dx = x + 0.5 - (IconSize / 2d);
                var dy = y + 0.5 - (IconSize / 2d);
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var coverage = Math.Clamp(28.5 - distance, 0, 1) * Math.Clamp(distance - 18.5, 0, 1);
                var angle = Math.Atan2(dx, -dy);
                if (angle < 0)
                {
                    angle += Math.Tau;
                }

                var filled = angle < sweep;
                var offset = ((y * IconSize) + x) * 4;
                var alpha = (byte)Math.Round(coverage * 255);
                pixels[offset] = (byte)((filled ? 230 : 110) * alpha / 255);
                pixels[offset + 1] = (byte)((filled ? 160 : 110) * alpha / 255);
                pixels[offset + 2] = (byte)((filled ? 40 : 110) * alpha / 255);
                pixels[offset + 3] = alpha;
            }
        }

        var stream = new InMemoryRandomAccessStream();
        try
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, IconSize, IconSize, 96, 96, pixels);
            await encoder.FlushAsync();
            stream.Seek(0);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
