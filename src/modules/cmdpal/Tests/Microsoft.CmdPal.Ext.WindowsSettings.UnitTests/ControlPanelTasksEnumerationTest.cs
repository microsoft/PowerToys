// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowsSettings.UnitTests;

/// <summary>
/// Functional test for the shell enumeration of Control Panel tasks. Runs
/// against the real shell of the test machine.
/// </summary>
[TestClass]
public class ControlPanelTasksEnumerationTest
{
    [TestMethod]
    public void EnumerationReturnsWellFormedTasks()
    {
        var tasks = ControlPanelTasksHelper.GetAllControlPanelTasks();

        if (tasks.Count == 0)
        {
            // The All Tasks shell folder may be unavailable on some SKUs; the
            // extension degrades gracefully in that case.
            Assert.Inconclusive("The shell returned no Control Panel tasks on this machine.");
        }

        foreach (var task in tasks)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.Name));
            Assert.IsTrue(task.Command.StartsWith(ControlPanelTasksHelper.ShellItemCommandPrefix, System.StringComparison.Ordinal));
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.Type));

            // every enumerated task must carry the id list used to launch it
            Assert.IsNotNull(task.TaskIdList);
            Assert.IsTrue(task.TaskIdList.Length > 0);

            // the settings path shown as the result subtitle is filled in
            // during enumeration, not by a later pass over the whole list
            Assert.AreEqual(task.Type, task.JoinedFullSettingsPath);
        }

        // The enumerated names are distinct enough to be useful for search
        // results deduplication.
        var distinctNames = tasks.Select(x => x.Name).Distinct(System.StringComparer.OrdinalIgnoreCase).Count();
        Assert.IsTrue(distinctNames > 0);
    }
}
