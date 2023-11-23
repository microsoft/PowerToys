// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Support.UI;
using static FancyZonesEditorCommon.Data.AppliedLayouts;
using static FancyZonesEditorCommon.Data.DefaultLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static FancyZonesEditorCommon.Data.LayoutTemplates;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class TemplateLayoutsTests
    {
        private static readonly TemplateLayoutsListWrapper Layouts = new TemplateLayoutsListWrapper
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
            _templatesIOHelper = new IOTestHelper(layoutTemplates.File);
            _templatesIOHelper.WriteData(layoutTemplates.Serialize(Layouts));

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
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _editorParamsIOHelper?.RestoreData();
            _templatesIOHelper?.RestoreData();
            _defaultLayoutsIOHelper?.RestoreData();
            _appliedLayoutsIOHelper?.RestoreData();

            _context = null;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _session = new FancyZonesEditorSession(_context!);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _session?.Close(_context!);
        }

        [TestMethod]
        public void ZoneNumber()
        {
            foreach (var (key, name) in Constants.LayoutNames)
            {
                if (key == Constants.Layouts.Empty)
                {
                    continue;
                }

                _session?.Click_EditLayout(name);

                var slider = _session?.GetZoneCountSlider();
                var expected = Layouts.LayoutTemplates.Find(x => x.Type == Constants.LayoutTypes[key]).ZoneCount;
                Assert.AreEqual($"{expected}", slider?.Text);

                _session?.Click_Cancel();

                // let the dialog window close
                WebDriverWait wait = new WebDriverWait(_session?.Session, TimeSpan.FromSeconds(1));
                wait.Timeout = TimeSpan.FromSeconds(0.5);
            }
        }

        [TestMethod]
        public void HighlightDistance()
        {
            foreach (var (key, name) in Constants.LayoutNames)
            {
                if (key == Constants.Layouts.Empty)
                {
                    continue;
                }

                _session?.Click_EditLayout(name);

                var slider = _session?.GetSensitivitySlider();
                var expected = Layouts.LayoutTemplates.Find(x => x.Type == Constants.LayoutTypes[key]).SensitivityRadius;
                Assert.AreEqual($"{expected}", slider?.Text);

                _session?.Click_Cancel();

                // let the dialog window close
                WebDriverWait wait = new WebDriverWait(_session?.Session, TimeSpan.FromSeconds(1));
                wait.Timeout = TimeSpan.FromSeconds(0.5);
            }
        }

        [TestMethod]
        public void SpaceAroundZones()
        {
            foreach (var (key, name) in Constants.LayoutNames)
            {
                if (key == Constants.Layouts.Empty || key == Constants.Layouts.Focus)
                {
                    // only for grid layouts
                    continue;
                }

                _session?.Click_EditLayout(name);

                var toggle = _session?.GetSpaceAroundZonesToggle();
                var slider = _session?.GetSpaceAroudZonesSlider();

                var spacingEnabled = Layouts.LayoutTemplates.Find(x => x.Type == Constants.LayoutTypes[key]).ShowSpacing;
                Assert.AreEqual(spacingEnabled, slider?.Enabled);

                var expected = Layouts.LayoutTemplates.Find(x => x.Type == Constants.LayoutTypes[key]).Spacing;
                Assert.AreEqual($"{expected}", slider?.Text);

                _session?.Click_Cancel();

                // let the dialog window close
                WebDriverWait wait = new WebDriverWait(_session?.Session, TimeSpan.FromSeconds(1));
                wait.Timeout = TimeSpan.FromSeconds(0.5);
            }
        }
    }
}
