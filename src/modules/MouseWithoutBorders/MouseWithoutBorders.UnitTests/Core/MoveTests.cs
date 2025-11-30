using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class MoveTests
{
    [TestMethod]
    public void MoveDown_ShouldSwitchFromTopToBottomBasedOnMonitorBounds()
    {
        // Arrange: create a 2x2 matrix (A,B on top row; C,D on bottom row)
        Setting.Values.PauseInstantSaving = true;
        Setting.Values.MatrixOneRow = false; // 2 rows

        MachineStuff.MachinePool.Initialize(new string[] { "A", "B", "C", "D" });
        MachineStuff.MachineMatrix = new string[] { "A", "B", "C", "D" };

        // Assign deterministic IDs so NameFromID/IdFromName work predictably.
        _ = MachineStuff.MachinePool.TryUpdateMachineID("A", (ID)1, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("B", (ID)2, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("C", (ID)3, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("D", (ID)4, true);

        // Make this test environment the controller machine = A (1)
        Setting.Values.MachineId = 1;

        // Stub the monitor bounds for the point so the Move* logic checks the monitor's bounds
        var stubBounds = new Rectangle(0, 0, 1000, 500);
        var original = MachineStuff.GetScreenBoundsFromPoint;
        try
        {
            MachineStuff.GetScreenBoundsFromPoint = p => stubBounds;

            // Act: pointer at the bottom edge of the top-left monitor (A) should trigger MoveDown -> C
            var result = MachineStuff.MoveToMyNeighbourIfNeeded(10, 500, Common.MachineID);

            // Assert: result should be non-empty (indicates a switch) and newDesMachineIdEx should now be C (ID=3)
            Assert.IsFalse(result.IsEmpty, "MoveToMyNeighbourIfNeeded returned an empty point; expected a switch.");
            Assert.AreEqual((ID)3, MachineStuff.newDesMachineIdEx);
        }
        finally
        {
            // restore
            MachineStuff.GetScreenBoundsFromPoint = original;
        }
    }

    [TestMethod]
    public void MoveDown_ShouldTrigger_When_NoMonitorBelowAtCursorX()
    {
        // Arrange: top-row monitors with different heights (no monitors below on this machine at these X positions)
        Setting.Values.PauseInstantSaving = true;
        Setting.Values.MatrixOneRow = false; // 2 rows

        MachineStuff.MachinePool.Initialize(new string[] { "A", "B", "C", "D" });
        MachineStuff.MachineMatrix = new string[] { "A", "B", "C", "D" };
        _ = MachineStuff.MachinePool.TryUpdateMachineID("A", (ID)1, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("B", (ID)2, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("C", (ID)3, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("D", (ID)4, true);
        Setting.Values.MachineId = 1;

        // Current machine has two monitors in top row with different heights and no monitors below them.
        Common.MonitorRects = new System.Collections.Generic.List<MyRectangle>
        {
            new MyRectangle { Left = 0, Top = 0, Right = 1000, Bottom = 500 },
            new MyRectangle { Left = 1000, Top = 0, Right = 2000, Bottom = 300 }
        };

        // Act: pointer at the bottom edge of the left top monitor should trigger MoveDown -> C
        var result = MachineStuff.MoveToMyNeighbourIfNeeded(10, 500, Common.MachineID);

        // Assert
        Assert.IsFalse(result.IsEmpty);
        Assert.AreEqual((ID)3, MachineStuff.newDesMachineIdEx);
    }

    [TestMethod]
    public void MoveDown_ShouldNotTrigger_When_MonitorBelowExistsAtCursorX()
    {
        // Arrange: two stacked monitors so there IS a monitor below at the cursor's X
        Setting.Values.PauseInstantSaving = true;
        Setting.Values.MatrixOneRow = false; // 2 rows

        MachineStuff.MachinePool.Initialize(new string[] { "A", "B", "C", "D" });
        MachineStuff.MachineMatrix = new string[] { "A", "B", "C", "D" };
        _ = MachineStuff.MachinePool.TryUpdateMachineID("A", (ID)1, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("B", (ID)2, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("C", (ID)3, true);
        _ = MachineStuff.MachinePool.TryUpdateMachineID("D", (ID)4, true);
        Setting.Values.MachineId = 1;

        // Two monitors stacked vertically with the same X range
        Common.MonitorRects = new System.Collections.Generic.List<MyRectangle>
        {
            new MyRectangle { Left = 0, Top = 0, Right = 1000, Bottom = 500 },
            new MyRectangle { Left = 0, Top = 500, Right = 1000, Bottom = 1000 }
        };

        // Act: pointer at the bottom edge of the top monitor should NOT trigger MoveDown since there is a monitor below
        var result = MachineStuff.MoveToMyNeighbourIfNeeded(10, 500, Common.MachineID);

        // Assert
        Assert.IsTrue(result.IsEmpty);
    }
}
