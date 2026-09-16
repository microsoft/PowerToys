// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Launching;

namespace Microsoft.CmdPal.TryRun.UnitTests;

[TestClass]
public sealed class TryRunCommandTests
{
    [TestMethod]
    [DataRow("C:\\Apps\\editor.exe")]
    [DataRow("C:\\Apps\\run.PS1")]
    [DataRow("C:\\Apps\\run.cmd")]
    [DataRow("C:\\Apps\\run.bat")]
    public void ApplicationResultsOfferConfiguration(string path)
    {
        var action = OpenInTryRunCommand.ForApplication(path, "--file \"C:\\data & notes.txt\"");
        Assert.IsNotNull(action);
        Assert.IsInstanceOfType<OpenInTryRunCommand>(action.Command);
        Assert.AreEqual("com.microsoft.powertoys.tryrun.selection", action.Command.Id);
    }

    [TestMethod]
    [DataRow("C:\\Apps\\editor.lnk")]
    [DataRow("C:\\Apps\\setup.msi")]
    [DataRow("shell:AppsFolder\\App!Id")]
    [DataRow("https://example.com/app.exe")]
    [DataRow("\\\\server\\share\\app.exe")]
    [DataRow("C:\\app.exe:stream")]
    [DataRow("C:app.exe")]
    public void UnsupportedApplicationsCannotFallBackToHostLaunch(string path)
    {
        Assert.IsNull(OpenInTryRunCommand.ForApplication(path, string.Empty));
    }

    [TestMethod]
    [DataRow("C:\\Data\\report.docx")]
    [DataRow("C:\\Data\\folder")]
    [DataRow("C:\\Data\\脚本 & notes.ps1")]
    public void FilesAndFoldersOfferConfigurationWithoutReadingFiles(string path)
    {
        Assert.IsNotNull(OpenInTryRunCommand.ForFile(path));
        var selection = CmdPalHandoff.CreateSelection(path);
        Assert.AreEqual(path, CmdPalHandoff.Decode(CmdPalHandoff.Encode(selection)).Path);
    }

    [TestMethod]
    public void ShortcutArgumentsPreserveQuotesUnicodeMetacharactersAndEmptyValues()
    {
        var selection = CmdPalHandoff.CreateSelection(@"C:\Apps\editor.exe", "--file \"C:\\目录\\data & notes.txt\" \"\" \"a\\\"b\" ;whoami $(literal)");
        string[] expected = ["--file", @"C:\目录\data & notes.txt", string.Empty, "a\"b", ";whoami", "$(literal)"];
        CollectionAssert.AreEqual(expected, selection.Arguments);
        CollectionAssert.AreEqual(expected, CmdPalHandoff.Decode(CmdPalHandoff.Encode(selection)).Arguments);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"Version\":2,\"Path\":\"C:\\\\app.exe\",\"Arguments\":[]}")]
    [DataRow("{\"Version\":\"1\",\"Path\":\"C:\\\\app.exe\",\"Arguments\":[]}")]
    [DataRow("{\"Version\":1,\"Path\":\"--run\",\"Arguments\":[]}")]
    [DataRow("{\"Version\":1,\"Path\":\"C:\\\\app.exe\",\"Arguments\":[null]}")]
    [DataRow("{\"Version\":1,\"Path\":\"C:\\\\app.exe\",\"Arguments\":[\"\\uDE94\"]}")]
    public void InvalidMessagesAreRejected(string json)
    {
        Assert.ThrowsException<ArgumentException>(() => CmdPalHandoff.Decode(json));
    }

    [TestMethod]
    public async Task HandoffLimitsAndCancellationAreEnforced()
    {
        Assert.ThrowsException<ArgumentException>(() => CmdPalHandoff.Encode(new(@"C:\app.exe", new string[65])));
        Assert.ThrowsException<ArgumentException>(() => CmdPalHandoff.CreateSelection(@"C:\app.exe", new string('x', 8193)));
        Assert.ThrowsException<ArgumentException>(() => CmdPalHandoff.CreateSelection(@"C:\app.exe", "first\nsecond"));
        Assert.ThrowsException<ArgumentException>(() => CmdPalHandoff.Encode(new(@"C:\app.exe", [new string('x', 4097)])));
        using var reader = new StringReader(new string('x', CmdPalHandoff.MaximumCharacters + 1));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => CmdPalHandoff.ReadAsync(reader, CancellationToken.None));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => CmdPalHandoff.OpenAsync(@"C:\missing.exe", new(@"C:\app.exe", []), new CancellationToken(true)));
    }

    [TestMethod]
    public void DiscoveryUsesSiblingBuildAndRejectsInvalidOverride()
    {
        var root = Path.Combine(Path.GetTempPath(), "CmdPal-TryRun-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "WinUI3Apps", "CmdPal"));
        var tryRun = Directory.CreateDirectory(Path.Combine(root, "TryRun-Policies")).FullName;
        var application = Path.Combine(tryRun, CmdPalHandoff.ApplicationName);
        try
        {
            File.WriteAllText(application, "Discovery fixture, never executed.");
            Assert.AreEqual(application, CmdPalHandoff.FindApplication(Path.Combine(root, "WinUI3Apps", "CmdPal"), null));
            Assert.ThrowsException<FileNotFoundException>(() => CmdPalHandoff.FindApplication(root, @"C:\missing\PowerToys.TryRun.exe"));
            Assert.ThrowsException<FileNotFoundException>(() => CmdPalHandoff.FindApplication(root, @"C:\Windows\System32\cmd.exe"));
            Assert.ThrowsException<FileNotFoundException>(() => CmdPalHandoff.FindApplication(tryRun, "PowerToys.TryRun.exe"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void FailedHandoffKeepsCmdPalOpenAndHasLocalizedActionName()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var command = new OpenInTryRunCommand("--invalid");
            Assert.AreEqual("在 Try Run 中打开", command.Name);
            var result = command.Invoke();
            Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
            var toast = (IToastArgs)result.Args;
            Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result.Kind);
            StringAssert.StartsWith(toast.Message, "无法打开 Try Run。");
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
    }

    [TestMethod]
    [TestCategory("CmdPalIntegration")]
    public async Task NativeContextCommandOpensSetupWithoutExecutingSelectedScript()
    {
        var application = Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_APP");
        Assert.IsTrue(File.Exists(application), "Set POWERTOYS_TRYRUN_APP to the built Try Run application.");
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CmdPal-TryRun-" + Guid.NewGuid().ToString("N"))).FullName;
        var script = Path.Combine(root, "脚本 & demo.ps1");
        var marker = Path.Combine(root, "must-not-execute.txt");
        var before = Process.GetProcessesByName("PowerToys.TryRun").Select(process =>
        {
            using (process)
            {
                return process.Id;
            }
        }).ToHashSet();
        Process? window = null;
        try
        {
            File.WriteAllText(script, $"Set-Content -LiteralPath '{marker}' -Value unexpected");
            var result = new OpenInTryRunCommand(script).Invoke();
            Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(20))
            {
                foreach (var process in Process.GetProcessesByName("PowerToys.TryRun"))
                {
                    if (!before.Contains(process.Id) && process.MainWindowHandle != IntPtr.Zero)
                    {
                        window = process;
                        break;
                    }

                    process.Dispose();
                }

                if (window is not null)
                {
                    break;
                }

                await Task.Delay(100);
            }

            Assert.IsNotNull(window, "The command did not open the Try Run window.");
            await Task.Delay(2000);
            Assert.IsFalse(File.Exists(marker), "Selecting an action must not run the workload.");
        }
        finally
        {
            if (window is not null)
            {
                using (window)
                {
                    window.CloseMainWindow();
                    if (!window.WaitForExit(10000))
                    {
                        window.Kill(true);
                        await window.WaitForExitAsync();
                    }
                }
            }

            Directory.Delete(root, true);
        }
    }
}
