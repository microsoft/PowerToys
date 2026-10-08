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
/// no runner behind it (no lifecycle owner), so the port asserts the same thing the original cared
/// about — the module process runs — through the supported path: the runner starts it when enabled.
/// </remarks>
[TestClass]
public class RunFancyZonesTests : UITestBase
{
    public RunFancyZonesTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.UnSpecified, [FancyZonesSettingsSeed.ModuleName])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => FancyZonesTestHelper.StaleProcessNames;

    [TestMethod]
    [TestCategory("FancyZones")]
    public void RunFancyZones()
    {
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForProcess(FancyZonesTestHelper.FancyZonesProcess, true, 30_000),
            $"The runner did not start {FancyZonesTestHelper.FancyZonesProcess}. " +
            $"Live instances: {FancyZonesTestHelper.DescribeProcesses(FancyZonesTestHelper.FancyZonesProcess)}.");
    }
}
