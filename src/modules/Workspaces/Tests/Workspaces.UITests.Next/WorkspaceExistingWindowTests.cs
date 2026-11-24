// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests;

[TestClass]
[TestCategory("Workspaces")]
public sealed class WorkspaceExistingWindowTests
{
    [TestMethod]
    public async Task FailedMoveCanRecaptureOnlyItsLiveExistingWindow()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "NativeFixture", "WorkspacesCliRuntimeFixture.exe");
        Assert.IsTrue(File.Exists(executable), $"Missing native desktop fixture: {executable}");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("existing-window-recapture");

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The fixture exited between the timeout and the termination request.
            }

            Assert.IsTrue(process.WaitForExit(10_000), $"Could not stop the timed-out fixture (PID {process.Id}).");
            Assert.Fail($"The existing-window fixture timed out. {await stdout} {await stderr}");
        }

        Assert.AreEqual(0, process.ExitCode, $"{await stdout} {await stderr}");
    }
}
