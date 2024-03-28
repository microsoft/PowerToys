// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorSession;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class UIInitializationTests
    {
        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _context = null;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            LayoutTemplates layoutTemplates = new LayoutTemplates();
            LayoutTemplates.TemplateLayoutsListWrapper templateLayoutsListWrapper = new LayoutTemplates.TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<LayoutTemplates.TemplateLayoutWrapper>
                {
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Blank.TypeToString(),
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Focus.TypeToString(),
                        ZoneCount = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Rows.TypeToString(),
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Columns.TypeToString(),
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.Grid.TypeToString(),
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new LayoutTemplates.TemplateLayoutWrapper
                    {
                        Type = LayoutType.PriorityGrid.TypeToString(),
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close();
            FancyZonesEditorSession.Files.Restore();
        }

        [TestMethod]
        public void EditorParams_VerifySelectedMonitor()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
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
                        IsSelected = false,
                    },
                    new NativeMonitorDataWrapper
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
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);

            Assert.IsFalse(_session.GetMonitorItem(1)?.Selected);
            Assert.IsTrue(_session.GetMonitorItem(2)?.Selected);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorScaling()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 192, // 200% scaling
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            var scaling = monitor.FindElementByAccessibilityId("ScalingText");
            Assert.AreEqual("200%", scaling.Text);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorResolution()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 192,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            var resolution = monitor.FindElementByAccessibilityId("ResolutionText");
            Assert.AreEqual("1920 × 1080", resolution.Text);
        }

        [TestMethod]
        public void EditorParams_SpanAcrossMonitors()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = true,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 192,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            Assert.IsNotNull(monitor);
            Assert.IsTrue(monitor.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_LayoutsApplied()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
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
                    new NativeMonitorDataWrapper
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
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        Type = CustomLayout.Canvas.TypeToString(),
                        Name = "Layout 0",
                        Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                        {
                            RefHeight = 1080,
                            RefWidth = 1920,
                            SensitivityRadius = 10,
                            Zones = new List<CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper> { },
                        }),
                    },
                },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
                {
                    new AppliedLayouts.AppliedLayoutWrapper
                    {
                        Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-1",
                            MonitorInstance = "instance-id-1",
                            SerialNumber = "serial-number-1",
                            MonitorNumber = 1,
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = "{00000000-0000-0000-0000-000000000000}",
                            Type = LayoutType.Columns.TypeToString(),
                            ShowSpacing = true,
                            Spacing = 10,
                            ZoneCount = 1,
                            SensitivityRadius = 20,
                        },
                    },
                    new AppliedLayouts.AppliedLayoutWrapper
                    {
                        Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-2",
                            MonitorInstance = "instance-id-2",
                            SerialNumber = "serial-number-2",
                            MonitorNumber = 2,
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = customLayoutListWrapper.CustomLayouts[0].Uuid,
                            Type = LayoutType.Custom.TypeToString(),
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            _session = new FancyZonesEditorSession(_context!);

            // check layout on monitor 1
            var layoutOnMonitor1 = _session?.GetLayout(TestConstants.TemplateLayoutNames[LayoutType.Columns]);
            Assert.IsNotNull(layoutOnMonitor1);
            Assert.IsTrue(layoutOnMonitor1.Selected);

            // check layout on monitor 2
            _session?.ClickMonitor(2);
            var layoutOnMonitor2 = _session?.GetLayout(customLayoutListWrapper.CustomLayouts[0].Name);
            Assert.IsNotNull(layoutOnMonitor2);
            Assert.IsTrue(layoutOnMonitor2.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_CustomLayoutsApplied_LayoutIdNotFound()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
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
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        Type = CustomLayout.Canvas.TypeToString(),
                        Name = "Layout 0",
                        Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                        {
                            RefHeight = 1080,
                            RefWidth = 1920,
                            SensitivityRadius = 10,
                            Zones = new List<CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper> { },
                        }),
                    },
                },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
                {
                    new AppliedLayouts.AppliedLayoutWrapper
                    {
                        Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-1",
                            MonitorInstance = "instance-id-1",
                            SerialNumber = "serial-number-1",
                            MonitorNumber = 1,
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = "{00000000-0000-0000-0000-000000000000}",
                            Type = LayoutType.Custom.TypeToString(),
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            _session = new FancyZonesEditorSession(_context!);

            var emptyLayout = _session?.GetLayout(TestConstants.TemplateLayoutNames[LayoutType.Blank]);
            Assert.IsNotNull(emptyLayout);
            Assert.IsTrue(emptyLayout.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_NoLayoutsApplied_CustomDefaultLayout()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
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
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        Type = CustomLayout.Canvas.TypeToString(),
                        Name = "Layout 0",
                        Info = new CustomLayouts().ToJsonElement(new CustomLayouts.CanvasInfoWrapper
                        {
                            RefHeight = 1080,
                            RefWidth = 1920,
                            SensitivityRadius = 10,
                            Zones = new List<CustomLayouts.CanvasInfoWrapper.CanvasZoneWrapper> { },
                        }),
                    },
                },
            };
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>
                {
                    new DefaultLayouts.DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Horizontal.TypeToString(),
                        Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = LayoutType.Custom.TypeToString(),
                            Uuid = customLayoutListWrapper.CustomLayouts[0].Uuid,
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            _session = new FancyZonesEditorSession(_context!);

            var defaultLayout = _session?.GetLayout(customLayoutListWrapper.CustomLayouts[0].Name);
            Assert.IsNotNull(defaultLayout);
            Assert.IsTrue(defaultLayout.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_NoLayoutsApplied_TemplateDefaultLayout()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
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
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper>
                {
                    new DefaultLayouts.DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Horizontal.TypeToString(),
                        Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = LayoutType.Grid.TypeToString(),
                            ZoneCount = 6,
                            ShowSpacing = true,
                            Spacing = 5,
                            SensitivityRadius = 20,
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            _session = new FancyZonesEditorSession(_context!);

            var defaultLayout = _session?.GetLayout(TestConstants.TemplateLayoutNames[LayoutType.Grid]);
            Assert.IsNotNull(defaultLayout);
            Assert.IsTrue(defaultLayout.Selected);

            // check the number of zones and spacing
            _session?.ClickEditLayout(TestConstants.TemplateLayoutNames[LayoutType.Grid]);
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.ZoneCount, int.Parse(_session?.FindByAccessibilityId(AccessibilityId.TemplateZoneSlider)?.Text!, CultureInfo.InvariantCulture));
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.Spacing, int.Parse(_session?.FindByAccessibilityId(AccessibilityId.SpacingSlider)?.Text!, CultureInfo.InvariantCulture));
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.ShowSpacing, _session?.FindByAccessibilityId(AccessibilityId.SpacingSlider)?.Enabled);
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.ShowSpacing, _session?.FindByAccessibilityId(AccessibilityId.SpacingToggle)?.Selected);
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.SensitivityRadius, int.Parse(_session?.FindByAccessibilityId(AccessibilityId.SensitivitySlider)?.Text!, CultureInfo.InvariantCulture));
            Assert.IsNotNull(_session?.GetHorizontalDefaultButton(true));
        }

        [TestMethod]
        public void AppliedLayouts_VerifyDisconnectedMonitorsLayoutsAreNotChanged()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
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
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
                {
                    new AppliedLayouts.AppliedLayoutWrapper
                    {
                        Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-2",
                            MonitorInstance = "instance-id-2",
                            SerialNumber = "serial-number-2",
                            MonitorNumber = 2,
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = "{00000000-0000-0000-0000-000000000000}",
                            Type = LayoutType.Focus.TypeToString(),
                            ShowSpacing = true,
                            Spacing = 10,
                            ZoneCount = 4,
                            SensitivityRadius = 30,
                        },
                    },
                    new AppliedLayouts.AppliedLayoutWrapper
                    {
                        Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-3",
                            MonitorInstance = "instance-id-3",
                            SerialNumber = "serial-number-3",
                            MonitorNumber = 1,
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = "{00000000-0000-0000-0000-000000000000}",
                            Type = LayoutType.Columns.TypeToString(),
                            ShowSpacing = true,
                            Spacing = 10,
                            ZoneCount = 1,
                            SensitivityRadius = 20,
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            _session = new FancyZonesEditorSession(_context!);
            _session?.Click(_session?.GetLayout(TestConstants.TemplateLayoutNames[LayoutType.Rows])!);

            // check the file
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(parameters.Monitors.Count + appliedLayoutsWrapper.AppliedLayouts.Count, data.AppliedLayouts.Count);

            foreach (var monitor in parameters.Monitors)
            {
                Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == monitor.Monitor));
            }

            foreach (var layout in appliedLayoutsWrapper.AppliedLayouts)
            {
                Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == layout.Device.Monitor));
            }
        }

        [TestMethod]
        public void AppliedLayouts_VerifyOtherVirtualDesktopsAreNotChanged()
        {
            string virtualDesktop1 = "{11111111-1111-1111-1111-111111111111}";
            string virtualDesktop2 = "{22222222-2222-2222-2222-222222222222}";

            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = virtualDesktop1,
                        Dpi = 96,
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
                {
                    new AppliedLayouts.AppliedLayoutWrapper
                    {
                        Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-1",
                            MonitorInstance = "instance-id-1",
                            SerialNumber = "serial-number-1",
                            MonitorNumber = 1,
                            VirtualDesktop = virtualDesktop2,
                        },
                        AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                        {
                            Uuid = "{00000000-0000-0000-0000-000000000000}",
                            Type = LayoutType.Focus.TypeToString(),
                            ShowSpacing = true,
                            Spacing = 10,
                            ZoneCount = 4,
                            SensitivityRadius = 30,
                        },
                    },
                },
            };
            FancyZonesEditorSession.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            _session = new FancyZonesEditorSession(_context!);
            _session?.Click(_session?.GetLayout(TestConstants.TemplateLayoutNames[LayoutType.Rows])!);

            // check the file
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(parameters.Monitors.Count + appliedLayoutsWrapper.AppliedLayouts.Count, data.AppliedLayouts.Count);
            Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop1));
            Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop2));
            Assert.AreEqual(appliedLayoutsWrapper.AppliedLayouts[0].AppliedLayout.Type, data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop2).AppliedLayout.Type);
            Assert.AreEqual(LayoutType.Rows.TypeToString(), data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop1).AppliedLayout.Type);
        }

        [TestMethod]
        public void FirstLaunch()
        {
            EditorParameters editorParameters = new EditorParameters();
            ParamsWrapper parameters = new ParamsWrapper
            {
                ProcessId = 1,
                SpanZonesAcrossMonitors = false,
                Monitors = new List<NativeMonitorDataWrapper>
                {
                    new NativeMonitorDataWrapper
                    {
                        Monitor = "monitor-1",
                        MonitorInstanceId = "instance-id-1",
                        MonitorSerialNumber = "serial-number-1",
                        MonitorNumber = 1,
                        VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        Dpi = 192, // 200% scaling
                        LeftCoordinate = 0,
                        TopCoordinate = 0,
                        WorkAreaHeight = 1040,
                        WorkAreaWidth = 1920,
                        MonitorHeight = 1080,
                        MonitorWidth = 1920,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorSession.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            // files not yet exist
            FancyZonesEditorSession.Files.LayoutTemplatesIOHelper.DeleteFile();
            FancyZonesEditorSession.Files.CustomLayoutsIOHelper.DeleteFile();
            FancyZonesEditorSession.Files.LayoutHotkeysIOHelper.DeleteFile();
            FancyZonesEditorSession.Files.DefaultLayoutsIOHelper.DeleteFile();

            // verify editor opens without errors
            _session = new FancyZonesEditorSession(_context!);
            Assert.IsNotNull(_session.FindByAccessibilityId(FancyZonesEditorSession.AccessibilityId.MainWindow));
        }
    }
}
