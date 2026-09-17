// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SandboxStartupErrorTests
{
    [DataTestMethod]
    [DataRow("WindowsSandboxClient.exe", "#32770")]
    [DataRow("WindowsSandbox.exe", "WindowsForms10.Window")]
    [DataRow("Unrelated.exe", "#32770")]
    public void OnlyLauncherErrorDialogsAreRead(string executable, string windowClass)
    {
        var message = SandboxStartupError.Read(
            executable,
            windowClass,
            () => throw new InvalidOperationException("A viewer/provider must not be queried."));

        Assert.IsNull(message);
    }

    [TestMethod]
    public void GenericDialogOrErrorTextDoesNotProveStartupFailure()
    {
        Assert.IsNull(SandboxStartupError.Read(
            "WindowsSandbox.exe",
            "#32770",
            () => ["Windows Sandbox", "Error 0x800705B4", "Would you like to close the sandbox?"]));
    }

    [TestMethod]
    public void NativeStartupFailureKeepsItsErrorCode()
    {
        var message = SandboxStartupError.Read(
            "WindowsSandbox.exe",
            "#32770",
            () => ["Windows Sandbox failed to start.", "Error 0x800705B4. The timeout period expired."]);

        Assert.AreEqual("Windows Sandbox failed to start (0x800705B4).", message);
    }

    [TestMethod]
    public void NativeStartupFailureWithoutACodeStillFails()
    {
        Assert.AreEqual(
            "Windows Sandbox failed to start.",
            SandboxStartupError.Read("WindowsSandbox.exe", "#32770", () => ["Windows Sandbox failed to start."]));
    }
}
