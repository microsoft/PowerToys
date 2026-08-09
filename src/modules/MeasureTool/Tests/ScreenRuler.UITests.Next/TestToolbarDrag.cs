// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

/// <summary>
/// Verifies the toolbar's 32x32 drag grip is present, accessible, and actually moves the toolbar
/// when dragged - the "movable toolbar" feature's end-to-end UI coverage. Kept intentionally
/// narrow (a single scoped <c>Find</c> by AutomationId, like <c>SelectToolAndVerify</c>'s button
/// lookups elsewhere in this suite - NOT a full UIA tree walk/`list-windows`, which the rest of
/// this test project explicitly avoids for this same live/frozen overlay because it costs
/// 5-30s on CI or can hang) so it stays reliable in CI.
/// </summary>
[TestClass]
public class TestToolbarDrag : UITestBase
{
    public TestToolbarDrag()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("Toolbar")]
    public void TestDragGripMovesToolbar()
    {
        TestHelper.InitializeTest(this, "toolbar drag test");
        try
        {
            var activationKeys = TestHelper.ReadActivationShortcut(this);
            var ruler = TestHelper.ActivateScreenRuler(this, activationKeys, "toolbar drag test");

            var grip = ruler.Find<Element>(By.AccessibilityId(TestHelper.ToolbarDragHandleId), 8000);
            Assert.IsFalse(grip.IsOffscreen, "The drag grip should be visible on-screen before dragging");

            int startX = grip.X + (grip.Width / 2);
            int startY = grip.Y + (grip.Height / 2);

            // A modest, monitor-safe offset: large enough to be a meaningfully distinct position,
            // small enough to stay within a typical CI display regardless of where the toolbar's
            // configured anchor placed it.
            const int DragDeltaX = 80;
            const int DragDeltaY = 60;

            MouseHelper.Drag(startX, startY, startX + DragDeltaX, startY + DragDeltaY);
            System.Threading.Thread.Sleep(500);

            // Re-query (not reuse the stale `grip`) so the coordinates reflect the toolbar's new,
            // native-move-loop-driven position.
            var movedGrip = ruler.Find<Element>(By.AccessibilityId(TestHelper.ToolbarDragHandleId), 8000);
            Assert.IsFalse(movedGrip.IsOffscreen, "The drag grip should still be visible on-screen after dragging");

            int endX = movedGrip.X + (movedGrip.Width / 2);
            int endY = movedGrip.Y + (movedGrip.Height / 2);

            // Allow generous tolerance (DPI rounding, work-area clamping near an edge) - the point is
            // to confirm the toolbar actually moved with the drag, not to pixel-match the delta.
            const int Tolerance = 20;
            Assert.IsTrue(
                System.Math.Abs((endX - startX) - DragDeltaX) <= Tolerance,
                $"Toolbar should have moved horizontally by ~{DragDeltaX}px; moved by {endX - startX}px instead");
            Assert.IsTrue(
                System.Math.Abs((endY - startY) - DragDeltaY) <= Tolerance,
                $"Toolbar should have moved vertically by ~{DragDeltaY}px; moved by {endY - startY}px instead");

            TestHelper.CloseScreenRulerUI(this);
            Assert.IsTrue(
                TestHelper.WaitForScreenRulerUIToDisappear(this, 2000),
                "ScreenRulerUI should close after calling CloseScreenRulerUI");
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }
}
