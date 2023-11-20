// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZonesEditorCommon.Data.AppliedLayouts;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutTemplates;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class UIInitializaionTests
    {
        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;
        private static IOTestHelper? _editorParamsIOHelper;
        private static IOTestHelper? _templatesIOHelper;
        private static IOTestHelper? _defaultLayoutsIOHelper;
        private static IOTestHelper? _appliedLayoutsIOHelper;

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

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close(_context!);

            _editorParamsIOHelper?.RestoreData();
            _templatesIOHelper?.RestoreData();
            _defaultLayoutsIOHelper?.RestoreData();
            _appliedLayoutsIOHelper?.RestoreData();
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
            _editorParamsIOHelper = new IOTestHelper(editorParameters.File);
            _editorParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

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
            _editorParamsIOHelper = new IOTestHelper(editorParameters.File);
            _editorParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            var scaling = monitor.FindElementByAccessibilityId("ScalingText");
            Assert.AreEqual("200%", scaling.Text);
        }

        [TestMethod]
        public void EditorParams_VerifyMonitorResolution()
        {
            EditorParameters editorParameters = new EditorParameters();
            _editorParamsIOHelper = new IOTestHelper(editorParameters.File);
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
            _editorParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            var resolution = monitor.FindElementByAccessibilityId("ResolutionText");
            Assert.AreEqual("1920 × 1080", resolution.Text);
        }

        [TestMethod]
        public void EditorParams_SpanAcrossMonitors()
        {
            EditorParameters editorParameters = new EditorParameters();
            _editorParamsIOHelper = new IOTestHelper(editorParameters.File);
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
            _editorParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            _session = new FancyZonesEditorSession(_context!);
            var monitor = _session.GetMonitorItem(1);
            Assert.IsNotNull(monitor);
            Assert.IsTrue(monitor.Selected);
        }

        [TestMethod]
        public void TemplateLayouts_ZoneNumber() // verify zone numbers are set correctly
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
            _editorParamsIOHelper = new IOTestHelper(editorParameters.File);
            _editorParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

            LayoutTemplates layoutTemplates = new LayoutTemplates();
            TemplateLayoutsListWrapper list = new TemplateLayoutsListWrapper
            {
                LayoutTemplates = new List<TemplateLayoutWrapper>
                {
                    new TemplateLayoutWrapper
                    {
                        Type = Constants.LayoutTypes[Constants.Layouts.Empty],
                    },
                    new TemplateLayoutWrapper
                    {
                        Type = Constants.LayoutTypes[Constants.Layouts.Focus],
                        ZoneCount = 10,
                    },
                    new TemplateLayoutWrapper
                    {
                        Type = Constants.LayoutTypes[Constants.Layouts.Rows],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 10,
                        SensitivityRadius = 10,
                    },
                    new TemplateLayoutWrapper
                    {
                        Type = Constants.LayoutTypes[Constants.Layouts.Columns],
                        ZoneCount = 2,
                        ShowSpacing = true,
                        Spacing = 20,
                        SensitivityRadius = 20,
                    },
                    new TemplateLayoutWrapper
                    {
                        Type = Constants.LayoutTypes[Constants.Layouts.Grid],
                        ZoneCount = 4,
                        ShowSpacing = false,
                        Spacing = 10,
                        SensitivityRadius = 30,
                    },
                    new TemplateLayoutWrapper
                    {
                        Type = Constants.LayoutTypes[Constants.Layouts.PriorityGrid],
                        ZoneCount = 3,
                        ShowSpacing = true,
                        Spacing = 1,
                        SensitivityRadius = 40,
                    },
                },
            };
            _templatesIOHelper = new IOTestHelper(layoutTemplates.File);
            _templatesIOHelper.WriteData(layoutTemplates.Serialize(list));

            // Default layouts should match templates
            DefaultLayouts defaultLayouts = new DefaultLayouts();
            DefaultLayoutsListWrapper defaultLayoutsList = new DefaultLayoutsListWrapper
            {
                DefaultLayouts = new List<DefaultLayoutWrapper>
                {
                    new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Vertical.ToString(),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = Constants.LayoutTypes[Constants.Layouts.Rows],
                            ZoneCount = 2,
                            ShowSpacing = true,
                            Spacing = 10,
                            SensitivityRadius = 10,
                        },
                    },
                    new DefaultLayoutWrapper
                    {
                        MonitorConfiguration = MonitorConfigurationType.Horizontal.ToString(),
                        Layout = new DefaultLayoutWrapper.LayoutWrapper
                        {
                            Type = Constants.LayoutTypes[Constants.Layouts.PriorityGrid],
                            ZoneCount = 3,
                            ShowSpacing = true,
                            Spacing = 1,
                            SensitivityRadius = 40,
                        },
                    },
                },
            };
            _defaultLayoutsIOHelper = new IOTestHelper(defaultLayouts.File);
            _defaultLayoutsIOHelper.WriteData(defaultLayouts.Serialize(defaultLayoutsList));

            // Make sure applied layouts don't replate template settings
            AppliedLayouts appliedLayouts = new AppliedLayouts();
            AppliedLayoutsListWrapper appliedLayoutsList = new AppliedLayoutsListWrapper
            {
                AppliedLayouts = new List<AppliedLayoutWrapper>
                {
                    new AppliedLayoutWrapper
                    {
                        Device = new AppliedLayoutWrapper.DeviceIdWrapper
                        {
                            Monitor = "monitor-1",
                            MonitorInstance = "instance-id-1",
                            MonitorNumber = 1,
                            SerialNumber = "serial-number-1",
                            VirtualDesktop = "{FF34D993-73F3-4B8C-AA03-73730A01D6A8}",
                        },
                        AppliedLayout = new AppliedLayoutWrapper.LayoutWrapper
                        {
                            Type = Constants.LayoutTypes[Constants.Layouts.PriorityGrid],
                            ZoneCount = 3,
                            ShowSpacing = true,
                            Spacing = 1,
                            SensitivityRadius = 40,
                        },
                    },
                },
            };
            _appliedLayoutsIOHelper = new IOTestHelper(appliedLayouts.File);
            _appliedLayoutsIOHelper.WriteData(appliedLayouts.Serialize(appliedLayoutsList));

            _session = new FancyZonesEditorSession(_context!);

            foreach (var (key, name) in Constants.LayoutNames)
            {
                if (key == Constants.Layouts.Empty)
                {
                    continue;
                }

                _session?.Click_EditLayout(name);
                Assert.IsNotNull(_session?.Session?.FindElementsByName($"Edit '{name}'"));

                // Number of zones slider. Possible range is from 1 to 128. Value is 3.
                var slider = _session.GetZoneCountSlider();
                var expectedZoneCount = list.LayoutTemplates.Find(x => x.Type == Constants.LayoutTypes[key]).ZoneCount;
                Assert.AreEqual($"{expectedZoneCount}", slider.Text);

                _session?.Click_Cancel();
            }
        }
    }
}
