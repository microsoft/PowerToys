// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

public abstract class FancyZonesEditorTestBase : UITestBase
{
    protected FancyZonesEditorTestBase()
        : base(PowerToysModule.FancyZonesEditor, WindowSize.UnSpecified)
    {
        Files = new FancyZonesEditorFiles();
        EditorTestData.WriteMinimal(Files);
    }

    protected FancyZonesEditorFiles Files { get; }

    protected override void PrepareTestState()
    {
        Assert.IsTrue(
            WindowControl.TryKillProcessTreeByNameAndWait("PowerToys.FancyZonesEditor", 10_000),
            "A stale FancyZones Editor process was still running before fixture restaging.");
        Files.RestageAll();
    }

    [TestCleanup]
    public async Task CleanupEditorTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        WindowControl.TryKillProcessTreeByNameAndWait("PowerToys.FancyZonesEditor", 10_000);
        Files.RestoreAll();
    }
}