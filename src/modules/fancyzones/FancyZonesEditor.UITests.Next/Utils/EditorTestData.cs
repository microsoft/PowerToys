// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;

namespace FancyZonesEditor.UITests.Utils;

public static class EditorTestData
{
    public const string Monitor1VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}";

    private const string Monitor2VirtualDesktop = Monitor1VirtualDesktop;
    private const string Monitor1Name = "monitor-1";
    private const string Monitor2Name = "monitor-2";
    private const string Monitor3Name = "monitor-3";
    private const string Monitor1InstanceId = "instance-id-1";
    private const string Monitor2InstanceId = "instance-id-2";
    private const string Monitor3InstanceId = "instance-id-3";
    private const string Monitor1Serial = "serial-number-1";
    private const string Monitor2Serial = "serial-number-2";
    private const string Monitor3Serial = "serial-number-3";
    private const string MonitorHorizontalConfiguration = "horizontal";
    private const string CustomLayout1Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";
    private const string MissingLayoutUuid = "{00000000-0000-0000-0000-000000000000}";

    public static void WriteMinimal(FancyZonesEditorFiles files)
    {
        Write(files, monitorCount: 1, includeDefaultCustomLayout: false);
    }

    public static void WriteForRunFancyZonesEditorTests(FancyZonesEditorFiles files)
    {
        Write(files, monitorCount: 2, includeDefaultCustomLayout: true);
    }

    public static void WriteForFirstLaunchTests(FancyZonesEditorFiles files)
    {
        Write(files, monitorCount: 1, includeDefaultCustomLayout: false);
    }

    public static void WriteForCreateLayoutTests(FancyZonesEditorFiles files)
    {
        Write(files, monitorCount: 1, includeDefaultCustomLayout: false);
    }

    public static void WriteForCopyLayoutTests(FancyZonesEditorFiles files)
    {
        var copiedSourceLayout = new CustomLayouts.CustomLayoutWrapper
        {
            Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
            Type = CustomLayout.Grid.TypeToString(),
            Name = "Grid custom layout",
            Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
            {
                Rows = 2,
                Columns = 3,
                RowsPercentage = [2967, 7033],
                ColumnsPercentage = [2410, 6040, 1550],
                CellChildMap = [[0, 1, 1], [0, 2, 3]],
                SensitivityRadius = 30,
                Spacing = 26,
                ShowSpacing = false,
            }),
        };

        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors = [CreateMonitor("monitor-1", "instance-id-1", "serial-number-1", 1, Monitor1VirtualDesktop, 192, 0, true)],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = [copiedSourceLayout],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates = CreateStandardTemplateLayouts(),
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts =
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "vertical",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "custom",
                        Uuid = copiedSourceLayout.Uuid,
                    },
                },
            ],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys =
            [
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    LayoutId = copiedSourceLayout.Uuid,
                    Key = 0,
                },
            ],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
    }

    public static void WriteForDeleteLayoutTests(FancyZonesEditorFiles files)
    {
        const string firstUuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";
        const string secondUuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}";

        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors = [CreateMonitor("monitor-1", "instance-id-1", "serial-number-1", 1, Monitor1VirtualDesktop, 192, 0, true)],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts =
            [
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = firstUuid,
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Custom layout 1",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = [2967, 7033],
                        ColumnsPercentage = [2410, 6040, 1550],
                        CellChildMap = [[0, 1, 1], [0, 2, 3]],
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = secondUuid,
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Custom layout 2",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                    {
                        RefHeight = 952,
                        RefWidth = 1500,
                        SensitivityRadius = 10,
                        Zones =
                        [
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 900,
                                Height = 522,
                            },
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 900,
                                Y = 0,
                                Width = 600,
                                Height = 750,
                            },
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 522,
                                Width = 1500,
                                Height = 430,
                            },
                        ],
                    }),
                },
            ],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates = CreateStandardTemplateLayouts(),
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts =
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "horizontal",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "custom",
                        Uuid = secondUuid,
                    },
                },
            ],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys =
            [
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    LayoutId = secondUuid,
                    Key = 0,
                },
            ],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
    }

    public static void WriteForApplyLayoutTests(FancyZonesEditorFiles files)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors =
            [
                new EditorParameters.NativeMonitorDataWrapper
                {
                    Monitor = "monitor-1",
                    MonitorInstanceId = "instance-id-1",
                    MonitorSerialNumber = "serial-number-1",
                    MonitorNumber = 1,
                    VirtualDesktop = Monitor1VirtualDesktop,
                    Dpi = 96,
                    LeftCoordinate = 0,
                    TopCoordinate = 0,
                    WorkAreaHeight = 1040,
                    WorkAreaWidth = 1920,
                    MonitorHeight = 1080,
                    MonitorWidth = 1920,
                    IsSelected = true,
                },
                new EditorParameters.NativeMonitorDataWrapper
                {
                    Monitor = "monitor-2",
                    MonitorInstanceId = "instance-id-2",
                    MonitorSerialNumber = "serial-number-2",
                    MonitorNumber = 2,
                    VirtualDesktop = Monitor1VirtualDesktop,
                    Dpi = 96,
                    LeftCoordinate = 1920,
                    TopCoordinate = 0,
                    WorkAreaHeight = 1040,
                    WorkAreaWidth = 1920,
                    MonitorHeight = 1080,
                    MonitorWidth = 1920,
                    IsSelected = false,
                },
            ],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts =
            [
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                    {
                        RefHeight = 952,
                        RefWidth = 1500,
                        SensitivityRadius = 10,
                        Zones = [],
                    }),
                },
            ],
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts =
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "horizontal",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus],
                        ZoneCount = 4,
                        ShowSpacing = true,
                        Spacing = 5,
                        SensitivityRadius = 20,
                    },
                },
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "vertical",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = Constants.CustomLayoutJsonTag,
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        ZoneCount = 0,
                        ShowSpacing = false,
                        Spacing = 0,
                        SensitivityRadius = 0,
                    },
                },
            ],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates =
            [
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty] },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus], ZoneCount = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows], ZoneCount = 2, ShowSpacing = true, Spacing = 10, SensitivityRadius = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns], ZoneCount = 2, ShowSpacing = true, Spacing = 20, SensitivityRadius = 20 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid], ZoneCount = 4, ShowSpacing = false, Spacing = 10, SensitivityRadius = 30 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid], ZoneCount = 3, ShowSpacing = true, Spacing = 1, SensitivityRadius = 40 },
            ],
        }));
    }

    public static void WriteForDefaultLayoutsTests(FancyZonesEditorFiles files)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors =
            [
                new EditorParameters.NativeMonitorDataWrapper
                {
                    Monitor = "monitor-1",
                    MonitorInstanceId = "instance-id-1",
                    MonitorSerialNumber = "serial-number-1",
                    MonitorNumber = 1,
                    VirtualDesktop = Monitor1VirtualDesktop,
                    Dpi = 192,
                    LeftCoordinate = 0,
                    TopCoordinate = 0,
                    WorkAreaHeight = 1040,
                    WorkAreaWidth = 1920,
                    MonitorHeight = 1080,
                    MonitorWidth = 1920,
                    IsSelected = true,
                },
                new EditorParameters.NativeMonitorDataWrapper
                {
                    Monitor = "monitor-2",
                    MonitorInstanceId = "instance-id-2",
                    MonitorSerialNumber = "serial-number-2",
                    MonitorNumber = 2,
                    VirtualDesktop = Monitor1VirtualDesktop,
                    Dpi = 96,
                    LeftCoordinate = 1920,
                    TopCoordinate = 0,
                    WorkAreaHeight = 1040,
                    WorkAreaWidth = 1920,
                    MonitorHeight = 1080,
                    MonitorWidth = 1920,
                    IsSelected = false,
                },
            ],
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts =
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "horizontal",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid],
                        ZoneCount = 4,
                        ShowSpacing = true,
                        Spacing = 5,
                        SensitivityRadius = 20,
                    },
                },
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "vertical",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = Constants.CustomLayoutJsonTag,
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        ZoneCount = 0,
                        ShowSpacing = false,
                        Spacing = 0,
                        SensitivityRadius = 0,
                    },
                },
            ],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts =
            [
                CreateDefaultLayoutsTestCustomLayout("{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}", "Layout 0"),
                CreateDefaultLayoutsTestCustomLayout("{E7807D0D-6223-4883-B15B-1F3883944C09}", "Layout 1"),
                CreateDefaultLayoutsTestCustomLayout("{F1A94F38-82B6-4876-A653-70D0E882DE2A}", "Layout 2"),
                CreateDefaultLayoutsTestCustomLayout("{F5FDBC04-0760-4776-9F05-96AAC4AE613F}", "Layout 3"),
            ],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates =
            [
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty] },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus], ZoneCount = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows], ZoneCount = 2, ShowSpacing = true, Spacing = 10, SensitivityRadius = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns], ZoneCount = 2, ShowSpacing = true, Spacing = 20, SensitivityRadius = 20 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid], ZoneCount = 4, ShowSpacing = false, Spacing = 10, SensitivityRadius = 30 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid], ZoneCount = 3, ShowSpacing = true, Spacing = 1, SensitivityRadius = 40 },
            ],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
    }

    public static void WriteForTemplateLayoutsTests(FancyZonesEditorFiles files)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors = [CreateMonitor("monitor-1", "instance-id-1", "serial-number-1", 1, Monitor1VirtualDesktop, 192, 0, true)],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates =
            [
                new LayoutTemplates.TemplateLayoutWrapper { Type = "blank" },
                new LayoutTemplates.TemplateLayoutWrapper { Type = "focus", ZoneCount = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = "rows", ZoneCount = 2, ShowSpacing = true, Spacing = 10, SensitivityRadius = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = "columns", ZoneCount = 2, ShowSpacing = true, Spacing = 20, SensitivityRadius = 20 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = "grid", ZoneCount = 4, ShowSpacing = false, Spacing = 10, SensitivityRadius = 30 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = "priority-grid", ZoneCount = 3, ShowSpacing = true, Spacing = 1, SensitivityRadius = 40 },
            ],
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts =
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "vertical",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "rows",
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                },
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = "horizontal",
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = "priority-grid",
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            ],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts =
            [
                new AppliedLayouts.AppliedLayoutWrapper
                {
                    Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstance = "instance-id-1",
                        MonitorNumber = 1,
                        SerialNumber = "serial-number-1",
                        VirtualDesktop = Monitor1VirtualDesktop,
                    },
                    AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                    {
                        Uuid = "{72409DFC-2B87-469B-AAC4-557273791C26}",
                        Type = "priority-grid",
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            ],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = [],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));
    }

    public static void WriteForCustomLayoutsTests(FancyZonesEditorFiles files)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors = [CreateMonitor("monitor-1", "instance-id-1", "serial-number-1", 1, Monitor1VirtualDesktop, 192, 0, true)],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts =
            [
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = "grid",
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = [2967, 7033],
                        ColumnsPercentage = [2410, 6040, 1550],
                        CellChildMap = [[0, 1, 1], [0, 2, 3]],
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = "canvas",
                    Name = "Canvas custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                    {
                        RefHeight = 952,
                        RefWidth = 1500,
                        SensitivityRadius = 10,
                        Zones =
                        [
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 900,
                                Height = 522,
                            },
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 900,
                                Y = 0,
                                Width = 600,
                                Height = 750,
                            },
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 522,
                                Width = 1500,
                                Height = 430,
                            },
                        ],
                    }),
                },
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{F1A94F38-82B6-4876-A653-70D0E882DE2A}",
                    Type = "grid",
                    Name = "Grid custom layout spacing enabled",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = [2967, 7033],
                        ColumnsPercentage = [2410, 6040, 1550],
                        CellChildMap = [[0, 1, 1], [0, 2, 3]],
                        SensitivityRadius = 30,
                        Spacing = 10,
                        ShowSpacing = true,
                    }),
                },
            ],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates = CreateStandardTemplateLayouts(),
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = [],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
    }

    public static void WriteForEditLayoutTests(FancyZonesEditorFiles files)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors =
            [
                new EditorParameters.NativeMonitorDataWrapper
                {
                    Monitor = "monitor-1",
                    MonitorInstanceId = "instance-id-1",
                    MonitorSerialNumber = "serial-number-1",
                    MonitorNumber = 1,
                    VirtualDesktop = Monitor1VirtualDesktop,
                    Dpi = 192,
                    LeftCoordinate = 0,
                    TopCoordinate = 0,
                    WorkAreaHeight = 1040,
                    WorkAreaWidth = 1920,
                    MonitorHeight = 1080,
                    MonitorWidth = 1920,
                    IsSelected = true,
                },
            ],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates = CreateStandardTemplateLayouts(),
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts =
            [
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 2,
                        RowsPercentage = [5000, 5000],
                        ColumnsPercentage = [5000, 5000],
                        CellChildMap = [[0, 1], [2, 3]],
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{0EB9BF3E-010E-46D7-8681-1879D1E111E1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid-9",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 3,
                        Columns = 3,
                        RowsPercentage = [2333, 3333, 4334],
                        ColumnsPercentage = [2333, 3333, 4334],
                        CellChildMap = [[0, 1, 2], [3, 4, 5], [6, 7, 8]],
                        SensitivityRadius = 20,
                        Spacing = 3,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Canvas custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                    {
                        RefHeight = 1040,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones =
                        [
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 500,
                                Height = 250,
                            },
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 500,
                                Y = 0,
                                Width = 1420,
                                Height = 500,
                            },
                            new CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 250,
                                Width = 1920,
                                Height = 500,
                            },
                        ],
                    }),
                },
            ],
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = [],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
    }


    public static void WriteForLayoutHotkeysTests(FancyZonesEditorFiles files)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors = [CreateMonitor("monitor-1", "instance-id-1", "serial-number-1", 1, Monitor1VirtualDesktop, 192, 0, true)],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts =
            [
                CreateLayoutHotkeysTestCustomLayout("{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}", "Layout 0"),
                CreateLayoutHotkeysTestCustomLayout("{E7807D0D-6223-4883-B15B-1F3883944C09}", "Layout 1"),
                CreateLayoutHotkeysTestCustomLayout("{F1A94F38-82B6-4876-A653-70D0E882DE2A}", "Layout 2"),
                CreateLayoutHotkeysTestCustomLayout("{F5FDBC04-0760-4776-9F05-96AAC4AE613F}", "Layout 3"),
            ],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys =
            [
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    LayoutId = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Key = 0,
                },
                new LayoutHotkeys.LayoutHotkeyWrapper
                {
                    LayoutId = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Key = 1,
                },
            ],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates = CreateStandardTemplateLayouts(),
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = [],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
    }

    public static void WriteForUIInitializeEditorParamsVerifySelectedMonitor(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, false),
                CreateMonitor(Monitor2Name, Monitor2InstanceId, Monitor2Serial, 2, Monitor2VirtualDesktop, 96, 1920, true),
            ]);
    }

    public static void WriteForUIInitializeEditorParamsVerifyMonitorScaling(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 192, 0, true),
            ]);
    }

    public static void WriteForUIInitializeEditorParamsVerifyMonitorResolution(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, true),
            ]);
    }

    public static void WriteForUIInitializeEditorParamsSpanAcrossMonitors(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 192, 0, true),
            ],
            spanZonesAcrossMonitors: true);
    }

    public static void WriteForUIInitializeAppliedLayoutsLayoutsApplied(FancyZonesEditorFiles files)
    {
        var customLayout = CreateCanvasCustomLayout(CustomLayout1Uuid, "Custom layout 1");

        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, true),
                CreateMonitor(Monitor2Name, Monitor2InstanceId, Monitor2Serial, 2, Monitor2VirtualDesktop, 96, 1920, false),
            ],
            customLayouts: [customLayout],
            appliedLayouts:
            [
                CreateAppliedLayout(
                    Monitor1Name,
                    Monitor1InstanceId,
                    Monitor1Serial,
                    1,
                    Monitor1VirtualDesktop,
                    MissingLayoutUuid,
                    Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns],
                    showSpacing: true,
                    spacing: 10,
                    zoneCount: 1,
                    sensitivityRadius: 20),
                CreateAppliedLayout(
                    Monitor2Name,
                    Monitor2InstanceId,
                    Monitor2Serial,
                    2,
                    Monitor2VirtualDesktop,
                    customLayout.Uuid,
                    Constants.CustomLayoutJsonTag),
            ]);
    }

    public static void WriteForUIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFound(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, true),
            ],
            customLayouts:
            [
                CreateCanvasCustomLayout(CustomLayout1Uuid, "Custom layout 1"),
            ],
            appliedLayouts:
            [
                CreateAppliedLayout(
                    Monitor1Name,
                    Monitor1InstanceId,
                    Monitor1Serial,
                    1,
                    Monitor1VirtualDesktop,
                    MissingLayoutUuid,
                    Constants.CustomLayoutJsonTag),
            ]);
    }

    public static void WriteForUIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayout(FancyZonesEditorFiles files)
    {
        var customLayout = CreateCanvasCustomLayout(CustomLayout1Uuid, "Custom layout 1");

        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, true),
            ],
            customLayouts: [customLayout],
            defaultLayouts:
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = MonitorHorizontalConfiguration,
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = Constants.CustomLayoutJsonTag,
                        Uuid = customLayout.Uuid,
                    },
                },
            ]);
    }

    public static void WriteForUIInitializeAppliedLayoutsNoLayoutsAppliedTemplateDefaultLayout(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, true),
            ],
            defaultLayouts:
            [
                new DefaultLayouts.DefaultLayoutWrapper
                {
                    MonitorConfiguration = MonitorHorizontalConfiguration,
                    Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                    {
                        Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid],
                        ZoneCount = 6,
                        ShowSpacing = true,
                        Spacing = 5,
                        SensitivityRadius = 20,
                    },
                },
            ]);
    }

    public static void WriteForUIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChanged(FancyZonesEditorFiles files)
    {
        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, Monitor1VirtualDesktop, 96, 0, true),
            ],
            appliedLayouts:
            [
                CreateAppliedLayout(
                    Monitor2Name,
                    Monitor2InstanceId,
                    Monitor2Serial,
                    2,
                    Monitor2VirtualDesktop,
                    MissingLayoutUuid,
                    Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus],
                    showSpacing: true,
                    spacing: 10,
                    zoneCount: 4,
                    sensitivityRadius: 30),
                CreateAppliedLayout(
                    Monitor3Name,
                    Monitor3InstanceId,
                    Monitor3Serial,
                    1,
                    Monitor1VirtualDesktop,
                    MissingLayoutUuid,
                    Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns],
                    showSpacing: true,
                    spacing: 10,
                    zoneCount: 1,
                    sensitivityRadius: 20),
            ]);
    }

    public static void WriteForUIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChanged(FancyZonesEditorFiles files)
    {
        const string virtualDesktop1 = "{11111111-1111-1111-1111-111111111111}";
        const string virtualDesktop2 = "{22222222-2222-2222-2222-222222222222}";

        WriteUIInitializeFixture(
            files,
            monitors:
            [
                CreateMonitor(Monitor1Name, Monitor1InstanceId, Monitor1Serial, 1, virtualDesktop1, 96, 0, true),
            ],
            appliedLayouts:
            [
                CreateAppliedLayout(
                    Monitor1Name,
                    Monitor1InstanceId,
                    Monitor1Serial,
                    1,
                    virtualDesktop2,
                    MissingLayoutUuid,
                    Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus],
                    showSpacing: true,
                    spacing: 10,
                    zoneCount: 4,
                    sensitivityRadius: 30),
            ]);
    }

    private static void WriteUIInitializeFixture(
        FancyZonesEditorFiles files,
        IReadOnlyList<EditorParameters.NativeMonitorDataWrapper> monitors,
        bool spanZonesAcrossMonitors = false,
        IReadOnlyList<AppliedLayouts.AppliedLayoutWrapper>? appliedLayouts = null,
        IReadOnlyList<CustomLayouts.CustomLayoutWrapper>? customLayouts = null,
        IReadOnlyList<DefaultLayouts.DefaultLayoutWrapper>? defaultLayouts = null)
    {
        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = spanZonesAcrossMonitors,
            Monitors = [.. monitors],
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [.. (appliedLayouts ?? [])],
        }));

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = [.. (customLayouts ?? [])],
        }));

        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = [.. (defaultLayouts ?? [])],
        }));

        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));

        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates = CreateStandardTemplateLayouts(),
        }));
    }

    private static CustomLayouts.CustomLayoutWrapper CreateCanvasCustomLayout(string uuid, string name)
    {
        return new CustomLayouts.CustomLayoutWrapper
        {
            Uuid = uuid,
            Type = CustomLayout.Canvas.TypeToString(),
            Name = name,
            Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
            {
                RefHeight = 1080,
                RefWidth = 1920,
                SensitivityRadius = 10,
                Zones = [],
            }),
        };
    }

    private static AppliedLayouts.AppliedLayoutWrapper CreateAppliedLayout(
        string monitor,
        string monitorInstance,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop,
        string uuid,
        string type,
        bool showSpacing = false,
        int spacing = 0,
        int zoneCount = 0,
        int sensitivityRadius = 0)
    {
        return new AppliedLayouts.AppliedLayoutWrapper
        {
            Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
            {
                Monitor = monitor,
                MonitorInstance = monitorInstance,
                SerialNumber = serialNumber,
                MonitorNumber = monitorNumber,
                VirtualDesktop = virtualDesktop,
            },
            AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
            {
                Uuid = uuid,
                Type = type,
                ShowSpacing = showSpacing,
                Spacing = spacing,
                ZoneCount = zoneCount,
                SensitivityRadius = sensitivityRadius,
            },
        };
    }

    private static void Write(FancyZonesEditorFiles files, int monitorCount, bool includeDefaultCustomLayout)
    {
        var monitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("monitor-1", "instance-id-1", "serial-number-1", 1, Monitor1VirtualDesktop, 96, 0, true),
        };

        if (monitorCount > 1)
        {
            monitors.Add(CreateMonitor("monitor-2", "instance-id-2", "serial-number-2", 2, Monitor1VirtualDesktop, 96, 1920, false));
        }

        files.Parameters.Write(new EditorParameters().Serialize(new EditorParameters.ParamsWrapper
        {
            ProcessId = 1,
            SpanZonesAcrossMonitors = false,
            Monitors = monitors,
        }));

        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
        var customLayouts = new List<CustomLayouts.CustomLayoutWrapper>();
        if (includeDefaultCustomLayout)
        {
            customLayouts.Add(new CustomLayouts.CustomLayoutWrapper
            {
                Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                Type = CustomLayout.Canvas.TypeToString(),
                Name = "Custom layout",
                Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                {
                    RefHeight = 952,
                    RefWidth = 1500,
                    SensitivityRadius = 10,
                    Zones = [],
                }),
            });
        }

        files.CustomLayouts.Write(new CustomLayouts().Serialize(new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = customLayouts,
        }));
        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(new DefaultLayouts.DefaultLayoutsListWrapper
        {
            DefaultLayouts = [],
        }));
        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(new LayoutHotkeys.LayoutHotkeysWrapper
        {
            LayoutHotkeys = [],
        }));
        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(new LayoutTemplates.TemplateLayoutsListWrapper
        {
            LayoutTemplates =
            [
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty] },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus], ZoneCount = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows], ZoneCount = 2, ShowSpacing = true, Spacing = 10, SensitivityRadius = 10 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns], ZoneCount = 2, ShowSpacing = true, Spacing = 20, SensitivityRadius = 20 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid], ZoneCount = 4, ShowSpacing = false, Spacing = 10, SensitivityRadius = 30 },
                new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid], ZoneCount = 3, ShowSpacing = true, Spacing = 1, SensitivityRadius = 40 },
            ],
        }));
    }

    private static CustomLayouts.CustomLayoutWrapper CreateDefaultLayoutsTestCustomLayout(string uuid, string name)
    {
        return new CustomLayouts.CustomLayoutWrapper
        {
            Uuid = uuid,
            Type = CustomLayout.Canvas.TypeToString(),
            Name = name,
            Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
            {
                RefHeight = 1080,
                RefWidth = 1920,
                SensitivityRadius = 10,
                Zones = [],
            }),
        };
    }

    private static CustomLayouts.CustomLayoutWrapper CreateLayoutHotkeysTestCustomLayout(string uuid, string name)
    {
        return new CustomLayouts.CustomLayoutWrapper
        {
            Uuid = uuid,
            Type = CustomLayout.Canvas.TypeToString(),
            Name = name,
            Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
            {
                RefHeight = 1080,
                RefWidth = 1920,
                SensitivityRadius = 10,
                Zones = [],
            }),
        };
    }

    private static List<LayoutTemplates.TemplateLayoutWrapper> CreateStandardTemplateLayouts()
    {
        return
        [
            new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Empty] },
            new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus], ZoneCount = 10 },
            new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows], ZoneCount = 2, ShowSpacing = true, Spacing = 10, SensitivityRadius = 10 },
            new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Columns], ZoneCount = 2, ShowSpacing = true, Spacing = 20, SensitivityRadius = 20 },
            new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Grid], ZoneCount = 4, ShowSpacing = false, Spacing = 10, SensitivityRadius = 30 },
            new LayoutTemplates.TemplateLayoutWrapper { Type = Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.PriorityGrid], ZoneCount = 3, ShowSpacing = true, Spacing = 1, SensitivityRadius = 40 },
        ];
    }

    private static EditorParameters.NativeMonitorDataWrapper CreateMonitor(
        string monitor,
        string monitorInstanceId,
        string monitorSerialNumber,
        int monitorNumber,
        string virtualDesktop,
        int dpi,
        int leftCoordinate,
        bool isSelected)
    {
        return new EditorParameters.NativeMonitorDataWrapper
        {
            Monitor = monitor,
            MonitorInstanceId = monitorInstanceId,
            MonitorSerialNumber = monitorSerialNumber,
            MonitorNumber = monitorNumber,
            VirtualDesktop = virtualDesktop,
            Dpi = dpi,
            LeftCoordinate = leftCoordinate,
            TopCoordinate = 0,
            WorkAreaHeight = 1040,
            WorkAreaWidth = 1920,
            MonitorHeight = 1080,
            MonitorWidth = 1920,
            IsSelected = isSelected,
        };
    }
}