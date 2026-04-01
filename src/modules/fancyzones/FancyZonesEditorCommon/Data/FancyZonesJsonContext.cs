// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using FancyZonesEditorCommon.Utils;

namespace FancyZonesEditorCommon.Data
{
    /// <summary>
    /// JSON serialization context for AOT-compatible serialization of FancyZones data types.
    /// </summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
        WriteIndented = true)]
    [JsonSerializable(typeof(AppliedLayouts.AppliedLayoutsListWrapper))]
    [JsonSerializable(typeof(AppliedLayouts.AppliedLayoutWrapper))]
    [JsonSerializable(typeof(AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper))]
    [JsonSerializable(typeof(AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper), TypeInfoPropertyName = "AppliedLayoutLayoutWrapper")]
    [JsonSerializable(typeof(CustomLayouts.CustomLayoutListWrapper))]
    [JsonSerializable(typeof(CustomLayouts.CustomLayoutWrapper))]
    [JsonSerializable(typeof(CustomLayouts.CanvasInfoWrapper))]
    [JsonSerializable(typeof(CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper))]
    [JsonSerializable(typeof(CustomLayouts.GridInfoWrapper))]
    [JsonSerializable(typeof(LayoutTemplates.TemplateLayoutsListWrapper))]
    [JsonSerializable(typeof(LayoutTemplates.TemplateLayoutWrapper))]
    [JsonSerializable(typeof(LayoutHotkeys.LayoutHotkeysWrapper))]
    [JsonSerializable(typeof(LayoutHotkeys.LayoutHotkeyWrapper))]
    [JsonSerializable(typeof(EditorParameters.ParamsWrapper))]
    [JsonSerializable(typeof(EditorParameters.NativeMonitorDataWrapper))]
    [JsonSerializable(typeof(DefaultLayouts.DefaultLayoutsListWrapper))]
    [JsonSerializable(typeof(DefaultLayouts.DefaultLayoutWrapper))]
    [JsonSerializable(typeof(DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper), TypeInfoPropertyName = "DefaultLayoutLayoutWrapper")]
    public partial class FancyZonesJsonContext : JsonSerializerContext
    {
    }
}
