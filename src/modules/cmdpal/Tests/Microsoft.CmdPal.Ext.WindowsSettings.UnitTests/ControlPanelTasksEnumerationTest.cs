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
    /// <summary>
    /// The path delimiter used by <see cref="WindowsSettingsPathHelper"/>.
    /// </summary>
    private const string PathDelimiterSequence = "\u0020\u0020\u02C3\u0020\u0020"; // = "<space><space><arrow><space><space>"

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

            // The settings path shown as the result subtitle (and searched by
            // ">" path queries) must be consistent with the task's areas: with
            // an area it is "<Type>  ˃  <Area>", without it is just the type.
            if (task.Areas is null)
            {
                Assert.AreEqual(task.Type, task.JoinedFullSettingsPath);
            }
            else
            {
                Assert.IsTrue(task.Areas.Count > 0);
                Assert.IsFalse(task.Areas.Any(string.IsNullOrWhiteSpace));
                Assert.AreEqual(
                    $"{task.Type}{PathDelimiterSequence}{string.Join(PathDelimiterSequence, task.Areas)}",
                    task.JoinedFullSettingsPath);
            }

            // alternative names (Control Panel search keywords) are optional,
            // but when present they must be usable for search matching
            if (task.AltNames is not null)
            {
                Assert.IsTrue(task.AltNames.Any());
                Assert.IsFalse(task.AltNames.Any(string.IsNullOrWhiteSpace));
            }
        }

        // The overwhelming majority of task names must be distinct — the
        // merge dedupes by name, so a name collapse (e.g. a localization
        // fallback bug) would silently drop most of the tasks from search.
        var distinctNames = tasks.Select(x => x.Name).Distinct(System.StringComparer.OrdinalIgnoreCase).Count();
        Assert.IsTrue(distinctNames > tasks.Count / 2);

        // Control Panel tasks belong to an applet (e.g. "Devices and
        // Printers") and carry the keywords Control Panel's own search uses.
        // Both are expected on client SKUs, but the extension degrades
        // gracefully without them, so their absence on a reduced shell
        // environment must not fail the suite.
        if (!tasks.Any(x => x.Areas is not null) || !tasks.Any(x => x.AltNames is not null))
        {
            Assert.Inconclusive("The shell provided no category or keyword properties for the enumerated tasks on this machine.");
        }
    }
}
