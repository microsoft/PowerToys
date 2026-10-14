// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class EditLayoutTests : FancyZonesEditorTestBase
{
    private const string EditorProcessName = "PowerToys.FancyZonesEditor";
    private const string GridLayoutUuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";
    private const string Grid9LayoutUuid = "{0EB9BF3E-010E-46D7-8681-1879D1E111E1}";
    private const string CanvasLayoutUuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}";
    private const string GridLayoutName = "Grid custom layout";
    private const string Grid9LayoutName = "Grid-9";
    private const string CanvasLayoutName = "Canvas custom layout";

    private sealed record ZoneEditorContext(Session Controls, Session Surface);

    private sealed record ElementBounds(int X, int Y, int Width, int Height, string ClassName);

    private static readonly CustomLayouts.CustomLayoutListWrapper SeedLayouts = new()
    {
        CustomLayouts =
        [
            new CustomLayouts.CustomLayoutWrapper
            {
                Uuid = GridLayoutUuid,
                Type = CustomLayout.Grid.TypeToString(),
                Name = GridLayoutName,
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
                Uuid = Grid9LayoutUuid,
                Type = CustomLayout.Grid.TypeToString(),
                Name = Grid9LayoutName,
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
                Uuid = CanvasLayoutUuid,
                Type = CustomLayout.Canvas.TypeToString(),
                Name = CanvasLayoutName,
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
    };

    public EditLayoutTests()
    {
        EditorTestData.WriteForEditLayoutTests(Files);
    }

    [TestMethod("FancyZonesEditor.Basic.OpenEditMode")]
    [TestCategory("FancyZones Editor #7")]
    public void OpenEditMode()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, GridLayoutName);

        var gridEditor = EditorUiTestHelper.EnterZoneEditModeFromDialog(this, Session, EditorUiTestHelper.ElementName.GridLayoutEditor);
        Assert.IsTrue(gridEditor.WaitForElement(By.Name(EditorUiTestHelper.ElementName.Save), 10_000));

        EditorUiTestHelper.Step(this, "Closing the grid editor with Cancel");
        gridEditor.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
    }

    [TestMethod("FancyZonesEditor.Basic.OpenEditModeFromContextMenu")]
    [TestCategory("FancyZones Editor #7")]
    public void OpenEditModeFromContextMenu()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var gridEditor = EditorUiTestHelper.EnterZoneEditModeFromContextMenu(this, Session, GridLayoutName, EditorUiTestHelper.ElementName.GridLayoutEditor);
    Assert.IsTrue(gridEditor.WaitForElement(By.Name(EditorUiTestHelper.ElementName.Save), 10_000));

        EditorUiTestHelper.Step(this, "Closing the grid editor with Cancel");
        gridEditor.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_AddZone_Save")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_AddZone_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var seedCanvas = GetSeedLayout(CanvasLayoutUuid);
        var expected = new CustomLayouts().CanvasFromJsonElement(seedCanvas.Info.GetRawText());

        var canvasEditor = OpenCanvasEditorFromDialog();
        EditorUiTestHelper.Step(this, "Adding a new canvas zone");
        canvasEditor.Controls.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewZoneButton)).Click();

        EditorUiTestHelper.Step(this, "Saving canvas layout changes");
        canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();

        var actualLayout = ReadLayoutByUuid(CanvasLayoutUuid);
        var actual = new CustomLayouts().CanvasFromJsonElement(actualLayout.Info.GetRawText());
        Assert.AreEqual(expected.Zones.Count + 1, actual.Zones.Count);
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_AddZone_Cancel")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_AddZone_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var canvasEditor = OpenCanvasEditorFromDialog();
        EditorUiTestHelper.Step(this, "Adding a new canvas zone and cancelling");
        canvasEditor.Controls.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewZoneButton)).Click();
        canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();

        AssertLayoutJsonUnchanged(CanvasLayoutUuid, GetSeedLayout(CanvasLayoutUuid).Info.GetRawText());
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_DeleteZone_Save")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_DeleteZone_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var seedCanvas = GetSeedLayout(CanvasLayoutUuid);
        var expected = new CustomLayouts().CanvasFromJsonElement(seedCanvas.Info.GetRawText());

        var canvasEditor = OpenCanvasEditorFromDialog();
        DeleteCanvasZone(canvasEditor.Surface, zoneNumber: 1);

        EditorUiTestHelper.Step(this, "Saving canvas layout changes");
        canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();

        var actualLayout = ReadLayoutByUuid(CanvasLayoutUuid);
        var actual = new CustomLayouts().CanvasFromJsonElement(actualLayout.Info.GetRawText());
        Assert.AreEqual(expected.Zones.Count - 1, actual.Zones.Count);
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_DeleteZone_Cancel")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_DeleteZone_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var canvasEditor = OpenCanvasEditorFromDialog();
        DeleteCanvasZone(canvasEditor.Surface, zoneNumber: 1);

        EditorUiTestHelper.Step(this, "Cancelling canvas layout changes");
        canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();

        AssertLayoutJsonUnchanged(CanvasLayoutUuid, GetSeedLayout(CanvasLayoutUuid).Info.GetRawText());
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_MoveZone_Save")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_MoveZone_Save()
    {
        const int zoneNumber = 1;
        const int xOffset = 100;
        const int yOffset = 100;

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = new CustomLayouts().CanvasFromJsonElement(GetSeedLayout(CanvasLayoutUuid).Info.GetRawText());

        var canvasEditor = OpenCanvasEditorFromDialog();
        try
        {
            var zone = FindZone(canvasEditor.Surface, zoneNumber, EditorUiTestHelper.ClassName.CanvasZone);
            DragWithOffset(canvasEditor.Surface, zone, xOffset, yOffset, $"moving canvas zone {zoneNumber}");

            EditorUiTestHelper.Step(this, "Saving moved canvas zone");
            canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        var actual = new CustomLayouts().CanvasFromJsonElement(ReadLayoutByUuid(CanvasLayoutUuid).Info.GetRawText());

        Assert.IsTrue(expected.Zones[zoneNumber - 1].X < actual.Zones[zoneNumber - 1].X, $"Expected moved zone X to increase. Expected={expected.Zones[zoneNumber - 1].X}, Actual={actual.Zones[zoneNumber - 1].X}");
        Assert.IsTrue(expected.Zones[zoneNumber - 1].Y < actual.Zones[zoneNumber - 1].Y, $"Expected moved zone Y to increase. Expected={expected.Zones[zoneNumber - 1].Y}, Actual={actual.Zones[zoneNumber - 1].Y}");
        Assert.AreEqual(expected.Zones[zoneNumber - 1].Width, actual.Zones[zoneNumber - 1].Width);
        Assert.AreEqual(expected.Zones[zoneNumber - 1].Height, actual.Zones[zoneNumber - 1].Height);

        for (var index = 0; index < expected.Zones.Count; index++)
        {
            if (index == zoneNumber - 1)
            {
                continue;
            }

            Assert.AreEqual(expected.Zones[index].X, actual.Zones[index].X, $"Zone {index + 1} X changed unexpectedly.");
            Assert.AreEqual(expected.Zones[index].Y, actual.Zones[index].Y, $"Zone {index + 1} Y changed unexpectedly.");
            Assert.AreEqual(expected.Zones[index].Width, actual.Zones[index].Width, $"Zone {index + 1} Width changed unexpectedly.");
            Assert.AreEqual(expected.Zones[index].Height, actual.Zones[index].Height, $"Zone {index + 1} Height changed unexpectedly.");
        }
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_MoveZone_Cancel")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_MoveZone_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var canvasEditor = OpenCanvasEditorFromDialog();
        try
        {
            var zone = FindZone(canvasEditor.Surface, zoneNumber: 1, EditorUiTestHelper.ClassName.CanvasZone);
            DragWithOffset(canvasEditor.Surface, zone, 100, 100, "moving canvas zone 1 before cancel");

            EditorUiTestHelper.Step(this, "Cancelling moved canvas zone");
            canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        AssertLayoutJsonUnchanged(CanvasLayoutUuid, GetSeedLayout(CanvasLayoutUuid).Info.GetRawText());
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_ResizeZone_Save")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_ResizeZone_Save()
    {
        const int zoneNumber = 1;
        const int xOffset = 100;
        const int yOffset = 100;

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = new CustomLayouts().CanvasFromJsonElement(GetSeedLayout(CanvasLayoutUuid).Info.GetRawText());

        var canvasEditor = OpenCanvasEditorFromDialog();
        try
        {
            var zone = FindZone(canvasEditor.Surface, zoneNumber, EditorUiTestHelper.ClassName.CanvasZone);
            var topRightResizeThumb = canvasEditor.Surface.FindAll<Thumb>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TopRightCorner), 5_000)
                .Where(candidate => IsCenterInside(candidate, zone))
                .FirstOrDefault();

            Assert.IsNotNull(topRightResizeThumb, $"Could not find '{EditorUiTestHelper.AccessibilityId.TopRightCorner}' for canvas zone {zoneNumber}.");
            DragWithOffset(canvasEditor.Surface, topRightResizeThumb!, xOffset, yOffset, $"resizing canvas zone {zoneNumber} from the top-right corner");

            EditorUiTestHelper.Step(this, "Saving resized canvas zone");
            canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        var actual = new CustomLayouts().CanvasFromJsonElement(ReadLayoutByUuid(CanvasLayoutUuid).Info.GetRawText());

        Assert.AreEqual(expected.Zones[zoneNumber - 1].X, actual.Zones[zoneNumber - 1].X);
        Assert.IsTrue(expected.Zones[zoneNumber - 1].Y < actual.Zones[zoneNumber - 1].Y, $"Expected resized zone Y to increase. Expected={expected.Zones[zoneNumber - 1].Y}, Actual={actual.Zones[zoneNumber - 1].Y}");
        Assert.IsTrue(expected.Zones[zoneNumber - 1].Width < actual.Zones[zoneNumber - 1].Width, $"Expected resized zone Width to increase. Expected={expected.Zones[zoneNumber - 1].Width}, Actual={actual.Zones[zoneNumber - 1].Width}");
        Assert.IsTrue(expected.Zones[zoneNumber - 1].Height > actual.Zones[zoneNumber - 1].Height, $"Expected resized zone Height to decrease. Expected={expected.Zones[zoneNumber - 1].Height}, Actual={actual.Zones[zoneNumber - 1].Height}");

        for (var index = 0; index < expected.Zones.Count; index++)
        {
            if (index == zoneNumber - 1)
            {
                continue;
            }

            Assert.AreEqual(expected.Zones[index].X, actual.Zones[index].X, $"Zone {index + 1} X changed unexpectedly.");
            Assert.AreEqual(expected.Zones[index].Y, actual.Zones[index].Y, $"Zone {index + 1} Y changed unexpectedly.");
            Assert.AreEqual(expected.Zones[index].Width, actual.Zones[index].Width, $"Zone {index + 1} Width changed unexpectedly.");
            Assert.AreEqual(expected.Zones[index].Height, actual.Zones[index].Height, $"Zone {index + 1} Height changed unexpectedly.");
        }
    }

    [TestMethod("FancyZonesEditor.Basic.Canvas_ResizeZone_Cancel")]
    [TestCategory("FancyZones Editor #7")]
    public void Canvas_ResizeZone_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var canvasEditor = OpenCanvasEditorFromDialog();
        try
        {
            var zone = FindZone(canvasEditor.Surface, zoneNumber: 1, EditorUiTestHelper.ClassName.CanvasZone);
            var topRightResizeThumb = canvasEditor.Surface.FindAll<Thumb>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TopRightCorner), 5_000)
                .Where(candidate => IsCenterInside(candidate, zone))
                .FirstOrDefault();

            Assert.IsNotNull(topRightResizeThumb, $"Could not find '{EditorUiTestHelper.AccessibilityId.TopRightCorner}' for canvas zone 1.");
            DragWithOffset(canvasEditor.Surface, topRightResizeThumb!, 100, 100, "resizing canvas zone 1 before cancel");

            EditorUiTestHelper.Step(this, "Cancelling resized canvas zone");
            canvasEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        AssertLayoutJsonUnchanged(CanvasLayoutUuid, GetSeedLayout(CanvasLayoutUuid).Info.GetRawText());
    }

    [TestMethod("FancyZonesEditor.Basic.Grid_SplitZone_Save")]
    [TestCategory("FancyZones Editor #8")]
    public void Grid_SplitZone_Save()
    {
        const int zoneNumber = 1;

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = new CustomLayouts().GridFromJsonElement(GetSeedLayout(GridLayoutUuid).Info.GetRawText());

        var gridEditor = OpenGridEditorFromDialog(GridLayoutName);
        EditorUiTestHelper.Step(this, "Splitting grid zone 1 by clicking inside the zone");
        var zone = FindZone(gridEditor.Surface, zoneNumber, EditorUiTestHelper.ClassName.GridZone);
        ClickCenter(gridEditor.Surface, zone, "splitting a grid zone");

        EditorUiTestHelper.Step(this, "Saving split grid layout");
        gridEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();

        var actual = new CustomLayouts().GridFromJsonElement(ReadLayoutByUuid(GridLayoutUuid).Info.GetRawText());

        Assert.AreEqual(expected.Columns + 1, actual.Columns);
        Assert.AreEqual(expected.ColumnsPercentage[0], actual.ColumnsPercentage[0] + actual.ColumnsPercentage[1]);
        Assert.AreEqual(expected.ColumnsPercentage[1], actual.ColumnsPercentage[2]);

        Assert.AreEqual(expected.Rows, actual.Rows);
        for (var index = 0; index < expected.Rows; index++)
        {
            Assert.AreEqual(expected.RowsPercentage[index], actual.RowsPercentage[index]);
        }
    }

    [TestMethod("FancyZonesEditor.Basic.Grid_SplitZone_Cancel")]
    [TestCategory("FancyZones Editor #8")]
    public void Grid_SplitZone_Cancel()
    {
        const int zoneNumber = 1;

        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var gridEditor = OpenGridEditorFromDialog(GridLayoutName);
        EditorUiTestHelper.Step(this, "Splitting grid zone 1 and cancelling");
        var zone = FindZone(gridEditor.Surface, zoneNumber, EditorUiTestHelper.ClassName.GridZone);
        ClickCenter(gridEditor.Surface, zone, "splitting a grid zone before cancel");
        gridEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();

        AssertLayoutJsonUnchanged(GridLayoutUuid, GetSeedLayout(GridLayoutUuid).Info.GetRawText());
    }

    [TestMethod("FancyZonesEditor.Basic.Grid_MergeZones_Save")]
    [TestCategory("FancyZones Editor #8")]
    public void Grid_MergeZones_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = new CustomLayouts().GridFromJsonElement(GetSeedLayout(GridLayoutUuid).Info.GetRawText());

        var gridEditor = OpenGridEditorFromDialog(GridLayoutName);
        try
        {
            var firstZone = FindZone(gridEditor.Surface, 1, EditorUiTestHelper.ClassName.GridZone);
            var secondZone = FindZone(gridEditor.Surface, 2, EditorUiTestHelper.ClassName.GridZone);
            DragToTarget(gridEditor.Surface, firstZone, secondZone, "starting grid-zone merge drag");

            EditorUiTestHelper.Step(this, "Clicking Merge zones after drag selection");
            gridEditor.Surface.Find<Element>(EditorUiTestHelper.ElementName.MergeZones).Click();
            EditorUiTestHelper.Step(this, "Saving merged grid layout");
            gridEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        var actual = new CustomLayouts().GridFromJsonElement(ReadLayoutByUuid(GridLayoutUuid).Info.GetRawText());

        Assert.AreEqual(expected.Columns, actual.Columns);
        for (var index = 0; index < expected.Columns; index++)
        {
            Assert.AreEqual(expected.ColumnsPercentage[index], actual.ColumnsPercentage[index]);
        }

        Assert.AreEqual(expected.Rows, actual.Rows);
        for (var index = 0; index < expected.Rows; index++)
        {
            Assert.AreEqual(expected.RowsPercentage[index], actual.RowsPercentage[index]);
        }

        Assert.IsTrue(actual.CellChildMap[0].SequenceEqual([0, 0]), "Expected merged first row to map to [0,0].");
        Assert.IsTrue(actual.CellChildMap[1].SequenceEqual([1, 2]), "Expected merged second row to map to [1,2].");
    }

    [TestMethod("FancyZonesEditor.Basic.Grid_MergeZones_Cancel")]
    [TestCategory("FancyZones Editor #8")]
    public void Grid_MergeZones_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var gridEditor = OpenGridEditorFromDialog(GridLayoutName);
        try
        {
            var firstZone = FindZone(gridEditor.Surface, 1, EditorUiTestHelper.ClassName.GridZone);
            var secondZone = FindZone(gridEditor.Surface, 2, EditorUiTestHelper.ClassName.GridZone);
            DragToTarget(gridEditor.Surface, firstZone, secondZone, "starting grid-zone merge drag before cancel");

            EditorUiTestHelper.Step(this, "Clicking Merge zones before cancellation");
            gridEditor.Surface.Find<Element>(EditorUiTestHelper.ElementName.MergeZones).Click();
            gridEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        AssertLayoutJsonUnchanged(GridLayoutUuid, GetSeedLayout(GridLayoutUuid).Info.GetRawText());
    }

    [TestMethod("FancyZonesEditor.Basic.Grid_MoveSplitter_Save")]
    [TestCategory("FancyZones Editor #8")]
    public void Grid_MoveSplitter_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = new CustomLayouts().GridFromJsonElement(GetSeedLayout(Grid9LayoutUuid).Info.GetRawText());

        var gridEditor = OpenGridEditorFromDialog(Grid9LayoutName);
        try
        {
            var splitter = FindSplitter(gridEditor.Surface, index: 2);
            DragWithOffset(gridEditor.Surface, splitter, -50, 0, "moving splitter index 2");

            EditorUiTestHelper.Step(this, "Saving splitter move");
            gridEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        var actual = new CustomLayouts().GridFromJsonElement(ReadLayoutByUuid(Grid9LayoutUuid).Info.GetRawText());

        Assert.AreEqual(expected.Rows, actual.Rows);
        for (var index = 0; index < expected.Rows; index++)
        {
            Assert.AreEqual(expected.RowsPercentage[index], actual.RowsPercentage[index]);
        }

        Assert.AreEqual(expected.Columns, actual.Columns);
        Assert.IsTrue(expected.ColumnsPercentage[0] > actual.ColumnsPercentage[0], $"Expected first column to shrink. Expected={expected.ColumnsPercentage[0]}, Actual={actual.ColumnsPercentage[0]}");
        Assert.IsTrue(expected.ColumnsPercentage[1] < actual.ColumnsPercentage[1], $"Expected second column to grow. Expected={expected.ColumnsPercentage[1]}, Actual={actual.ColumnsPercentage[1]}");
        Assert.AreEqual(expected.ColumnsPercentage[2], actual.ColumnsPercentage[2]);

        for (var index = 0; index < expected.CellChildMap.Length; index++)
        {
            Assert.IsTrue(actual.CellChildMap[index].SequenceEqual(expected.CellChildMap[index]), $"Grid cell map row {index} changed unexpectedly.");
        }
    }

    [TestMethod("FancyZonesEditor.Basic.Grid_MoveSplitter_Cancel")]
    [TestCategory("FancyZones Editor #8")]
    public void Grid_MoveSplitter_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var gridEditor = OpenGridEditorFromDialog(Grid9LayoutName);
        try
        {
            var splitter = FindSplitter(gridEditor.Surface, index: 2);
            DragWithOffset(gridEditor.Surface, splitter, -100, 0, "moving splitter index 2 before cancel");

            EditorUiTestHelper.Step(this, "Cancelling splitter move");
            gridEditor.Controls.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
        }
        finally
        {
            ReleaseInteractionState();
        }

        AssertLayoutJsonUnchanged(Grid9LayoutUuid, GetSeedLayout(Grid9LayoutUuid).Info.GetRawText());
    }

    private ZoneEditorContext OpenCanvasEditorFromDialog()
    {
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, CanvasLayoutName);
        var controls = EditorUiTestHelper.EnterZoneEditModeFromDialog(this, Session, EditorUiTestHelper.ElementName.CanvasLayoutEditor);
        var surface = EditorUiTestHelper.FindZoneEditorSurface(this);
        return new ZoneEditorContext(controls, surface);
    }

    private ZoneEditorContext OpenGridEditorFromDialog(string layoutName)
    {
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, layoutName);
        var controls = EditorUiTestHelper.EnterZoneEditModeFromDialog(this, Session, EditorUiTestHelper.ElementName.GridLayoutEditor);
        var surface = EditorUiTestHelper.FindZoneEditorSurface(this);
        return new ZoneEditorContext(controls, surface);
    }

    private void DeleteCanvasZone(Session canvasEditor, int zoneNumber)
    {
        var zone = FindZone(canvasEditor, zoneNumber, EditorUiTestHelper.ClassName.CanvasZone);

        EditorUiTestHelper.Step(this, $"Deleting canvas zone {zoneNumber}");
        var deleteButton = FindBoundsByControlType(canvasEditor.Inspect(depth: 12, hideOffscreen: true), "Button")
            .Where(candidate => IsCenterInside(candidate, zone))
            .FirstOrDefault();

        Assert.IsNotNull(deleteButton, $"Could not find the delete button inside canvas zone {zoneNumber}.");
        ClickCenter(canvasEditor, deleteButton!, $"deleting canvas zone {zoneNumber}");
    }

    private static CustomLayouts.CustomLayoutWrapper GetSeedLayout(string layoutUuid)
    {
        return SeedLayouts.CustomLayouts.First(layout => layout.Uuid == layoutUuid);
    }

    private static CustomLayouts.CustomLayoutWrapper ReadLayoutByUuid(string layoutUuid)
    {
        return EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == layoutUuid);
    }

    private static void AssertLayoutJsonUnchanged(string layoutUuid, string expectedRawJson)
    {
        var actualRawJson = ReadLayoutByUuid(layoutUuid).Info.GetRawText();
        Assert.AreEqual(NormalizeJson(expectedRawJson), NormalizeJson(actualRawJson), $"Expected layout '{layoutUuid}' JSON to remain unchanged after cancellation.");
    }

    private static string NormalizeJson(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private ElementBounds FindZone(Session editorSession, int zoneNumber, string zoneClassName)
    {
        var zoneMarkerText = zoneNumber.ToString(CultureInfo.InvariantCulture);
        EditorUiTestHelper.Step(this, $"Locating zone {zoneNumber} with class '{zoneClassName}'");

        var labelMarker = editorSession.FindAll<Element>(By.Name(zoneMarkerText), 2_000)
            .FirstOrDefault(element => string.Equals(element.Name, zoneMarkerText, StringComparison.Ordinal));
        Assert.IsNotNull(labelMarker, $"No exact zone marker '{zoneMarkerText}' was found.");
        var ancestors = editorSession.InspectAncestors(labelMarker!);
        var zoneBounds = FindBoundsByClassName(ancestors, zoneClassName);
        Assert.IsNotNull(
            zoneBounds,
            $"Could not map zone marker '{zoneMarkerText}' to a '{zoneClassName}' ancestor.");

        return zoneBounds!;
    }

    private static ElementBounds? FindBoundsByClassName(JsonElement element, string className)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("className", out var classNameProperty) &&
                string.Equals(classNameProperty.GetString(), className, StringComparison.Ordinal))
            {
                return new ElementBounds(
                    ReadJsonInt(element, "x"),
                    ReadJsonInt(element, "y"),
                    ReadJsonInt(element, "width"),
                    ReadJsonInt(element, "height"),
                    className);
            }

            foreach (var property in element.EnumerateObject())
            {
                var bounds = FindBoundsByClassName(property.Value, className);
                if (bounds is not null)
                {
                    return bounds;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var bounds = FindBoundsByClassName(item, className);
                if (bounds is not null)
                {
                    return bounds;
                }
            }
        }

        return null;
    }

    private static int ReadJsonInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static IReadOnlyList<ElementBounds> FindBoundsByControlType(JsonElement root, string controlType)
    {
        var matches = new List<ElementBounds>();
        CollectBoundsByControlType(root, controlType, matches);
        return matches;
    }

    private static void CollectBoundsByControlType(JsonElement element, string controlType, List<ElementBounds> matches)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var typeProperty) &&
                string.Equals(typeProperty.GetString(), controlType, StringComparison.Ordinal))
            {
                matches.Add(new ElementBounds(
                    ReadJsonInt(element, "x"),
                    ReadJsonInt(element, "y"),
                    ReadJsonInt(element, "width"),
                    ReadJsonInt(element, "height"),
                    element.TryGetProperty("className", out var classNameProperty) ? classNameProperty.GetString() ?? string.Empty : string.Empty));
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectBoundsByControlType(property.Value, controlType, matches);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectBoundsByControlType(item, controlType, matches);
            }
        }
    }

    private ElementBounds FindSplitter(Session editorSession, int index)
    {
        EditorUiTestHelper.Step(this, $"Locating splitter thumb at index {index}");

        var splitters = FindBoundsByControlType(editorSession.Inspect(depth: 12, hideOffscreen: true), "Thumb")
            .Where(element => element.Width > 0 && element.Height > 0)
            .ToList();

        Assert.IsTrue(
            splitters.Count > index,
            $"Expected at least {index + 1} splitter thumbs, but found {splitters.Count}. Splitter bounds: {DescribeBounds(splitters)}");

        return splitters[index];
    }

    private void DragWithOffset(Session editorSession, Element source, int xOffset, int yOffset, string gestureName)
    {
        EnsureWindowForeground(editorSession, gestureName);
        var startX = source.X + (source.Width / 2);
        var startY = source.Y + (source.Height / 2);
        Assert.IsTrue(
            WindowControl.IsPointOwnedByWindow(new IntPtr(editorSession.WindowHandle), startX, startY),
            $"The {gestureName} start point ({startX},{startY}) is not owned by the editor window. Window={editorSession.WindowHandle}, Bounds={DescribeBounds(source)}");

        EditorUiTestHelper.Step(this, $"Dragging for {gestureName} by ({xOffset}, {yOffset}) from {DescribeBounds(source)}");
        MouseHelper.Drag(startX, startY, startX + xOffset, startY + yOffset);
    }

    private void DragWithOffset(Session editorSession, ElementBounds source, int xOffset, int yOffset, string gestureName)
    {
        EnsureWindowForeground(editorSession, gestureName);
        var startX = source.X + (source.Width / 2);
        var startY = source.Y + (source.Height / 2);
        AssertPointOwnedByWindow(editorSession, startX, startY, gestureName, DescribeBounds(source));

        EditorUiTestHelper.Step(this, $"Dragging for {gestureName} by ({xOffset}, {yOffset}) from {DescribeBounds(source)}");
        MouseHelper.Drag(startX, startY, startX + xOffset, startY + yOffset);
    }

    private void DragToTarget(Session editorSession, ElementBounds source, ElementBounds target, string gestureName)
    {
        EnsureWindowForeground(editorSession, gestureName);
        var startX = source.X + (source.Width / 2);
        var startY = source.Y + (source.Height / 2);
        var endX = target.X + (target.Width / 2);
        var endY = target.Y + (target.Height / 2);
        AssertPointOwnedByWindow(editorSession, startX, startY, gestureName, $"Source={DescribeBounds(source)}, Target={DescribeBounds(target)}");

        EditorUiTestHelper.Step(this, $"Dragging for {gestureName} from {DescribeBounds(source)} to {DescribeBounds(target)}");
        MouseHelper.Drag(startX, startY, endX, endY);
    }

    private void ClickCenter(Session editorSession, ElementBounds target, string actionName)
    {
        EnsureWindowForeground(editorSession, actionName);
        var x = target.X + (target.Width / 2);
        var y = target.Y + (target.Height / 2);
        AssertPointOwnedByWindow(editorSession, x, y, actionName, DescribeBounds(target));

        EditorUiTestHelper.Step(this, $"Clicking for {actionName} at ({x},{y}) in {DescribeBounds(target)}");
        MouseHelper.LeftClickAt(x, y);
    }

    private static void AssertPointOwnedByWindow(Session session, int x, int y, string actionName, string bounds)
    {
        Assert.IsTrue(
            WindowControl.IsPointOwnedByWindow(new IntPtr(session.WindowHandle), x, y),
            $"The {actionName} point ({x},{y}) is not owned by editor window {session.WindowHandle}. Bounds={bounds}");
    }

    private void EnsureWindowForeground(Session session, string reason)
    {
        EditorUiTestHelper.Step(this, $"Ensuring editor window foreground ownership before {reason}");
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(session.WindowHandle), timeoutMS: 10_000, requiredConsecutiveMatches: 2),
            $"Editor window {session.WindowHandle} did not become foreground before {reason}. Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
    }

    private static bool IsCenterInside(Element child, Element parent)
    {
        var centerX = child.X + (child.Width / 2);
        var centerY = child.Y + (child.Height / 2);
        return centerX >= parent.X && centerX <= parent.X + parent.Width
            && centerY >= parent.Y && centerY <= parent.Y + parent.Height;
    }

    private static bool IsCenterInside(Element child, ElementBounds parent)
    {
        var centerX = child.X + (child.Width / 2);
        var centerY = child.Y + (child.Height / 2);
        return centerX >= parent.X && centerX <= parent.X + parent.Width
            && centerY >= parent.Y && centerY <= parent.Y + parent.Height;
    }

    private static bool IsCenterInside(ElementBounds child, ElementBounds parent)
    {
        var centerX = child.X + (child.Width / 2);
        var centerY = child.Y + (child.Height / 2);
        return centerX >= parent.X && centerX <= parent.X + parent.Width
            && centerY >= parent.Y && centerY <= parent.Y + parent.Height;
    }

    private static string DescribeBounds(Element element)
    {
        return $"X={element.X},Y={element.Y},W={element.Width},H={element.Height}";
    }

    private static string DescribeBounds(ElementBounds element)
    {
        return $"Class={element.ClassName},X={element.X},Y={element.Y},W={element.Width},H={element.Height}";
    }

    private static string DescribeBounds(IEnumerable<Element> elements)
    {
        return string.Join("; ", elements.Select(DescribeBounds));
    }

    private static string DescribeBounds(IEnumerable<ElementBounds> elements)
    {
        return string.Join("; ", elements.Select(DescribeBounds));
    }

    private static void ReleaseInteractionState()
    {
        try
        {
            MouseHelper.LeftUp();
        }
        catch
        {
        }

        try
        {
            KeyboardHelper.ReleaseKey(Key.Ctrl);
        }
        catch
        {
        }

        try
        {
            KeyboardHelper.ReleaseKey(Key.Shift);
        }
        catch
        {
        }

        try
        {
            KeyboardHelper.ReleaseKey(Key.Alt);
        }
        catch
        {
        }
    }
}
