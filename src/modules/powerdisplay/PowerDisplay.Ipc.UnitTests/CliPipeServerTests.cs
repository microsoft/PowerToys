// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

    private static readonly string[] DelayThenCreateOrder = { "delay", "create" };

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

    // ─── CreateReplacementWithRetryAsync ───────────────────────────────────────
    // The accept loop must not drop the in-flight request or dispose the connected instance just
    // because the FIRST replacement-creation attempt (the fast path, tried before serving) throws.
    // It must instead retry, with a bounded delay awaited before EVERY attempt (including the first
    // one made inside this helper — the fast path already consumed attempt #1), until a replacement
    // is created or the caller's token is cancelled (app shutdown) — never spinning without delaying,
    // and never delaying-then-attempting out of order.
    [TestMethod]
    public async Task CreateReplacementWithRetryAsync_FirstAttemptThrows_RetriesAndReturnsReplacement()
    {
        var attempts = 0;
        NamedPipeServerStream? created = null;
        var name = UniquePipeName();
        var security = CurrentUserPipeSecurity();

        NamedPipeServerStream Factory()
        {
            attempts++;
            if (attempts == 1)
            {
                throw new IOException("simulated first-attempt replacement-creation failure");
            }

            created = CliPipeServer.CreateServerStream(name, security, firstInstance: true);
            return created;
        }

        var delayCalls = 0;
        Task Delay(CancellationToken ct)
        {
            delayCalls++;

            // No-op delay: the test proves retry behaviour, not real timing.
            return Task.CompletedTask;
        }

        try
        {
            var replacement = await CliPipeServer.CreateReplacementWithRetryAsync(Factory, Delay, CancellationToken.None);

            Assert.AreSame(created, replacement);
            Assert.AreEqual(2, attempts, "The factory must be retried exactly once after the first failure.");
            Assert.AreEqual(2, delayCalls, "The bounded delay must be awaited before every attempt (including the first made in this helper), so two attempts require two delay calls.");
        }
        finally
        {
            created?.Dispose();
        }
    }

    [TestMethod]
    public async Task CreateReplacementWithRetryAsync_DelaysBeforeFirstAttempt_EvenWhenItSucceeds()
    {
        // This helper is only reached after the fast-path replacement create has already failed
        // once, so even its very first attempt is itself a retry and must be preceded by the
        // bounded backoff — not just the attempts after that.
        var callOrder = new List<string>();
        NamedPipeServerStream? created = null;
        var name = UniquePipeName();
        var security = CurrentUserPipeSecurity();

        NamedPipeServerStream Factory()
        {
            callOrder.Add("create");
            created = CliPipeServer.CreateServerStream(name, security, firstInstance: true);
            return created;
        }

        Task Delay(CancellationToken ct)
        {
            callOrder.Add("delay");
            return Task.CompletedTask;
        }

        try
        {
            var replacement = await CliPipeServer.CreateReplacementWithRetryAsync(Factory, Delay, CancellationToken.None);

            Assert.AreSame(created, replacement);
            CollectionAssert.AreEqual(
                DelayThenCreateOrder,
                callOrder,
                "The delay must be awaited before the create attempt, even when that first attempt succeeds.");
        }
        finally
        {
            created?.Dispose();
        }
    }

    [TestMethod]
    public async Task CreateReplacementWithRetryAsync_NonRecoverableException_PropagatesWithoutRetry()
    {
        // Only IOException/UnauthorizedAccessException are recoverable pipe-creation failures worth
        // retrying. Anything else (a programming/non-recoverable error) must propagate immediately
        // instead of being retried forever.
        var attempts = 0;
        NamedPipeServerStream Factory()
        {
            attempts++;
            throw new InvalidOperationException("simulated programming/non-recoverable failure");
        }

        var delayCalls = 0;
        Task Delay(CancellationToken ct)
        {
            delayCalls++;
            return Task.CompletedTask;
        }

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => CliPipeServer.CreateReplacementWithRetryAsync(Factory, Delay, CancellationToken.None));

        Assert.AreEqual(1, attempts, "A non-recoverable exception must not be retried.");
        Assert.AreEqual(1, delayCalls, "No additional delay must occur once a non-recoverable exception propagates (no retry follows it).");
    }

    [TestMethod]
    public async Task CreateReplacementWithRetryAsync_CancelledDuringRetry_StopsRetryingAndThrows()
    {
        var attempts = 0;
        NamedPipeServerStream Factory()
        {
            attempts++;
            throw new IOException("simulated persistent replacement-creation failure");
        }

        using var cts = new CancellationTokenSource();

        var delayCalls = 0;
        Task Delay(CancellationToken ct)
        {
            delayCalls++;
            if (delayCalls == 2)
            {
                // Simulate app shutdown happening while the retry loop is backing off before the
                // second attempt (the delay before the first attempt already ran normally).
                cts.Cancel();
                return Task.Delay(Timeout.Infinite, ct);
            }

            return Task.CompletedTask;
        }

        // Task.Delay surfaces cancellation as TaskCanceledException, a subtype of
        // OperationCanceledException; assert on the base type the same way the accept loop's
        // `catch (OperationCanceledException)` does, rather than the exact derived type.
        var threwOperationCanceled = false;
        try
        {
            await CliPipeServer.CreateReplacementWithRetryAsync(Factory, Delay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            threwOperationCanceled = true;
        }

        Assert.IsTrue(threwOperationCanceled, "Cancellation during the retry delay must propagate as OperationCanceledException.");

        Assert.AreEqual(1, attempts, "Retry must stop as soon as cancellation is observed, not keep spinning.");
    }
}
