// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
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
}
