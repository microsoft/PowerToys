// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Timers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable

namespace SamplePagesExtension;

internal sealed partial class SampleLiveDetailsPage : ListPage, IDisposable
{
    private readonly Details _clockDetails = new()
    {
        Title = "Current Time",
        Body = "Loading...",
    };

    private readonly Details _counterDetails = new()
    {
        Title = "Count: 0",
        Body = "Elapsed: 0 seconds",
    };

    private readonly Details _staticDetails = new()
    {
        Title = "Static Details",
        Body = "This item does not update. Select the items above to see live updates in the details pane.",
    };

    private readonly ListItem[] _items;
    private Timer? _timer;
    private int _counter;
    private bool _disposed;

    public SampleLiveDetailsPage()
    {
        Icon = new IconInfo("\uE916"); // Refresh
        Name = Title = "Live Updating Details";
        ShowDetails = true;

        _items = [
            new ListItem(new NoOpCommand())
            {
                Title = "Live Clock",
                Subtitle = "Details pane shows current time, updating every second",
                Details = _clockDetails,
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Counter",
                Subtitle = "Details pane increments a counter every second",
                Details = _counterDetails,
            },
            new ListItem(new NoOpCommand())
            {
                Title = "Static Item",
                Subtitle = "This item's details do not change",
                Details = _staticDetails,
            },
        ];
    }

    public override IListItem[] GetItems()
    {
        if (_timer is null)
        {
            _timer = new Timer(1000);
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        return _items;
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        _counter++;

        // Updating Details properties fires INotifyPropChanged automatically
        // (Details extends BaseObservable). DetailsViewModel picks up the change
        // live without requiring the user to reselect the item.
        _clockDetails.Body = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

        _counterDetails.Title = $"Count: {_counter}";
        _counterDetails.Body = $"Elapsed: {_counter} second{(_counter == 1 ? string.Empty : "s")}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _timer = null;
            _disposed = true;
        }
    }
}
