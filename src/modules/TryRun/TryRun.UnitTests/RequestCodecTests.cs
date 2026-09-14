// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class RequestCodecTests
{
    [TestMethod]
    public async Task ReaderStopsAtOneRequestAndRejectsOversizedMessages()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("Write-Output '中文'", session.WorkingDirectory, session.TemporaryDirectory, 1) { Arguments = ["中文", string.Empty, "'\""] };
        using var reader = new StringReader(JsonSerializer.Serialize(request) + "\nnext");
        var decoded = await RequestCodec.ReadAsync(reader, CancellationToken.None);
        Assert.AreEqual(request.Script, decoded.Script);
        Assert.AreEqual(request.WorkingDirectory, decoded.WorkingDirectory);
        Assert.AreEqual(request.TemporaryDirectory, decoded.TemporaryDirectory);
        Assert.AreEqual(request.TimeoutSeconds, decoded.TimeoutSeconds);
        Assert.AreEqual(request.Kind, decoded.Kind);
        CollectionAssert.AreEqual(request.Arguments, decoded.Arguments);
        Assert.AreEqual("next", reader.ReadToEnd());
        using var oversized = new StringReader(new string(' ', RequestCodec.MaximumMessageLength + 1));
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => RequestCodec.ReadAsync(oversized, CancellationToken.None));
    }

    [TestMethod]
    public void NullAndMalformedRequestsAreRejected()
    {
        Assert.ThrowsException<InvalidDataException>(() => RequestCodec.Parse("null"));
        Assert.ThrowsException<JsonException>(() => RequestCodec.Parse("{"));
        Assert.ThrowsException<ArgumentNullException>(() => RequestCodec.Parse("{}"));
    }
}
