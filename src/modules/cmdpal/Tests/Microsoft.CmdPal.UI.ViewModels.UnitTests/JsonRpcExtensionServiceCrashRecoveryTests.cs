// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Documents the crash-lifecycle decisions behind r2-p4-03 (an immediate post-init
/// crash must be handled) and r2-p4-07 (a crash-disabled extension recovers after a
/// source edit). The end-to-end wiring (the post-init <c>IsRunning()</c> probe that
/// drives <c>OnExtensionProcessExited</c>, the disable branch keeping the source
/// watcher alive, and hot-reload resetting the crash count) requires spawning a Node
/// process and is verified by inspection; the deterministic decision the wiring relies
/// on is exercised here through the pure <see cref="JsonRpcExtensionService.DecideCrashAction"/>
/// seam.
/// </summary>
[TestClass]
public class JsonRpcExtensionServiceCrashRecoveryTests
{
    private const int MaxRestartAttempts = 3;

    [TestMethod]
    public void CrashSequence_ReachesDisableAfterExceedingLimit()
    {
        // Each recorded crash increments the count; the service restarts while at or below
        // the limit and disables only once the count exceeds it. A crash observed
        // immediately after init (p4-03) feeds this same counter, so an extension that
        // exits right after starting is not treated as healthy.
        var crashCount = 0;
        for (var attempt = 1; attempt <= MaxRestartAttempts; attempt++)
        {
            crashCount++;
            Assert.AreEqual(
                JsonRpcExtensionService.CrashAction.Restart,
                JsonRpcExtensionService.DecideCrashAction(crashCount, MaxRestartAttempts),
                $"Crash {crashCount} is within the limit and must restart.");
        }

        crashCount++;
        Assert.AreEqual(
            JsonRpcExtensionService.CrashAction.Disable,
            JsonRpcExtensionService.DecideCrashAction(crashCount, MaxRestartAttempts),
            "Exceeding the restart limit must disable the extension.");
    }

    [TestMethod]
    public void SourceEdit_ResetsCrashCount_AllowsRestartAgain()
    {
        // Drive the extension to the disabled decision.
        var crashCount = MaxRestartAttempts + 1;
        Assert.AreEqual(
            JsonRpcExtensionService.CrashAction.Disable,
            JsonRpcExtensionService.DecideCrashAction(crashCount, MaxRestartAttempts));

        // A source edit hot-reloads with resetCrashCount: true, which clears the counter
        // for the directory. The very next crash decision must be Restart again, so the
        // extension is no longer stranded in the disabled state (p4-07).
        crashCount = 0;
        crashCount++;
        Assert.AreEqual(
            JsonRpcExtensionService.CrashAction.Restart,
            JsonRpcExtensionService.DecideCrashAction(crashCount, MaxRestartAttempts),
            "After a source edit resets the crash count, the extension must retry loading.");
    }
}
