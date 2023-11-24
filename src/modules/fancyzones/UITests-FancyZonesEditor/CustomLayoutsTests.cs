// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class CustomLayoutsTests
    {
        private static readonly CustomLayoutListWrapper Layouts = new CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayoutWrapper>
            {
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid],
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = new List<int> { 2967, 7033 },
                        ColumnsPercentage = new List<int> { 2410, 6040, 1550 },
                        CellChildMap = new int[][] { new int[] { 0, 1, 1 }, new int[] { 0, 2, 3 } },
                        SensitivityRadius = 30,
                        Spacing = 26,
                        ShowSpacing = false,
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas],
                    Name = "Canvas custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 952,
                        RefWidth = 1500,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper>
                        {
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 900,
                                Height = 522,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 900,
                                Y = 0,
                                Width = 600,
                                Height = 750,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 522,
                                Width = 1500,
                                Height = 430,
                            },
                        },
                    }),
                },
                new CustomLayoutWrapper
                {
                    Uuid = "{F1A94F38-82B6-4876-A653-70D0E882DE2A}",
                    Type = Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid],
                    Name = "Grid custom layout spacing enabled",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
                    {
                        Rows = 2,
                        Columns = 3,
                        RowsPercentage = new List<int> { 2967, 7033 },
                        ColumnsPercentage = new List<int> { 2410, 6040, 1550 },
                        CellChildMap = new int[][] { new int[] { 0, 1, 1 }, new int[] { 0, 2, 3 } },
                        SensitivityRadius = 30,
                        Spacing = 10,
                        ShowSpacing = true,
                    }),
                },
            },
        };

        private static TestContext? _context;
        private static FancyZonesEditorSession? _session;
        private static IOTestHelper? _editorParamsIOHelper;
        private static IOTestHelper? _customLayoutsIOHelper;

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

            CustomLayouts customLayouts = new CustomLayouts();
            _customLayoutsIOHelper = new IOTestHelper(customLayouts.File);
            _customLayoutsIOHelper.WriteData(customLayouts.Serialize(Layouts));
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _editorParamsIOHelper?.RestoreData();
            _customLayoutsIOHelper?.RestoreData();

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
        public void Name_Initialize()
        {
            // verify all custom layouts are presented
            foreach (var layout in Layouts.CustomLayouts)
            {
                Assert.IsNotNull(_session?.GetLayout(layout.Name));
            }
        }

        [TestMethod]
        public void Rename_Save()
        {
            string newName = "New layout name";
            var oldName = Layouts.CustomLayouts[0].Name;

            // rename the layout
            _session?.Click_EditLayout(oldName);
            var input = _session?.GetNameInput();
            input?.Clear();
            input?.SendKeys(newName);

            // verify new name
            _session?.Click_Save();
            Assert.IsNull(_session?.GetLayout(oldName)); // previous name isn't presented
            Assert.IsNotNull(newName); // new name is presented
        }

        [TestMethod]
        public void Rename_Cancel()
        {
            string newName = "New layout name";
            var oldName = Layouts.CustomLayouts[0].Name;

            // rename the layout
            _session?.Click_EditLayout(oldName);
            var input = _session?.GetNameInput();
            input?.Clear();
            input?.SendKeys(newName);

            // verify new name
            _session?.Click_Cancel();
            Assert.IsNotNull(_session?.GetLayout(oldName));
            Assert.IsNull(newName);
        }

        [TestMethod]
        public void HighlightDistance_Initialize()
        {
            foreach (var layout in Layouts.CustomLayouts)
            {
                _session?.Click_EditLayout(layout.Name);

                var slider = _session?.GetSensitivitySlider();
                var expected = layout.Type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas] ?
                    new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius :
                    new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;
                Assert.AreEqual($"{expected}", slider?.Text);

                _session?.Click_Cancel();

                // let the dialog window close
                _session?.WaitFor(0.5f);
            }
        }

        [TestMethod]
        public void HighlightDistance_Save()
        {
            var layout = Layouts.CustomLayouts[0];
            var type = layout.Type;
            _session?.Click_EditLayout(layout.Name);

            var slider = _session?.GetSensitivitySlider();
            slider?.SendKeys(Keys.Right);

            var value = type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas] ?
                    new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius :
                    new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;
            var expected = value + 1; // one step right

            Assert.AreEqual($"{expected}", slider?.Text);

            _session?.Click_Save();

            // let the dialog window close
            _session?.WaitFor(0.5f);

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas] ?
                new CustomLayouts().CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius :
                new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void HighlightDistance_Cancel()
        {
            var layout = Layouts.CustomLayouts[0];
            var type = layout.Type;
            _session?.Click_EditLayout(layout.Name);

            var slider = _session?.GetSensitivitySlider();
            slider?.SendKeys(Keys.Right);

            var expected = type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas] ?
                    new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius :
                    new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;

            _session?.Click_Cancel();

            // let the dialog window close
            _session?.WaitFor(0.5f);

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Canvas] ?
                new CustomLayouts().CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius :
                new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).SensitivityRadius;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Initialize()
        {
            foreach (var layout in Layouts.CustomLayouts)
            {
                if (layout.Type != Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid])
                {
                    // only for grid layouts
                    continue;
                }

                _session?.Click_EditLayout(layout.Name);

                var toggle = _session?.GetSpaceAroundZonesToggle();
                var slider = _session?.GetSpaceAroundZonesSlider();

                var spacingEnabled = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).ShowSpacing;
                Assert.AreEqual(spacingEnabled, slider?.Enabled);
                Assert.AreEqual(spacingEnabled, toggle?.Selected);

                var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).Spacing;
                Assert.AreEqual($"{expected}", slider?.Text);

                _session?.Click_Cancel();

                // let the dialog window close
                _session?.WaitFor(0.5f);
            }
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Save()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid] && new CustomLayouts().GridFromJsonElement(x.Info.GetRawText()).ShowSpacing);
            var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).Spacing + 1; // one step right
            _session?.Click_EditLayout(layout.Name);

            var slider = _session?.GetSpaceAroundZonesSlider();
            slider?.SendKeys(Keys.Right);
            Assert.AreEqual($"{expected}", slider?.Text);

            _session?.Click_Save();

            // let the dialog window close
            _session?.WaitFor(0.5f);

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Slider_Cancel()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid] && new CustomLayouts().GridFromJsonElement(x.Info.GetRawText()).ShowSpacing);
            _session?.Click_EditLayout(layout.Name);
            var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).Spacing;

            var slider = _session?.GetSpaceAroundZonesSlider();
            slider?.SendKeys(Keys.Right);
            _session?.Click_Cancel();

            // let the dialog window close
            _session?.WaitFor(0.5f);

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).Spacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Save()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid]);
            var value = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).ShowSpacing;
            var expected = !value;
            _session?.Click_EditLayout(layout.Name);

            var toggle = _session?.GetSpaceAroundZonesToggle();
            toggle?.Click();
            Assert.AreEqual(expected, toggle?.Selected, "Toggle value not changed");
            Assert.AreEqual(expected, _session?.GetSpaceAroundZonesSlider()?.Enabled);

            _session?.Click_Save();

            // let the dialog window close
            _session?.WaitFor(0.5f);

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SpaceAroundZones_Toggle_Cancel()
        {
            var layout = Layouts.CustomLayouts.Find(x => x.Type == Constants.CustomLayoutTypeNames[Constants.CustomLayoutType.Grid]);
            var expected = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).ShowSpacing;
            _session?.Click_EditLayout(layout.Name);

            var toggle = _session?.GetSpaceAroundZonesToggle();
            toggle?.Click();
            Assert.AreNotEqual(expected, toggle?.Selected, "Toggle value not changed");
            Assert.AreNotEqual(expected, _session?.GetSpaceAroundZonesSlider()?.Enabled);

            _session?.Click_Cancel();

            // let the dialog window close
            _session?.WaitFor(0.5f);

            // verify the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var actual = new CustomLayouts().GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == layout.Uuid).Info.GetRawText()).ShowSpacing;
            Assert.AreEqual(expected, actual);
        }
    }
}
