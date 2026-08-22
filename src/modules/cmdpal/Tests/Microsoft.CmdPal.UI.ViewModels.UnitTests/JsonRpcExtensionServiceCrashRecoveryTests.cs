// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Documents the crash lifecycle decisions behind r2-p4-03 (handle a crash right after
/// init) and r2-p4-07 (recover a disabled extension after a source edit).
/// The full wiring needs a Node process, so these tests cover the pure
/// <see cref="JsonRpcExtensionService.DecideCrashAction"/> decision the service depends on.
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
