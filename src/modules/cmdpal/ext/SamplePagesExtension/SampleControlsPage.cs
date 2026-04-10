// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleControlsPage : ControlsPage
{
    private readonly ControlItem _brightness = new() { Name = "Brightness", Type = ControlType.Slider, Value = 75, Minimum = 0, Maximum = 100, StepValue = 1 };
    private readonly ControlItem _nightLight = new() { Name = "Night Light", Type = ControlType.Toggle, Value = 0 };

    private readonly ControlItem _volume = new() { Name = "Volume", Type = ControlType.Slider, Value = 50, Minimum = 0, Maximum = 100, StepValue = 5 };
    private readonly ControlItem _mute = new() { Name = "Mute", Type = ControlType.Toggle, Value = 0 };

    private readonly ControlItem _wifi = new() { Name = "Wi-Fi", Type = ControlType.Toggle, Value = 1 };
    private readonly ControlItem _bluetooth = new() { Name = "Bluetooth", Type = ControlType.Toggle, Value = 0 };

    public SampleControlsPage()
    {
        Name = "Controls";
        Title = "Quick Settings";
        Icon = new IconInfo("\uE713");
    }

    public override IControlsSection[] GetSections() =>
    [
        new ControlsSection("Display", [_brightness, _nightLight]),
        new ControlsSection("Sound", [_volume, _mute]),
        new ControlsSection("Connectivity", [_wifi, _bluetooth]),
    ];
}
