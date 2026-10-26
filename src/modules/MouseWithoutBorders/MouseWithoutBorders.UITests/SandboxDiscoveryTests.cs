// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseWithoutBorders.UITests;

[TestClass]
public sealed class SandboxDiscoveryTests
{
    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void NativeStartupErrorTextDoesNotRequireUiAutomation()
    {
        var title = "MWB native dialog " + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
            $"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('Windows Sandbox failed to start. Error 0x800705B4.', '{title}') | Out-Null",
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Native dialog helper did not start.");
        IntPtr window = IntPtr.Zero;
        try
        {
            RunFiles.Wait(
                () =>
                {
                    window = WindowControl.EnumerateProcessWindows([process.Id])
                        .Where(candidate => candidate.IsVisible && candidate.ClassName == "#32770" && candidate.Title == title)
                        .Select(candidate => candidate.Hwnd).FirstOrDefault();
                    return window != IntPtr.Zero;
                },
                TimeSpan.FromSeconds(20),
                "The owned native dialog did not appear.");

            var text = NativeSupport.DialogStaticText(window, process.Id);
            Assert.AreEqual(
                "Windows Sandbox failed to start (0x800705B4).",
                SandboxStartupError.Read("WindowsSandbox.exe", "#32770", () => text));
        }
        finally
        {
            if (window != IntPtr.Zero)
            {
                NativeSupport.PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            }

            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(5000), "The owned native dialog helper did not stop.");
            }
        }
    }
}
