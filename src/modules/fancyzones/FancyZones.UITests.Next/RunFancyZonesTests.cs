// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZones.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZones.UITests;

/// <summary>
/// Port of the legacy <c>RunFancyZonesTest</c>.
/// </summary>
/// <remarks>
/// The legacy test started <c>PowerToys.FancyZones.exe</c> directly. A standalone module process has
/// no runner behind it (no keyboard hook, no lifecycle owner), so the port asserts the same thing the
/// original cared about — the module process runs — through the supported path: the runner starts it
/// when the Settings toggle is on, and stops it when the toggle goes off.
/// </remarks>
[TestClass]
public class RunFancyZonesTests : UITestBase
{
    public RunFancyZonesTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.UnSpecified, [FancyZonesSettingsSeed.ModuleName])
    {
    }

    [TestMethod]
    [TestCategory("FancyZones")]
    public void FancyZonesProcessFollowsTheSettingsToggle()
    {
        // This test runs last, so it inherits whatever the preceding 16 tests left behind. Record the
        // starting instances: more than one here means an earlier scope restart orphaned a module
        // process, which would make the "exits when disabled" assertion fail for an unrelated reason.
        FancyZonesTestHelper.Step(
            this,
            $"Live {FancyZonesTestHelper.FancyZonesProcess} instances before touching the toggle: " +
            FancyZonesTestHelper.DescribeProcesses(FancyZonesTestHelper.FancyZonesProcess));

        FancyZonesTestHelper.GoToFancyZonesPage(this);

        FancyZonesTestHelper.SetFancyZonesEnabled(this, true);
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForProcess(FancyZonesTestHelper.FancyZonesProcess, true, 15_000),
            $"{FancyZonesTestHelper.FancyZonesProcess} should be running while the module is enabled.");

        FancyZonesTestHelper.SetFancyZonesEnabled(this, false);
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForProcess(FancyZonesTestHelper.FancyZonesProcess, false, 15_000),
            $"{FancyZonesTestHelper.FancyZonesProcess} should exit once the module is disabled. " +
            $"Live instances: {FancyZonesTestHelper.DescribeProcesses(FancyZonesTestHelper.FancyZonesProcess)}");

        FancyZonesTestHelper.SetFancyZonesEnabled(this, true);
    }
}
