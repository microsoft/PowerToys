// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// A sample dock band with multiple buttons.
/// Includes separate live examples for label width, tabular digits, and trailing alignment.
/// </summary>
internal sealed partial class SampleButtonsDockBand : WrappedDockItem, IDisposable
{
    private static readonly string[] SizeConstraintValues = [
        "1%",
        "12.3%",
        "99.99%",
        "CPU 100% | 32 cores at 4.80 GHz | 256 active threads",
    ];

    private static readonly double[] TabularValues = [11.11, 88.88, 44.44, 77.77];
    private static readonly double[] AlignmentValues = [1, 12, 100, 999];

    private readonly ListItem _sizeConstraintItem;
    private readonly ListItem _tabularDigitsItem;
    private readonly ListItem _trailingAlignmentItem;
    private readonly Timer _timer;
    private readonly Lock _updateLock = new();
    private int _sampleIndex;
    private bool _disposed;

    public SampleButtonsDockBand()
        : base([], "com.microsoft.cmdpal.samples.buttons_band", "Sample Buttons Band")
    {
        _sizeConstraintItem = new ListItem(new ShowToastCommand("This changing label keeps a fixed 12ch width."))
        {
            Title = SizeConstraintValues[0],
            Subtitle = "Fixed width",
        }
        .SetDockLabelWidth("12ch");

        _tabularDigitsItem = new ListItem(new ShowToastCommand("Equal-length values use tabular digits without changing alignment."))
        {
            Title = FormatPercentage(TabularValues[0]),
            Subtitle = "Tabular digits",
        }
        .SetDockLabelTabularDigits();

        _trailingAlignmentItem = new ListItem(new ShowToastCommand("Changing values align to the trailing edge of a fixed slot."))
        {
            Title = FormatPercentage(AlignmentValues[0]),
            Subtitle = "Trailing aligned",
        }
        .SetDockLabelWidth("12ch")
        .SetDockLabelTrailingAlignment();

        ListItem[] buttons = [
            new(new ShowToastCommand("Button 1")) { Title = "1" },
            _sizeConstraintItem,
            _tabularDigitsItem,
            _trailingAlignmentItem,
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

            _sampleIndex = (_sampleIndex + 1) % SizeConstraintValues.Length;

            // ListItem raises PropChanged; keep the items and band list intact on each tick.
            _sizeConstraintItem.Title = SizeConstraintValues[_sampleIndex];
            _tabularDigitsItem.Title = FormatPercentage(TabularValues[_sampleIndex]);
            _trailingAlignmentItem.Title = FormatPercentage(AlignmentValues[_sampleIndex]);
        }
    }

    private static string FormatPercentage(double value) => $"{value:F2}%";

    public void Dispose()
    {
        lock (_updateLock)
        {
            _disposed = true;
            _timer.Dispose();
        }
    }
}
