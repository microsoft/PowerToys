// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// A sample dock band with multiple buttons.
/// Includes a simulated performance value that changes every second within a fixed label width.
/// </summary>
internal sealed partial class SampleButtonsDockBand : WrappedDockItem, IDisposable
{
    private static readonly string[] SampleValues = [
        "1%",
        "12.3%",
        "99.99%",
        "CPU 100% | 32 cores at 4.80 GHz | 256 active threads",
    ];

    private readonly ListItem _performanceItem;
    private readonly Timer _timer;
    private readonly Lock _updateLock = new();
    private int _sampleIndex;
    private bool _disposed;

    public SampleButtonsDockBand()
        : base([], "com.microsoft.cmdpal.samples.buttons_band", "Sample Buttons Band")
    {
        _performanceItem = new ListItem(new ShowToastCommand("Simulated CPU values change every second. This button keeps a fixed width."))
        {
            Title = SampleValues[0],
            Subtitle = "CPU sample",
        }.SetDockLabelWidth("12ch");

        ListItem[] buttons = [
            new(new ShowToastCommand("Button 1")) { Title = "1" },
            _performanceItem,
            new(new ShowToastCommand("Button B")) { Icon = new IconInfo("\uF094") }, // B button
            new(new ShowToastCommand("Button 3")) { Title = "Items have Icons &", Icon = new IconInfo("\uED1E"), Subtitle = "titles & subtitles" }, // Subtitles
        ];
        Icon = new IconInfo("\uEECA"); // ButtonView2
        Items = buttons;

        _timer = new Timer(UpdateSampleValue, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void UpdateSampleValue(object state)
    {
        lock (_updateLock)
        {
            if (_disposed)
            {
                return;
            }

            _sampleIndex = (_sampleIndex + 1) % SampleValues.Length;

            // ListItem raises PropChanged; keep the item and band list intact on each tick.
            _performanceItem.Title = SampleValues[_sampleIndex];
        }
    }

    public void Dispose()
    {
        lock (_updateLock)
        {
            _disposed = true;
            _timer.Dispose();
        }
    }
}
