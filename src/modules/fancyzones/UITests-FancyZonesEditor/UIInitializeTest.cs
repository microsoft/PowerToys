// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class UIInitializeTest : UITestBase
    {
        public UIInitializeTest()
            : base(PowerToysModule.FancyZone)
        {
        }

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void EditorParams_VerifySelectedMonitor()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));
            this.RestartScopeExe();

            Session.Find<Element>("Monitor 1").Click();
            Session.Find<Element>("Monitor 2").Click();
            Assert.IsFalse(Session.Find<Element>("Monitor 1").Selected);
            Assert.IsTrue(Session.Find<Element>("Monitor 2").Selected);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorScaling()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));
            this.RestartScopeExe();

            Session.Find<Element>("Monitor 1").Click();
            var monitor = Session.Find<Element>("Monitor 1");
            var scaling = monitor.Find<Element>(By.AccessibilityId("ScalingText"));
            Assert.AreEqual("200%", scaling.Text);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorResolution()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));
            this.RestartScopeExe();

            Session.Find<Element>("Monitor 1").Click();
            var monitor = Session.Find<Element>("Monitor 1");
            var resolution = monitor.Find<Element>(By.AccessibilityId("ResolutionText"));
            Assert.AreEqual("1920 × 1080", resolution.Text);
        }

        [TestMethod]
        public void EditorParams_SpanAcrossMonitors()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));
            this.RestartScopeExe();

            Session.Find<Element>("Monitor 1").Click();
            var monitor = Session.Find<Element>("Monitor 1");
            Assert.IsNotNull(monitor);
            Assert.IsTrue(monitor.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_LayoutsApplied()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        Type = CustomLayout.Canvas.TypeToString(),
                        Name = "Custom layout 1",
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
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

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
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));
            this.RestartScopeExe();

            // check layout on monitor 1
            var layoutOnMonitor1 = Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Columns]);
            Assert.IsNotNull(layoutOnMonitor1);
            Assert.IsTrue(layoutOnMonitor1.Selected);

            // check layout on monitor 2
            Session.Find<Element>(By.AccessibilityId("Monitors")).Find<Element>("Monitor 2").Click();
            var layoutOnMonitor2 = Session.Find<Element>(customLayoutListWrapper.CustomLayouts[0].Name);
            Assert.IsNotNull(layoutOnMonitor2);
            Assert.IsTrue(layoutOnMonitor2.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_CustomLayoutsApplied_LayoutIdNotFound()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        Type = CustomLayout.Canvas.TypeToString(),
                        Name = "Custom layout 1",
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
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

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
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            this.RestartScopeExe();

            var emptyLayout = Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Blank]);
            Assert.IsNotNull(emptyLayout);
            Assert.IsTrue(emptyLayout.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_NoLayoutsApplied_CustomDefaultLayout()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
                {
                    new CustomLayouts.CustomLayoutWrapper
                    {
                        Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                        Type = CustomLayout.Canvas.TypeToString(),
                        Name = "Custom layout 1",
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
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.RestoreData();
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
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            this.RestartScopeExe();
            Session.Find<Element>(customLayoutListWrapper.CustomLayouts[0].Name).Click();
            var defaultLayout = Session.Find<Element>(customLayoutListWrapper.CustomLayouts[0].Name);
            Assert.IsNotNull(defaultLayout);
            Assert.IsTrue(defaultLayout.Selected);
        }

        [TestMethod]
        public void AppliedLayouts_NoLayoutsApplied_TemplateDefaultLayout()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.RestoreData();
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
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            this.RestartScopeExe();

            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Grid]).Click();
            var defaultLayout = Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Grid]);
            Assert.IsNotNull(defaultLayout);
            Assert.IsTrue(defaultLayout.Selected);

            // check the number of zones and spacing
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Grid]).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.ZoneCount, int.Parse(Session.Find<Element>(By.AccessibilityId(AccessibilityId.TemplateZoneSlider))?.Text!, CultureInfo.InvariantCulture));
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.Spacing, int.Parse(Session.Find<Element>(By.AccessibilityId(AccessibilityId.SpacingSlider))?.Text!, CultureInfo.InvariantCulture));
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.ShowSpacing, Session.Find<Element>(By.AccessibilityId(AccessibilityId.SpacingSlider))?.Enabled);
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.ShowSpacing, Session.Find<Element>(By.AccessibilityId(AccessibilityId.SpacingToggle))?.Selected);
            Assert.AreEqual(defaultLayoutsListWrapper.DefaultLayouts[0].Layout.SensitivityRadius, int.Parse(Session.Find<Element>(By.AccessibilityId(AccessibilityId.SensitivitySlider))?.Text!, CultureInfo.InvariantCulture));
            Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(AccessibilityId.HorizontalDefaultButtonChecked)));
        }

        [TestMethod]
        public void AppliedLayouts_VerifyDisconnectedMonitorsLayoutsAreNotChanged()
        {
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

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
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            this.RestartScopeExe();
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Rows]).Click();

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
            InitFileData();
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

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
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            this.RestartScopeExe();
            Session.Find<Element>(TestConstants.TemplateLayoutNames[LayoutType.Rows]).Click();

            // check the file
            var data = appliedLayouts.Read(appliedLayouts.File);
            Assert.AreEqual(parameters.Monitors.Count + appliedLayoutsWrapper.AppliedLayouts.Count, data.AppliedLayouts.Count);
            Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop1));
            Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop2));
            Assert.AreEqual(appliedLayoutsWrapper.AppliedLayouts[0].AppliedLayout.Type, data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop2).AppliedLayout.Type);
            Assert.AreEqual(LayoutType.Rows.TypeToString(), data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == virtualDesktop1).AppliedLayout.Type);
        }

        private void InitFileData()
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayouts.DefaultLayoutsListWrapper defaultLayoutsListWrapper = new DefaultLayouts.DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayouts.DefaultLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.DefaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsListWrapper));

            LayoutHotkeys layoutHotkeys = new LayoutHotkeys();
            LayoutHotkeys.LayoutHotkeysWrapper layoutHotkeysWrapper = new LayoutHotkeys.LayoutHotkeysWrapper
            {
                LayoutHotkeys = new List<LayoutHotkeys.LayoutHotkeyWrapper> { },
            };
            FancyZonesEditorHelper.Files.LayoutHotkeysIOHelper.WriteData(layoutHotkeys.Serialize(layoutHotkeysWrapper));

            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayouts.AppliedLayoutsListWrapper appliedLayoutsWrapper = new AppliedLayouts.AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.AppliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsWrapper));

            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = new CustomLayouts.CustomLayoutListWrapper
            {
                CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper> { },
            };
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

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
            FancyZonesEditorHelper.Files.LayoutTemplatesIOHelper.WriteData(layoutTemplates.Serialize(templateLayoutsListWrapper));
        }
    }
}
