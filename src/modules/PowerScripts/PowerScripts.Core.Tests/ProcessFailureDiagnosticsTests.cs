// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class ProcessFailureDiagnosticsTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("Native standard error")]
    public void PythonFailureRetainsStdoutWithoutReturningItAsScriptOutput(string stderr)
    {
        var result = Normalize(new ProcessRunResult(7, "Native standard output", stderr));

        Assert.AreEqual(7, result.ExitCode);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Text);
        StringAssert.Contains(result.StdErr, "Native standard output");
        if (stderr.Length > 0)
        {
            StringAssert.Contains(result.StdErr, stderr);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WslStartupFailureExplainsVirtualizationAndKeepsNativeDetails(bool managed)
    {
        const string native = "Wsl/Service/CreateInstance/CreateVm/HCS_E_HYPERV_NOT_INSTALLED";
        var result = Normalize(new ProcessRunResult(-1, native, string.Empty), linux: true, managedLinux: managed);

        Assert.AreEqual(-1, result.ExitCode);
        StringAssert.Contains(result.StdErr, managed ? "managed Linux environment could not start" : "WSL2 could not start");
        StringAssert.Contains(result.StdErr, "nested virtualization");
        StringAssert.Contains(result.StdErr, "Details:");
        StringAssert.Contains(result.StdErr, native);
        Assert.IsFalse(result.StdErr.Contains("Azure", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Wsl1PolicyFailureIsNotReportedAsVirtualizationOrImageFailure()
    {
        const string native = "Wsl/Service/WSL_E_WSL1_DISABLED";
        var result = Normalize(new ProcessRunResult(1, native, string.Empty), linux: true);

        StringAssert.Contains(result.StdErr, "blocked by your organization's policy");
        StringAssert.Contains(result.StdErr, native);
        Assert.IsFalse(result.StdErr.Contains("Download image", StringComparison.Ordinal));
        Assert.IsFalse(result.StdErr.Contains("nested virtualization", StringComparison.Ordinal));
    }

    [TestMethod]
    public void NativeWslNulsAreIgnoredForClassificationButPreservedInDetails()
    {
        var native = string.Join("\0", "Wsl/Service/WSL_E_WSL1_DISABLED".ToCharArray());
        var result = Normalize(new ProcessRunResult(1, native, string.Empty), linux: true);

        StringAssert.Contains(result.StdErr, "blocked by your organization's policy");
        StringAssert.Contains(result.StdErr, native);
    }

    [TestMethod]
    [DataRow("WslcPullSessionImage failed: HRESULT 0x80370102", "virtualization_unavailable", "could not start")]
    [DataRow("WslcListSessionImages failed: HRESULT 0x80370102", "virtualization_unavailable", "could not start")]
    [DataRow("WSLC image 'python:test' not found locally.", "image_not_cached", "Download image")]
    public void ManagedPrerequisiteFailuresHaveDistinctStatusesAndGuidance(string native, string status, string guidance)
    {
        var failure = new ProcessRunResult(1, string.Empty, native);

        Assert.AreEqual(status, MxcLinuxRecovery.Status(failure));
        var result = Normalize(failure, linux: true, managedLinux: true);
        StringAssert.Contains(result.StdErr, guidance);
        StringAssert.Contains(result.StdErr, native);
    }

    [TestMethod]
    public void UnknownPythonExceptionsAreNotMisdiagnosedAsMissingImages()
    {
        const string traceback = "Traceback: FileNotFoundError: image not found";
        var failure = new ProcessRunResult(1, string.Empty, traceback);

        Assert.IsNull(ProcessFailureDiagnostics.LinuxStatus(failure));
        Assert.AreEqual(traceback, Normalize(failure, linux: true).StdErr);
    }

    [TestMethod]
    public void WindowsPythonDoesNotApplyLinuxFailureGuidance()
    {
        const string native = "A Windows script printed HCS_E_HYPERV_NOT_INSTALLED";
        var result = Normalize(new ProcessRunResult(1, string.Empty, native));

        Assert.AreEqual(native, result.StdErr);
    }

    private static PythonTransformResult Normalize(ProcessRunResult failure, bool linux = false, bool managedLinux = false) =>
        new PythonRuntime(new PythonSettings()).RunAndParse(
            new PowerScriptManifest { Id = "diagnostic-fixture", FolderPath = "C:\\diagnostic-fixture" },
            new PythonTransformInput(),
            (_, _) => failure,
            translateResultPaths: linux && !managedLinux,
            managedLinux: managedLinux);
}
