// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;

namespace FancyZones.UITests.Utils;

/// <summary>
/// The layout/monitor JSON fixtures the tests seed before launching PowerToys, ported from the legacy
/// FancyZones UI tests.
/// </summary>
/// <remarks>
/// The legacy suite obtained the layout-type json tags through <c>FancyZonesEditor</c>'s
/// <c>LayoutType.TypeToString()</c> / <c>MonitorConfigurationType.TypeToString()</c> extensions, which
/// live in the WPF editor assembly. The port depends on <c>FancyZonesEditorCommon</c> only, so the
/// same tags come from <see cref="Constants.TemplateLayoutJsonTags"/> and the two literals below.
/// </remarks>
public static class LayoutFixtures
{
    public const string HorizontalMonitorConfiguration = "horizontal";
    public const string VerticalMonitorConfiguration = "vertical";

    public const string GridCustomLayoutUuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";
    public const string Grid9LayoutUuid = "{0EB9BF3E-010E-46D7-8681-1879D1E111E1}";
    public const string CanvasCustomLayoutUuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}";
    public const string CustomColumnUuid = "{63F09977-D327-4DAC-98F4-0C886CAE9517}";

    public static string TemplateTag(Constants.TemplateLayout layout) => Constants.TemplateLayoutJsonTags[layout];

    /// <summary>Two-monitor editor parameters used by the quick-layout-switch tests.</summary>
    public static EditorParameters.ParamsWrapper TwoMonitorParameters { get; } = new()
    {
        ProcessId = 1,
        SpanZonesAcrossMonitors = false,
        Monitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            new()
            {
                Monitor = "monitor-1",
                MonitorInstanceId = "instance-id-1",
                MonitorSerialNumber = "serial-number-1",
                MonitorNumber = 1,
                VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                Dpi = 96,
                LeftCoordinate = 0,
                TopCoordinate = 0,
                WorkAreaHeight = 1040,
                WorkAreaWidth = 1920,
                MonitorHeight = 1080,
                MonitorWidth = 1920,
                IsSelected = true,
            },
            new()
            {
                Monitor = "monitor-2",
                MonitorInstanceId = "instance-id-2",
                MonitorSerialNumber = "serial-number-2",
                MonitorNumber = 2,
                VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                Dpi = 96,
                LeftCoordinate = 1920,
                TopCoordinate = 0,
                WorkAreaHeight = 1040,
                WorkAreaWidth = 1920,
                MonitorHeight = 1080,
                MonitorWidth = 1920,
                IsSelected = false,
            },
        },
    };

    /// <summary>Grid / Grid-9 / Canvas custom layouts referenced by the quick-layout-switch hotkeys.</summary>
    public static CustomLayouts.CustomLayoutListWrapper QuickSwitchCustomLayouts { get; } = new()
    {
        CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
        {
            new()
            {
                Uuid = GridCustomLayoutUuid,
                Type = CustomLayout.Grid.TypeToString(),
                Name = FancyZonesTestHelper.LayoutName.GridCustomLayout,
                Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                {
                    Rows = 2,
                    Columns = 2,
                    RowsPercentage = new List<int> { 5000, 5000 },
                    ColumnsPercentage = new List<int> { 5000, 5000 },
                    CellChildMap = new int[][] { [0, 1], [2, 3] },
                    SensitivityRadius = 30,
                    Spacing = 26,
                    ShowSpacing = false,
                }),
            },
            new()
            {
                Uuid = Grid9LayoutUuid,
                Type = CustomLayout.Grid.TypeToString(),
                Name = FancyZonesTestHelper.LayoutName.Grid9,
                Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                {
                    Rows = 3,
                    Columns = 3,
                    RowsPercentage = new List<int> { 2333, 3333, 4334 },
                    ColumnsPercentage = new List<int> { 2333, 3333, 4334 },
                    CellChildMap = new int[][] { [0, 1, 2], [3, 4, 5], [6, 7, 8] },
                    SensitivityRadius = 20,
                    Spacing = 3,
                    ShowSpacing = false,
                }),
            },
            new()
            {
                Uuid = CanvasCustomLayoutUuid,
                Type = CustomLayout.Canvas.TypeToString(),
                Name = FancyZonesTestHelper.LayoutName.CanvasCustomLayout,
                Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                {
                    RefHeight = 1040,
                    RefWidth = 1920,
                    SensitivityRadius = 10,
                    Zones = new List<CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper>
                    {
                        new() { X = 0, Y = 0, Width = 500, Height = 250 },
                        new() { X = 500, Y = 0, Width = 1420, Height = 500 },
                        new() { X = 0, Y = 250, Width = 1920, Height = 500 },
                    },
                }),
            },
        },
    };

    /// <summary>Win+Ctrl+Alt+0/1/2 bound to the three quick-switch custom layouts.</summary>
    public static LayoutHotkeys.LayoutHotkeysWrapper QuickSwitchHotkeys { get; } = new()
    {
        LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper>
        {
            new() { Key = 0, LayoutId = GridCustomLayoutUuid },
            new() { Key = 1, LayoutId = Grid9LayoutUuid },
            new() { Key = 2, LayoutId = CanvasCustomLayoutUuid },
        },
    };

    /// <summary>The stock template layouts, so the editor renders a predictable card list.</summary>
    public static LayoutTemplates.TemplateLayoutsListWrapper TemplateLayouts { get; } = new()
    {
        LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
        {
            new() { Type = TemplateTag(Constants.TemplateLayout.Empty) },
            new() { Type = TemplateTag(Constants.TemplateLayout.Focus), ZoneCount = 10 },
            new() { Type = TemplateTag(Constants.TemplateLayout.Rows), ZoneCount = 2, ShowSpacing = true, Spacing = 10, SensitivityRadius = 10 },
            new() { Type = TemplateTag(Constants.TemplateLayout.Columns), ZoneCount = 2, ShowSpacing = true, Spacing = 20, SensitivityRadius = 20 },
            new() { Type = TemplateTag(Constants.TemplateLayout.Grid), ZoneCount = 4, ShowSpacing = false, Spacing = 10, SensitivityRadius = 30 },
            new() { Type = TemplateTag(Constants.TemplateLayout.PriorityGrid), ZoneCount = 3, ShowSpacing = true, Spacing = 1, SensitivityRadius = 40 },
        },
    };

    /// <summary>Horizontal/vertical defaults matching the legacy <c>LayoutApplyHotKeyTests</c> fixture.</summary>
    public static DefaultLayouts.DefaultLayoutsListWrapper DefaultLayouts { get; } = new()
    {
        DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>
        {
            new()
            {
                MonitorConfiguration = HorizontalMonitorConfiguration,
                Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                {
                    Type = TemplateTag(Constants.TemplateLayout.Focus),
                    ZoneCount = 4,
                    ShowSpacing = true,
                    Spacing = 5,
                    SensitivityRadius = 20,
                },
            },
            new()
            {
                MonitorConfiguration = VerticalMonitorConfiguration,
                Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                {
                    Type = Constants.CustomLayoutJsonTag,
                    Uuid = GridCustomLayoutUuid,
                    ZoneCount = 0,
                    ShowSpacing = false,
                    Spacing = 0,
                    SensitivityRadius = 0,
                },
            },
        },
    };

    /// <summary>
    /// A single-cell "Custom Column" grid: one zone covering the whole work area, so a dragged window
    /// always lands in it and the sampled pixel outside the window is the zone's colour.
    /// </summary>
    public static CustomLayouts.CustomLayoutListWrapper SingleZoneColumn { get; } = CustomColumn(1, new List<int> { 10000 }, [[0]]);

    /// <summary>The same layout split into two columns, used where the test needs two side-by-side zones.</summary>
    public static CustomLayouts.CustomLayoutListWrapper TwoZoneColumns { get; } = CustomColumn(2, new List<int> { 5000, 5000 }, [[0, 1]]);

    private static CustomLayouts.CustomLayoutListWrapper CustomColumn(int columns, List<int> columnsPercentage, int[][] cellChildMap) => new()
    {
        CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
        {
            new()
            {
                Uuid = CustomColumnUuid,
                Type = CustomLayout.Grid.TypeToString(),
                Name = FancyZonesTestHelper.LayoutName.CustomColumn,
                Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                {
                    Rows = 1,
                    Columns = columns,
                    RowsPercentage = new List<int> { 10000 },
                    ColumnsPercentage = columnsPercentage,
                    CellChildMap = cellChildMap,
                    SensitivityRadius = 20,
                    ShowSpacing = true,
                    Spacing = 10,
                }),
            },
        },
    };
}
