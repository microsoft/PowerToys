// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.CmdPal;
using PowerToys.TryRun.Core;
using PowerToys.TryRun.FuzzTests;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class CmdPalTests
{
    [TestMethod]
    public void ProviderExposesSetupAndFileCommandsWithoutExecutingAnything()
    {
        var provider = new TryRunCommandsProvider();
        Assert.AreEqual("com.microsoft.powertoys.tryrun.development", provider.Id);
        var commands = provider.TopLevelCommands();
        Assert.AreEqual(2, commands.Length);
        Assert.AreEqual("Try Run", commands[0].Title);
        Assert.AreEqual("Try Run a file", commands[1].Title);
        var page = (IDynamicListPage)commands[1].Command;
        page.SearchText = "\"C:\\a folder\\脚本.ps1\"";
        Assert.AreEqual("C:\\a folder\\脚本.ps1", page.GetItems().Single().Subtitle);
        page.SearchText = "powershell -Command whoami";
        Assert.AreEqual("Enter one full local file or folder path", page.GetItems().Single().Title);
        page.SearchText = string.Empty;
        Assert.AreEqual("Open Try Run", page.GetItems().Single().Title);
    }

    [TestMethod]
    public void MissingApplicationKeepsCmdPalOpenAndExplainsTheError()
    {
        using var session = new RunSession();
        var command = new OpenTryRunCommand(Path.Combine(session.WorkingDirectory, "PowerToys.TryRun.exe"), []);
        var result = command.Invoke();
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = (IToastArgs)result.Args;
        StringAssert.StartsWith(toast.Message, "Try Run could not open.");
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result.Kind);
    }

    [TestMethod]
    [DataRow("C:\\a folder\\脚本.ps1")]
    [DataRow("C:\\a folder\\data & value.txt")]
    [DataRow("C:\\a folder\\semi;colon.cmd")]
    [DataRow("C:\\a folder\\$literal.exe")]
    public void PathsRoundTripAsData(string path)
    {
        Assert.AreEqual(path, CmdPalLaunch.ParseQuery(path).Single());
        Assert.AreEqual(path, CmdPalLaunch.ParseQuery('"' + path + '"').Single());
        Assert.AreEqual(path, SelectionPayload.Decode(SelectionPayload.Encode(CmdPalLaunch.ParseQuery(path))).Single());
        Assert.AreEqual(0, CmdPalLaunch.ParseQuery("   ").Length);
    }

    [TestMethod]
    [DataRow("powershell -Command whoami")]
    [DataRow("--selection-stdin")]
    [DataRow("https://example.com/app.exe")]
    [DataRow("\\\\server\\share\\app.exe")]
    [DataRow("C:\\app.exe:stream")]
    [DataRow("\"C:\\app.exe\" --run")]
    [DataRow("C:\\first.exe\nC:\\second.exe")]
    [DataRow("C:\\app\0.exe")]
    public void NonPathQueriesAreRejected(string query)
    {
        Assert.ThrowsException<ArgumentException>(() => CmdPalLaunch.ParseQuery(query));
    }

    [TestMethod]
    public void QueryFuzzingIsBoundedAndDoesNotChangePayloadMeaning()
    {
        var seed = Encoding.UTF8.GetBytes("\"C:\\demo folder\\input.ps1\"");
        var random = new Random(196);
        for (var index = 0; index < 2000; index++)
        {
            var data = seed.ToArray();
            data[random.Next(data.Length)] = (byte)random.Next(256);
            CmdPalQueryFuzzer.FuzzTarget(data);
        }

        Assert.ThrowsException<ArgumentException>(() => CmdPalLaunch.ParseQuery(new string('x', 4097)));
        CmdPalQueryFuzzer.FuzzTarget(new byte[20000]);
    }

    [TestMethod]
    public async Task CanceledHandoffNeverStartsAWindow()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => CmdPalLaunch.OpenAsync("C:\\missing\\PowerToys.TryRun.exe", [], canceled.Token));
    }

    [TestMethod]
    [TestCategory("CmdPalIntegration")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HandoffOpensConfigurationWithoutRunningSelectedScript(bool selectFile)
    {
        var application = Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_APP");
        Assert.IsTrue(File.Exists(application), "Set POWERTOYS_TRYRUN_APP to the built Try Run application.");
        using var source = new RunSession();
        var marker = Path.Combine(source.WorkingDirectory, "must-not-execute.txt");
        var script = Path.Combine(source.WorkingDirectory, "脚本 & data.ps1");
        File.WriteAllText(script, $"Set-Content -LiteralPath '{marker}' -Value unexpected");
        var pid = await CmdPalLaunch.OpenAsync(application!, selectFile ? [script] : [], CancellationToken.None);
        using var process = Process.GetProcessById(pid);
        try
        {
            var timeout = Stopwatch.StartNew();
            while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && timeout.Elapsed < TimeSpan.FromSeconds(20))
            {
                await Task.Delay(100);
                process.Refresh();
            }

            Assert.IsFalse(process.HasExited, "Try Run exited during the handoff.");
            Assert.AreNotEqual(IntPtr.Zero, process.MainWindowHandle);
            await Task.Delay(3000);
            Assert.IsFalse(File.Exists(marker), "Selecting a workload from CmdPal must not execute it.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
        }
    }
}
