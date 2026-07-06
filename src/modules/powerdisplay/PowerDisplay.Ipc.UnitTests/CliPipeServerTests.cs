// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Ipc;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Unit tests for <see cref="CliPipeServer.ReadBoundedLineAsync"/>, the length-bounded line reader
/// that protects the single-threaded accept loop from oversized / never-terminated requests.
/// </summary>
[TestClass]
public class CliPipeServerTests
{
    private static Task<string?> Read(string input, int maxChars = CliPipeProtocolMax)
        => CliPipeServer.ReadBoundedLineAsync(new StringReader(input), maxChars, CancellationToken.None);

    private const int CliPipeProtocolMax = 1024;

    [TestMethod]
    public async Task ReadBoundedLine_NewlineTerminated_ReturnsLineWithoutTerminator()
    {
        var line = await Read("{\"command\":\"list\"}\n");
        Assert.AreEqual("{\"command\":\"list\"}", line);
    }

    [TestMethod]
    public async Task ReadBoundedLine_CrlfTerminated_StripsCarriageReturn()
    {
        // The client writes via StreamWriter.WriteLineAsync (NewLine = "\r\n" on Windows).
        var line = await Read("payload\r\n");
        Assert.AreEqual("payload", line);
    }

    [TestMethod]
    public async Task ReadBoundedLine_StopsAtFirstNewline()
    {
        var line = await Read("first\nsecond\n");
        Assert.AreEqual("first", line);
    }

    [TestMethod]
    public async Task ReadBoundedLine_EmptyStream_ReturnsNull()
    {
        var line = await Read(string.Empty);
        Assert.IsNull(line);
    }

    [TestMethod]
    public async Task ReadBoundedLine_UnterminatedTail_ReturnsTail()
    {
        var line = await Read("no-newline");
        Assert.AreEqual("no-newline", line);
    }

    [TestMethod]
    public async Task ReadBoundedLine_AtExactlyMaxChars_IsAllowed()
    {
        var line = await Read("abcde\n", maxChars: 5);
        Assert.AreEqual("abcde", line);
    }

    [TestMethod]
    public async Task ReadBoundedLine_OverMaxChars_Throws()
    {
        await Assert.ThrowsExceptionAsync<InvalidDataException>(
            () => Read("abcdef\n", maxChars: 5));
    }

    [TestMethod]
    public async Task ReadBoundedLine_AlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<System.OperationCanceledException>(
            () => CliPipeServer.ReadBoundedLineAsync(new StringReader("x\n"), 1024, cts.Token));
    }

    // ─── CreateServerStream ownership mechanic ────────────────────────────────
    // These cover the property the gap-free accept loop relies on: a first instance asserts ownership
    // (FirstPipeInstance), a second *overlapping* instance can be created while the first is still
    // alive (so the loop can stand up the replacement before disposing the served one, never releasing
    // the well-known name), and a second first-instance create is rejected while one is alive (which is
    // what surfaces a pre-existing squatter at startup).
    private static PipeSecurity CurrentUserPipeSecurity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var ownerSid = identity.User ?? new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            ownerSid,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        return security;
    }

    private static string UniquePipeName() => $"PowerDisplay_Cli_Test_{Guid.NewGuid():N}";

    [TestMethod]
    public void CreateServerStream_FirstInstance_Succeeds()
    {
        using var server = CliPipeServer.CreateServerStream(UniquePipeName(), CurrentUserPipeSecurity(), firstInstance: true);
        Assert.IsNotNull(server);
    }

    [TestMethod]
    public void CreateServerStream_OverlappingSecondInstance_Succeeds()
    {
        // The gap-free accept loop creates the replacement instance while the connected one is still
        // alive; that overlap MUST be allowed so the well-known name is never released between requests.
        var name = UniquePipeName();
        var security = CurrentUserPipeSecurity();
        using var first = CliPipeServer.CreateServerStream(name, security, firstInstance: true);
        using var second = CliPipeServer.CreateServerStream(name, security, firstInstance: false);
        Assert.IsNotNull(second);
    }

    [TestMethod]
    public void CreateServerStream_SecondFirstInstance_WhileOneAlive_Throws()
    {
        // FirstPipeInstance must reject a create when an instance of this name already exists — this is
        // exactly what surfaces a pre-existing squatter at startup.
        var name = UniquePipeName();
        var security = CurrentUserPipeSecurity();
        using var first = CliPipeServer.CreateServerStream(name, security, firstInstance: true);

        NamedPipeServerStream? second = null;
        try
        {
            second = CliPipeServer.CreateServerStream(name, security, firstInstance: true);
            Assert.Fail("A second FirstPipeInstance create must fail while an instance is already alive.");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            second?.Dispose();
        }
    }
}
