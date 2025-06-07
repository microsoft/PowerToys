// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesEditorTests : UITestBase
{
    public WorkspacesEditorTests()
        : base(PowerToysModule.Workspaces, WindowSize.Medium)
    {
    }

    [TestMethod("WorkspacesEditor.Items.Present")]
    [TestCategory("Workspaces UI")]
    public void TestItemsPresents()
    {
        Assert.IsTrue(this.Has<Button>("Create Workspace"), "Should have create workspace button");
    }
}
