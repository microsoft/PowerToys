// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using LightSwitch.Cli.Ipc;
using LightSwitch.Cli.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LightSwitch.Cli.UnitTests;

[TestClass]
public sealed class CliPipeClientTests
{
    private const string Request = "{\"version\":1,\"command\":\"status\"}";

    private static readonly byte[] InvalidUtf16Line = { 0x00, 0xD8, 0x0A, 0x00 };

    [TestMethod]
    [Timeout(10000)]
    public async Task ResponseSplitAtEveryBytePreservesUtf16AndTheRequestHasNoBom()
    {
        string name = NewPipeName();
        using var server = CreateServer(name);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        const string Response = "{\"version\":1,\"success\":false,\"error\":{\"code\":\"EXECUTION_FAILED\",\"message\":\"主题 🌙\"}}";
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellation.Token);
            string? actualRequest = await ReadRequestAsync(server, cancellation.Token);
            Assert.AreEqual(Request, actualRequest, "A BOM or altered framing would change the request.");
            byte[] bytes = CliProtocol.Encoding.GetBytes(Response + "\n");
            for (int i = 0; i < bytes.Length; i++)
            {
                await server.WriteAsync(bytes.AsMemory(i, 1), cancellation.Token);
            }

            await WaitForClientCloseAsync(server, cancellation.Token);
        });

        var client = CreateClient(name);
        Assert.AreEqual(Response, await client.SendAsync(Request, cancellation.Token));
        await serverTask;
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task UntrustedServerReceivesNoRequestBytes()
    {
        string name = NewPipeName();
        using var server = CreateServer(name);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellation.Token);
            await WaitForClientCloseAsync(server, cancellation.Token);
        });

        var client = new CliPipeClient(name, static _ => false, TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsExceptionAsync<CliException>(() => client.SendAsync(Request, cancellation.Token));
        Assert.AreEqual("SERVICE_UNAVAILABLE", error.Code);
        await serverTask;
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task MissingServiceIsUnavailableWithinTheConnectDeadline()
    {
        var client = new CliPipeClient(NewPipeName(), static _ => true, TimeSpan.FromMilliseconds(100));
        var error = await Assert.ThrowsExceptionAsync<CliException>(() => client.SendAsync(Request, CancellationToken.None));
        Assert.AreEqual("SERVICE_UNAVAILABLE", error.Code);
    }

    [TestMethod]
    [Timeout(10000)]
    [DataRow("")]
    [DataRow("{\"version\":1,\"success\":false}")]
    public async Task DisconnectAfterRequestIsAnUnknownOutcomeEvenForParseableTail(string incompleteResponse)
    {
        string name = NewPipeName();
        using var server = CreateServer(name);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellation.Token);
            Assert.AreEqual(Request, await ReadRequestAsync(server, cancellation.Token));
            if (incompleteResponse.Length > 0)
            {
                await server.WriteAsync(CliProtocol.Encoding.GetBytes(incompleteResponse), cancellation.Token);
            }

            server.Dispose();
        });

        var error = await Assert.ThrowsExceptionAsync<CliException>(() => CreateClient(name).SendAsync(Request, cancellation.Token));
        Assert.AreEqual("TIMEOUT", error.Code);
        StringAssert.Contains(error.Message, "may already have taken effect");
        await serverTask;
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task InvalidUtf16IsAProtocolError()
    {
        string name = NewPipeName();
        using var server = CreateServer(name);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellation.Token);
            _ = await ReadRequestAsync(server, cancellation.Token);
            await server.WriteAsync(InvalidUtf16Line, cancellation.Token);
            await WaitForClientCloseAsync(server, cancellation.Token);
        });

        var error = await Assert.ThrowsExceptionAsync<CliException>(() => CreateClient(name).SendAsync(Request, cancellation.Token));
        Assert.AreEqual("PROTOCOL_ERROR", error.Code);
        await serverTask;
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task CancellingAConnectedRequestClosesTheConnection()
    {
        string name = NewPipeName();
        using var server = CreateServer(name);
        using var serverDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var clientCancellation = new CancellationTokenSource();
        var requestRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(serverDeadline.Token);
            _ = await ReadRequestAsync(server, serverDeadline.Token);
            requestRead.SetResult();
            await WaitForClientCloseAsync(server, serverDeadline.Token);
        });

        var exchange = CreateClient(name).SendAsync(Request, clientCancellation.Token);
        await requestRead.Task.WaitAsync(serverDeadline.Token);
        clientCancellation.Cancel();
        try
        {
            await exchange;
            Assert.Fail("The connected read should have been cancelled.");
        }
        catch (OperationCanceledException)
        {
        }

        await serverTask;
    }

    [TestMethod]
    public async Task BoundedReaderAcceptsTheLimitAndCrLf()
    {
        string text = new('a', CliProtocol.MaxMessageChars);
        using var reader = new StringReader(text + "\r\n");
        Assert.AreEqual(text, await CliPipeClient.ReadResponseLineAsync(reader, CancellationToken.None));
    }

    [TestMethod]
    public async Task BoundedReaderRejectsMoreThanTheLimit()
    {
        using var reader = new StringReader(new string('a', CliProtocol.MaxMessageChars + 1) + "\n");
        var error = await Assert.ThrowsExceptionAsync<CliException>(() => CliPipeClient.ReadResponseLineAsync(reader, CancellationToken.None));
        Assert.AreEqual("PROTOCOL_ERROR", error.Code);
    }

    [TestMethod]
    public async Task BoundedReaderDoesNotDiscardEmbeddedCarriageReturns()
    {
        using var reader = new StringReader("a\rb\n");
        Assert.AreEqual("a\rb", await CliPipeClient.ReadResponseLineAsync(reader, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("C:\\PowerToys\\LightSwitchService\\PowerToys.LightSwitchService.exe", "c:\\powertoys\\LightSwitchService\\PowerToys.LightSwitchService.exe", true)]
    [DataRow("C:\\Other\\LightSwitchService\\PowerToys.LightSwitchService.exe", "C:\\PowerToys\\LightSwitchService\\PowerToys.LightSwitchService.exe", false)]
    [DataRow("C:\\PowerToys\\LightSwitchService\\Other.exe", "C:\\PowerToys\\LightSwitchService\\PowerToys.LightSwitchService.exe", false)]
    public void ServerIdentityRequiresTheExactInstalledExecutable(string actualPath, string expectedPath, bool expected)
    {
        Assert.AreEqual(expected, PipeServerIdentity.PathsMatch(actualPath, expectedPath));
    }

    [TestMethod]
    public void ServerIdentityUsesTheServiceSubfolderOfTheInstallationRoot()
    {
        Assert.AreEqual(Path.Combine(AppContext.BaseDirectory, "LightSwitchService", "PowerToys.LightSwitchService.exe"), PipeServerIdentity.ExpectedServerPath);
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task ServerIdentityChecksTheProcessAtTheOtherEndOfThePipe()
    {
        string name = NewPipeName();
        using var server = CreateServer(name);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task accepted = server.WaitForConnectionAsync(cancellation.Token);
        await client.ConnectAsync(cancellation.Token);
        await accepted;
        string? imagePath = Environment.ProcessPath;
        Assert.IsNotNull(imagePath);
        Assert.IsTrue(PipeServerIdentity.IsTrustedServer(client, imagePath));
        Assert.IsFalse(PipeServerIdentity.IsTrustedServer(client, Path.Combine(Path.GetTempPath(), "Other.exe")));
    }

    private static string NewPipeName() => "LightSwitch_Cli_Test_" + Guid.NewGuid().ToString("N");

    private static NamedPipeServerStream CreateServer(string name)
        => new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private static CliPipeClient CreateClient(string name)
        => new(name, static _ => true, TimeSpan.FromSeconds(2));

    private static async Task<string?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, CliProtocol.Encoding, false, CliProtocol.BufferSize, leaveOpen: true);
        return await reader.ReadLineAsync(cancellationToken);
    }

    private static async Task WaitForClientCloseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        Assert.AreEqual(0, await stream.ReadAsync(buffer, cancellationToken));
    }
}
