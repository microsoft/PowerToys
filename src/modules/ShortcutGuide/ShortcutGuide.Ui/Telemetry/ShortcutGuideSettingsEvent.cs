// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ShortcutGuide.Telemetry;

[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class ShortcutGuideSettingsEvent : EventBase, IEvent
{
    public string Hotkey { get; }

    public int WindowsKeyAction { get; }

    public int WindowsKeyPressTime { get; }

    public bool CloseOnWindowsKeyRelease { get; }

    public string Theme { get; }

    public string DisabledApps { get; }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;

    public ShortcutGuideSettingsEvent(
        string hotkey,
        int windowsKeyAction,
        int windowsKeyPressTime,
        bool closeOnWindowsKeyRelease,
        string theme,
        string disabledApps)
    {
        Hotkey = hotkey;
        WindowsKeyAction = windowsKeyAction;
        WindowsKeyPressTime = windowsKeyPressTime;
        CloseOnWindowsKeyRelease = closeOnWindowsKeyRelease;
        Theme = theme;
        DisabledApps = disabledApps;
    }
}
