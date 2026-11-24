// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class ClipboardHelperPipeTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MatchedAppAndHelperExchangeSyntheticData(bool startClientBeforeServer)
    {
        var result = await RunPairAsync("PowerToys.MouseWithoutBorders.exe", "PowerToys.MouseWithoutBordersHelper.exe", startClientBeforeServer: startClientBeforeServer);

        Assert.AreEqual(0, result.ServerExit, result.ServerOutput);
        Assert.AreEqual(0, result.ClientExit, result.ClientOutput);
        StringAssert.Contains(result.ServerOutput, "accepted;calls:1");
        StringAssert.Contains(result.ClientOutput, "synthetic-handoff");
    }

    [DataTestMethod]
    [DataRow("OtherHelper.exe", null, null, "wrong-image")]
    [DataRow("PowerToys.MouseWithoutBordersHelper.exe", "S-1-5-18", null, "wrong-user")]
    [DataRow("PowerToys.MouseWithoutBordersHelper.exe", null, "-1", "wrong-session")]
    public async Task AppRejectsOtherParticipantsBeforeDispatch(string helperName, string? user, string? session, string reason)
    {
        using var identity = WindowsIdentity.GetCurrent();
        Assert.IsFalse(identity.IsSystem, "The disposable fixture must run as a normal user.");
        var result = await RunPairAsync("PowerToys.MouseWithoutBorders.exe", helperName, user, session);

        Assert.AreEqual(3, result.ServerExit, result.ServerOutput);
        StringAssert.Contains(result.ServerOutput, $"rejected:{reason};calls:0");
        Assert.AreNotEqual(0, result.ClientExit, result.ClientOutput);
    }

    [TestMethod]
    public async Task HelperRejectsOtherServerBeforeSendingData()
    {
        var result = await RunPairAsync("OtherApp.exe", "PowerToys.MouseWithoutBordersHelper.exe", capture: true);

        Assert.AreNotEqual(0, result.ClientExit, result.ClientOutput);
        StringAssert.Contains(result.ClientOutput, "connection rejected: wrong-image");
        Assert.AreEqual(0, result.ServerExit, result.ServerOutput);
        StringAssert.Contains(result.ServerOutput, "bytes:0");
    }

    [TestMethod]
    public async Task ExistingPipeInstanceCannotBeReplaced()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "ClipboardHelper.PipeTestHost", "ClipboardHelper.PipeTestHost.exe");
        string pipe = $"PowerToys.ClipboardFixture.{Guid.NewGuid():N}";
        using var server = StartFixture(executable, "capture", pipe);
        Process? secondServer = null;
        try
        {
            Assert.AreEqual("ready", await server.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            secondServer = StartFixture(executable, "capture", pipe);
            var secondOutput = secondServer.StandardOutput.ReadToEndAsync();
            await secondServer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(4, secondServer.ExitCode, await secondOutput);
            StringAssert.Contains(await secondOutput, "Win32Exception");

            using (var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await client.ConnectAsync(5000);
            }

            await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(0, server.ExitCode, await server.StandardOutput.ReadToEndAsync());
        }
        finally
        {
            StopFixture(secondServer);
            StopFixture(server);
            secondServer?.Dispose();
        }
    }

    private static async Task<(int ServerExit, string ServerOutput, int ClientExit, string ClientOutput)> RunPairAsync(
        string serverName,
        string clientName,
        string? user = null,
        string? session = null,
        bool capture = false,
        bool startClientBeforeServer = false)
    {
        string fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "ClipboardHelper.PipeTestHost");
        string executable = Path.Combine(fixtureDirectory, "ClipboardHelper.PipeTestHost.exe");
        string serverExecutable = Path.Combine(fixtureDirectory, serverName);
        string clientExecutable = Path.Combine(fixtureDirectory, clientName);
        File.Copy(executable, serverExecutable, true);
        File.Copy(executable, clientExecutable, true);
        string pipe = $"PowerToys.ClipboardFixture.{Guid.NewGuid():N}";
        using var identity = WindowsIdentity.GetCurrent();
        using var currentProcess = Process.GetCurrentProcess();
        Process? server = null;
        Process? client = null;
        try
        {
            if (startClientBeforeServer)
            {
                client = StartFixture(clientExecutable, "client", pipe);
                Assert.AreEqual("connecting", await client.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
                await Task.Delay(TimeSpan.FromSeconds(6));
            }

            server = StartFixture(
                serverExecutable,
                capture ? "capture" : "server",
                pipe,
                user ?? identity.User!.Value,
                session ?? currentProcess.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("ready", await server.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            client ??= StartFixture(clientExecutable, "client", pipe);
            var clientOutput = client.StandardOutput.ReadToEndAsync();
            var serverOutput = server.StandardOutput.ReadToEndAsync();
            await Task.WhenAll(server.WaitForExitAsync(), client.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(20));
            return (server.ExitCode, await serverOutput, client.ExitCode, await clientOutput);
        }
        finally
        {
            StopFixture(client);
            StopFixture(server);
            client?.Dispose();
            server?.Dispose();
            File.Delete(serverExecutable);
            File.Delete(clientExecutable);
        }
    }

    private static Process StartFixture(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("The fixture process did not start.");
    }

    private static void StopFixture(Process? process)
    {
        if (process != null && !process.HasExited)
        {
            _ = process.CloseMainWindow();
            if (!process.WaitForExit(1000))
            {
                process.Kill();
                Assert.IsTrue(process.WaitForExit(5000), $"Fixture PID {process.Id} did not exit.");
            }
        }
    }
}
