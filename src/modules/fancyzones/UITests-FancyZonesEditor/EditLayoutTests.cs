// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernWpf.Controls;
using OpenQA.Selenium.Appium.Windows;
using static FancyZonesEditorCommon.Data.CustomLayouts;
using static FancyZonesEditorCommon.Data.EditorParameters;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class EditLayoutTests : UITestBase
    {
        public EditLayoutTests()
            : base(PowerToysModule.FancyZone)
        {
        }

        private static readonly CustomLayouts.CustomLayoutListWrapper Layouts = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayoutWrapper
                {
                    Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid custom layout",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
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
                new CustomLayoutWrapper
                {
                    Uuid = "{0EB9BF3E-010E-46D7-8681-1879D1E111E1}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Grid-9",
                    Info = new CustomLayouts().ToJsonElement(new GridInfoWrapper
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
                new CustomLayoutWrapper
                {
                    Uuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}",
                    Type = CustomLayout.Canvas.TypeToString(),
                    Name = "Canvas custom layout",
                    Info = new CustomLayouts().ToJsonElement(new CanvasInfoWrapper
                    {
                        RefHeight = 1040,
                        RefWidth = 1920,
                        SensitivityRadius = 10,
                        Zones = new List<CanvasInfoWrapper.CanvasZoneWrapper>
                        {
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 0,
                                Width = 500,
                                Height = 250,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 500,
                                Y = 0,
                                Width = 1420,
                                Height = 500,
                            },
                            new CanvasInfoWrapper.CanvasZoneWrapper
                            {
                                X = 0,
                                Y = 250,
                                Width = 1920,
                                Height = 500,
                            },
                        },
                    }),
                },
            },
        };

        [TestInitialize]
        public void TestInitialize()
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
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));

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

            CustomLayouts customLayouts = new CustomLayouts();
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(Layouts));

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

            this.RestartScopeExe();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            FancyZonesEditorHelper.Files.Restore();
        }

        [TestMethod]
        public void OpenEditMode()
        {
            Session.Find<Element>(Layouts.CustomLayouts[0].Name).Find<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton)).Click();
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.EditZonesButton)).Click();
            Assert.IsNotNull(Session.Find<Element>(ElementName.GridLayoutEditor));
            Session.Find<Button>(ElementName.Cancel).Click();
        }

        [TestMethod]
        public void OpenEditModeFromContextMenu()
        {
            FancyZonesEditorHelper.ClickContextMenuItem(Session, Layouts.CustomLayouts[0].Name, FancyZonesEditorHelper.ElementName.EditZones);
            Assert.IsNotNull(Session.Find<Element>(ElementName.GridLayoutEditor));
            Session.Find<Button>(ElementName.Cancel).Click();
        }

        [TestMethod]
        public void Canvas_AddZone_Save()
        {
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.NewZoneButton)).Click();
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());
            Assert.AreEqual(expected.Zones.Count + 1, actual.Zones.Count);
        }

        [TestMethod]
        public void Canvas_AddZone_Cancel()
        {
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);
            Session.Find<Button>(By.AccessibilityId(AccessibilityId.NewZoneButton)).Click();
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());
            Assert.AreEqual(expected.Zones.Count, actual.Zones.Count);
        }

        [TestMethod]
        public void Canvas_DeleteZone_Save()
        {
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);
            FancyZonesEditorHelper.ClickDeleteZone(Session, 1);
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());
            Assert.AreEqual(expected.Zones.Count - 1, actual.Zones.Count);
        }

        [TestMethod]
        public void Canvas_DeleteZone_Cancel()
        {
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);
            FancyZonesEditorHelper.ClickDeleteZone(Session, 1);
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());
            Assert.AreEqual(expected.Zones.Count, actual.Zones.Count);
        }

        [TestMethod]
        public void Canvas_MoveZone_Save()
        {
            int zoneNumber = 1;
            int xOffset = 100;
            int yOffset = 100;
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.GetZone(Session, zoneNumber, FancyZonesEditorHelper.ClassName.CanvasZone)?.Drag(xOffset, yOffset);
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());

            // changed zone, exact offset may vary depending on screen resolution
            Assert.IsTrue(expected.Zones[zoneNumber - 1].X < actual.Zones[zoneNumber - 1].X, $"X: {expected.Zones[zoneNumber - 1].X} > {actual.Zones[zoneNumber - 1].X}");
            Assert.IsTrue(expected.Zones[zoneNumber - 1].Y < actual.Zones[zoneNumber - 1].Y, $"Y: {expected.Zones[zoneNumber - 1].Y} > {actual.Zones[zoneNumber - 1].Y}");
            Assert.AreEqual(expected.Zones[zoneNumber - 1].Width, actual.Zones[zoneNumber - 1].Width);
            Assert.AreEqual(expected.Zones[zoneNumber - 1].Height, actual.Zones[zoneNumber - 1].Height);

            // other zones
            for (int i = 0; i < expected.Zones.Count; i++)
            {
                if (i != zoneNumber - 1)
                {
                    Assert.AreEqual(expected.Zones[i].X, actual.Zones[i].X);
                    Assert.AreEqual(expected.Zones[i].Y, actual.Zones[i].Y);
                    Assert.AreEqual(expected.Zones[i].Width, actual.Zones[i].Width);
                    Assert.AreEqual(expected.Zones[i].Height, actual.Zones[i].Height);
                }
            }
        }

        [TestMethod]
        public void Canvas_MoveZone_Cancel()
        {
            int zoneNumber = 1;
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.GetZone(Session, zoneNumber, FancyZonesEditorHelper.ClassName.CanvasZone)?.Drag(100, 100);
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());
            for (int i = 0; i < expected.Zones.Count; i++)
            {
                Assert.AreEqual(expected.Zones[i].X, actual.Zones[i].X);
                Assert.AreEqual(expected.Zones[i].Y, actual.Zones[i].Y);
                Assert.AreEqual(expected.Zones[i].Width, actual.Zones[i].Width);
                Assert.AreEqual(expected.Zones[i].Height, actual.Zones[i].Height);
            }
        }

        [TestMethod]
        public void Canvas_ResizeZone_Save()
        {
            int zoneNumber = 1;
            int xOffset = 100;
            int yOffset = 100;
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.GetZone(Session, zoneNumber, FancyZonesEditorHelper.ClassName.CanvasZone)?.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.TopRightCorner)).Drag(xOffset, yOffset);
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());

            // changed zone, exact offset may vary depending on screen resolution
            Assert.AreEqual(expected.Zones[zoneNumber - 1].X, actual.Zones[zoneNumber - 1].X);
            Assert.IsTrue(expected.Zones[zoneNumber - 1].Y < actual.Zones[zoneNumber - 1].Y, $"Y: {expected.Zones[zoneNumber - 1].Y} > {actual.Zones[zoneNumber - 1].Y}");
            Assert.IsTrue(expected.Zones[zoneNumber - 1].Width < actual.Zones[zoneNumber - 1].Width, $"Width: {expected.Zones[zoneNumber - 1].Width} < {actual.Zones[zoneNumber - 1].Width}");
            Assert.IsTrue(expected.Zones[zoneNumber - 1].Height > actual.Zones[zoneNumber - 1].Height, $"Height: {expected.Zones[zoneNumber - 1].Height} < {actual.Zones[zoneNumber - 1].Height}");

            // other zones
            for (int i = 0; i < expected.Zones.Count; i++)
            {
                if (i != zoneNumber - 1)
                {
                    Assert.AreEqual(expected.Zones[i].X, actual.Zones[i].X);
                    Assert.AreEqual(expected.Zones[i].Y, actual.Zones[i].Y);
                    Assert.AreEqual(expected.Zones[i].Width, actual.Zones[i].Width);
                    Assert.AreEqual(expected.Zones[i].Height, actual.Zones[i].Height);
                }
            }
        }

        [TestMethod]
        public void Canvas_ResizeZone_Cancel()
        {
            int zoneNumber = 1;
            int xOffset = 100;
            int yOffset = 100;
            var canvas = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Canvas.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, canvas.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.GetZone(Session, zoneNumber, FancyZonesEditorHelper.ClassName.CanvasZone)?.Find<Element>(By.AccessibilityId(FancyZonesEditorHelper.AccessibilityId.TopRightCorner)).Drag(xOffset, yOffset);
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.CanvasFromJsonElement(canvas.Info.ToString());
            var actual = customLayouts.CanvasFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == canvas.Uuid).Info.GetRawText());

            for (int i = 0; i < expected.Zones.Count; i++)
            {
                Assert.AreEqual(expected.Zones[i].X, actual.Zones[i].X);
                Assert.AreEqual(expected.Zones[i].Y, actual.Zones[i].Y);
                Assert.AreEqual(expected.Zones[i].Width, actual.Zones[i].Width);
                Assert.AreEqual(expected.Zones[i].Height, actual.Zones[i].Height);
            }
        }

        [TestMethod]
        public void Grid_SplitZone_Save()
        {
            int zoneNumber = 1;
            var grid = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, grid.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.GetZone(Session, zoneNumber, FancyZonesEditorHelper.ClassName.GridZone)?.Click(); // horizontal split in the middle of the zone
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.GridFromJsonElement(grid.Info.ToString());
            var actual = customLayouts.GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == grid.Uuid).Info.GetRawText());

            // new column added
            Assert.AreEqual(expected.Columns + 1, actual.Columns);
            Assert.AreEqual(expected.ColumnsPercentage[0], actual.ColumnsPercentage[0] + actual.ColumnsPercentage[1]);
            Assert.AreEqual(expected.ColumnsPercentage[1], actual.ColumnsPercentage[2]);

            // rows are not changed
            Assert.AreEqual(expected.Rows, actual.Rows);
            for (int i = 0; i < expected.Rows; i++)
            {
                Assert.AreEqual(expected.RowsPercentage[i], actual.RowsPercentage[i]);
            }
        }

        [TestMethod]
        public void Grid_SplitZone_Cancel()
        {
            int zoneNumber = 1;
            var grid = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, grid.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.GetZone(Session, zoneNumber, FancyZonesEditorHelper.ClassName.GridZone)?.Click(); // horizontal split in the middle of the zone
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.GridFromJsonElement(grid.Info.ToString());
            var actual = customLayouts.GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == grid.Uuid).Info.GetRawText());

            // columns are not changed
            Assert.AreEqual(expected.Columns, actual.Columns);
            for (int i = 0; i < expected.Columns; i++)
            {
                Assert.AreEqual(expected.ColumnsPercentage[i], actual.ColumnsPercentage[i]);
            }

            // rows are not changed
            Assert.AreEqual(expected.Rows, actual.Rows);
            for (int i = 0; i < expected.Rows; i++)
            {
                Assert.AreEqual(expected.RowsPercentage[i], actual.RowsPercentage[i]);
            }
        }

        [TestMethod]
        public void Grid_MergeZones_Save()
        {
            var grid = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, grid.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.MergeGridZones(Session, 1, 2);
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.GridFromJsonElement(grid.Info.ToString());
            var actual = customLayouts.GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == grid.Uuid).Info.GetRawText());

            // columns are not changed
            Assert.AreEqual(expected.Columns, actual.Columns);
            for (int i = 0; i < expected.Columns; i++)
            {
                Assert.AreEqual(expected.ColumnsPercentage[i], actual.ColumnsPercentage[i]);
            }

            // rows are not changed
            Assert.AreEqual(expected.Rows, actual.Rows);
            for (int i = 0; i < expected.Rows; i++)
            {
                Assert.AreEqual(expected.RowsPercentage[i], actual.RowsPercentage[i]);
            }

            // cells are updated to [0,0][1,2]
            Assert.IsTrue(actual.CellChildMap[0].SequenceEqual([0, 0]));
            Assert.IsTrue(actual.CellChildMap[1].SequenceEqual([1, 2]));
        }

        [TestMethod]
        public void Grid_MergeZones_Cancel()
        {
            var grid = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString());
            FancyZonesEditorHelper.ClickContextMenuItem(Session, grid.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.MergeGridZones(Session, 1, 2);
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.GridFromJsonElement(grid.Info.ToString());
            var actual = customLayouts.GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == grid.Uuid).Info.GetRawText());

            // columns are not changed
            Assert.AreEqual(expected.Columns, actual.Columns);
            for (int i = 0; i < expected.Columns; i++)
            {
                Assert.AreEqual(expected.ColumnsPercentage[i], actual.ColumnsPercentage[i]);
            }

            // rows are not changed
            Assert.AreEqual(expected.Rows, actual.Rows);
            for (int i = 0; i < expected.Rows; i++)
            {
                Assert.AreEqual(expected.RowsPercentage[i], actual.RowsPercentage[i]);
            }

            // cells are not changed
            for (int i = 0; i < expected.CellChildMap.Length; i++)
            {
                Assert.IsTrue(actual.CellChildMap[i].SequenceEqual(expected.CellChildMap[i]));
            }
        }

        [TestMethod]
        public void Grid_MoveSplitter_Save()
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
                        WorkAreaHeight = 780,
                        WorkAreaWidth = 1240,
                        MonitorHeight = 780,
                        MonitorWidth = 1240,
                        IsSelected = true,
                    },
                },
            };
            FancyZonesEditorHelper.Files.ParamsIOHelper.WriteData(editorParameters.Serialize(parameters));
            this.RestartScopeExe();

            var grid = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString() && x.Name == "Grid-9");
            FancyZonesEditorHelper.ClickContextMenuItem(Session, grid.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.MoveSplitter(Session, 2, -50, 0);
            Session.Find<Button>(ElementName.Save).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.GridFromJsonElement(grid.Info.ToString());
            var actual = customLayouts.GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == grid.Uuid).Info.GetRawText());

            // rows are not changed
            Assert.AreEqual(expected.Rows, actual.Rows);
            for (int i = 0; i < expected.Rows; i++)
            {
                Assert.AreEqual(expected.RowsPercentage[i], actual.RowsPercentage[i]);
            }

            // Columns are changed
            Assert.AreEqual(expected.Columns, actual.Columns);
            Assert.IsTrue(expected.ColumnsPercentage[0] > actual.ColumnsPercentage[0], $"{expected.ColumnsPercentage[0]} > {actual.ColumnsPercentage[0]}");
            Assert.IsTrue(expected.ColumnsPercentage[1] < actual.ColumnsPercentage[1], $"{expected.ColumnsPercentage[1]} < {actual.ColumnsPercentage[1]}");
            Assert.AreEqual(expected.ColumnsPercentage[2], actual.ColumnsPercentage[2], $"{expected.ColumnsPercentage[2]} == {actual.ColumnsPercentage[2]}");

            // cells are not changed
            for (int i = 0; i < expected.CellChildMap.Length; i++)
            {
                Assert.IsTrue(actual.CellChildMap[i].SequenceEqual(expected.CellChildMap[i]));
            }
        }

        [TestMethod]
        public void Grid_MoveSplitter_Cancel()
        {
            var grid = Layouts.CustomLayouts.Find(x => x.Type == CustomLayout.Grid.TypeToString() && x.Name == "Grid-9");
            FancyZonesEditorHelper.ClickContextMenuItem(Session, grid.Name, FancyZonesEditorHelper.ElementName.EditZones);

            FancyZonesEditorHelper.MoveSplitter(Session, 2, -100, 0);
            Session.Find<Button>(ElementName.Cancel).Click();

            // check the file
            var customLayouts = new CustomLayouts();
            var data = customLayouts.Read(customLayouts.File);
            var expected = customLayouts.GridFromJsonElement(grid.Info.ToString());
            var actual = customLayouts.GridFromJsonElement(data.CustomLayouts.Find(x => x.Uuid == grid.Uuid).Info.GetRawText());

            // columns are not changed
            Assert.AreEqual(expected.Columns, actual.Columns);
            for (int i = 0; i < expected.Columns; i++)
            {
                Assert.AreEqual(expected.ColumnsPercentage[i], actual.ColumnsPercentage[i]);
            }

            // rows are not changed
            Assert.AreEqual(expected.Rows, actual.Rows);
            for (int i = 0; i < expected.Rows; i++)
            {
                Assert.AreEqual(expected.RowsPercentage[i], actual.RowsPercentage[i]);
            }

            // cells are not changed
            for (int i = 0; i < expected.CellChildMap.Length; i++)
            {
                Assert.IsTrue(actual.CellChildMap[i].SequenceEqual(expected.CellChildMap[i]));
            }
        }
    }
}
