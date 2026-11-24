// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests;

public sealed partial class WorkspacesTests
{
    [TestMethod]
    [DataRow("0", 0, DisplayName = "Busy launcher from the editor button")]
    [DataRow("1", 0, DisplayName = "Busy launcher from a shortcut")]
    [DataRow("2", 5, DisplayName = "Busy launcher during Launch and edit")]
    public void BusyLauncherReturnsTheExpectedExitCode(string invokePoint, int expectedExitCode)
    {
        var project = State.Project("busy-launch", State.Application("Busy launch fixture", State.Prefix + "-busy"));
        State.WriteProjects(project);
        using var busy = new Mutex(false, @"Local\PowerToys_WorkspacesLauncher_InstanceMutex", out var created);
        Assert.IsTrue(created, "A workspace launch was already running before the test.");

        Assert.AreEqual(expectedExitCode, RunLauncher(WorkspaceTestState.Text(project, "id"), invokePoint, 15_000));
    }

    [TestMethod]
    [DataRow("2", false, 1, DisplayName = "Launch and edit when every application fails")]
    [DataRow("2", true, 0, DisplayName = "Launch and edit when one application succeeds")]
    [DataRow("0", false, 0, DisplayName = "Editor button when every application fails")]
    public void CompletedLauncherUsesApplicationOutcomesForRecapture(string invokePoint, bool includeWorkingApp, int expectedExitCode)
    {
        string missingPath = Path.Combine(Path.GetTempPath(), State.Prefix + "-missing.exe");
        Assert.IsFalse(File.Exists(missingPath));
        var missing = State.Application("Missing fixture", State.Prefix + "-missing", path: missingPath);
        var project = includeWorkingApp
            ? State.Project("partial-launch", missing, State.Application("Working fixture", State.Prefix + "-working"))
            : State.Project("failed-launch", missing);
        State.WriteProjects(project);

        Assert.AreEqual(expectedExitCode, RunLauncher(WorkspaceTestState.Text(project, "id"), invokePoint, 60_000));
    }

    [TestMethod]
    [DataRow(false, false, 1, DisplayName = "Missing editor draft does not fall back to the saved workspace")]
    [DataRow(true, false, 1, DisplayName = "A different workspace in the editor draft is rejected")]
    [DataRow(true, true, 5, DisplayName = "The matching editor draft reaches the launch session")]
    public void TaggedLaunchReadsOnlyItsMatchingDraft(bool writeDraft, bool matchingId, int expectedExitCode)
    {
        var saved = State.Project("tagged-launch", State.Application("Draft fixture", State.Prefix + "-draft"));
        State.WriteProjects(saved);
        var token = Guid.NewGuid().ToString("B");
        var path = WorkspaceTestState.TemporaryPath + "." + token.ToUpperInvariant();
        using var preserved = SettingsConfigHelper.PreserveFile(path);
        if (writeDraft)
        {
            var draft = saved.DeepClone().AsObject();
            if (!matchingId)
            {
                draft["id"] = Guid.NewGuid().ToString("B");
            }

            File.WriteAllText(path, draft.ToJsonString());
        }

        using var busy = new Mutex(false, @"Local\PowerToys_WorkspacesLauncher_InstanceMutex", out var created);
        Assert.IsTrue(created, "A workspace launch was already running before the test.");
        Assert.AreEqual(expectedExitCode, RunLauncher(WorkspaceTestState.Text(saved, "id"), "2", 15_000, "--editor-draft=" + token));
    }

    private static int RunLauncher(string workspaceId, string invokePoint, int timeoutMilliseconds, string? draftOption = null)
    {
        var installDirectory = Path.GetDirectoryName(SessionHelper.GetExecutablePath(PowerToysModule.Runner))!;
        var startInfo = new ProcessStartInfo(Path.Combine(installDirectory, "PowerToys.WorkspacesLauncher.exe"));
        startInfo.ArgumentList.Add(workspaceId);
        startInfo.ArgumentList.Add(invokePoint);
        if (draftOption is not null)
        {
            startInfo.ArgumentList.Add(draftOption);
        }

        using var launcher = Process.Start(startInfo)!;
        if (!launcher.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                launcher.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (launcher.HasExited)
            {
                // The launcher exited between the timeout and the termination request.
            }

            Assert.IsTrue(launcher.WaitForExit(10_000), $"The test-owned launcher PID {launcher.Id} survived termination.");
            Assert.Fail($"The workspace launcher did not exit within {timeoutMilliseconds} ms.");
        }

        return launcher.ExitCode;
    }
}
