// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FancyZonesCLI;

// JSON Source Generator for AOT compatibility
[JsonSerializable(typeof(LayoutTemplates))]
[JsonSerializable(typeof(CustomLayouts))]
[JsonSerializable(typeof(AppliedLayouts))]
[JsonSerializable(typeof(LayoutHotkeys))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class FancyZonesJsonContext : JsonSerializerContext
{
}

// Layout Templates
public sealed class LayoutTemplates
{
    [JsonPropertyName("layout-templates")]
    public List<TemplateLayout> Templates { get; set; }
}

public sealed class TemplateLayout
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("zone-count")]
    public int ZoneCount { get; set; }

    [JsonPropertyName("show-spacing")]
    public bool ShowSpacing { get; set; }

    [JsonPropertyName("spacing")]
    public int Spacing { get; set; }

    [JsonPropertyName("sensitivity-radius")]
    public int SensitivityRadius { get; set; }
}

// Custom Layouts
public sealed class CustomLayouts
{
    [JsonPropertyName("custom-layouts")]
    public List<CustomLayout> Layouts { get; set; }
}

public sealed class CustomLayout
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("info")]
    public JsonElement Info { get; set; }
}

// Applied Layouts
public sealed class AppliedLayouts
{
    [JsonPropertyName("applied-layouts")]
    public List<AppliedLayoutWrapper> Layouts { get; set; }
}

public sealed class AppliedLayoutWrapper
{
    [JsonPropertyName("device")]
    public DeviceInfo Device { get; set; } = new();

    [JsonPropertyName("applied-layout")]
    public AppliedLayoutInfo AppliedLayout { get; set; } = new();
}

public sealed class DeviceInfo
{
    [JsonPropertyName("monitor")]
    public string Monitor { get; set; } = string.Empty;

    [JsonPropertyName("monitor-instance")]
    public string MonitorInstance { get; set; } = string.Empty;

    [JsonPropertyName("monitor-number")]
    public int MonitorNumber { get; set; }

    [JsonPropertyName("serial-number")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("virtual-desktop")]
    public string VirtualDesktop { get; set; } = string.Empty;
}

public sealed class AppliedLayoutInfo
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("show-spacing")]
    public bool ShowSpacing { get; set; }

    [JsonPropertyName("spacing")]
    public int Spacing { get; set; }

    [JsonPropertyName("zone-count")]
    public int ZoneCount { get; set; }

    [JsonPropertyName("sensitivity-radius")]
    public int SensitivityRadius { get; set; }
}

// Layout Hotkeys
public sealed class LayoutHotkeys
{
    [JsonPropertyName("layout-hotkeys")]
    public List<LayoutHotkey> Hotkeys { get; set; }
}

public sealed class LayoutHotkey
{
    [JsonPropertyName("key")]
    public int Key { get; set; }

    [JsonPropertyName("layout-id")]
    public string LayoutId { get; set; } = string.Empty;
}
