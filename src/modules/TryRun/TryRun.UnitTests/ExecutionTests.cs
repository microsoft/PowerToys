// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class ExecutionTests
{
    [TestMethod]
    public void OutputIsBoundedAndReportsTruncation()
    {
        var buffer = new OutputBuffer();
        buffer.Append(new string('a', OutputBuffer.MaximumCharacters + 1));
        buffer.Append(new string('b', 100));
        Assert.IsTrue(buffer.IsTruncated);
        StringAssert.Contains(buffer.ToString(), "Output limit reached");
        Assert.IsFalse(buffer.ToString().Contains('b'));
    }

    [TestMethod]
    public void OutputPreservesContentBelowLimit()
    {
        var buffer = new OutputBuffer();
        buffer.Append("hello\n");
        buffer.Append("世界");
        Assert.AreEqual("hello\n世界", buffer.ToString());
        Assert.IsFalse(buffer.IsTruncated);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(301)]
    public void InvalidTimeoutIsRejected(int timeout)
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("Write-Output 1", session.WorkingDirectory, session.TemporaryDirectory, timeout);
        Assert.ThrowsException<ArgumentOutOfRangeException>(request.Validate);
    }

    [TestMethod]
    public void EmptyAndOversizedScriptsAreRejected()
    {
        using var session = new RunSession();
        var empty = new ExecutionRequest(" ", session.WorkingDirectory, session.TemporaryDirectory, 60);
        Assert.ThrowsException<ArgumentException>(empty.Validate);
        Assert.ThrowsException<ArgumentException>(() => (empty with { Script = new string('x', ExecutionRequest.MaximumScriptLength + 1) }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (empty with { Script = "hello\0world" }).Validate());
    }

    [TestMethod]
    public void SessionCreatesDistinctDirectoriesAndCleansUp()
    {
        var session = new RunSession();
        var working = session.WorkingDirectory;
        Assert.IsTrue(Directory.Exists(working));
        Assert.AreNotEqual(working, session.TemporaryDirectory);
        File.WriteAllText(Path.Combine(working, "result.txt"), "test");
        session.Dispose();
        session.Dispose();
        Assert.IsFalse(Directory.Exists(working));
    }

    [TestMethod]
    public async Task MissingWorkerNeverFallsBackToDirectExecution()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("Write-Output 1", session.WorkingDirectory, session.TemporaryDirectory, 60);
        var client = new WorkerClient(Path.Combine(session.WorkingDirectory, "missing.exe"));
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(() => client.RunAsync(request, new Progress<WorkerMessage>(), CancellationToken.None));
    }

    [TestMethod]
    public void SessionCleanupHandlesDeeplyNestedOutput()
    {
        var session = new RunSession();
        var working = session.WorkingDirectory;
        var leaf = working;
        for (var depth = 0; depth < 256; depth++)
        {
            leaf = Path.Combine(leaf, "d");
        }

        Directory.CreateDirectory(leaf);
        File.WriteAllText(Path.Combine(leaf, "output.txt"), "test");
        session.Dispose();
        Assert.IsFalse(Directory.Exists(working));
    }

    [TestMethod]
    public void WorkerDirectoriesMustBelongToTheSameSession()
    {
        using var first = new RunSession();
        using var second = new RunSession();
        Assert.ThrowsException<ArgumentException>(() => RunSession.ValidateDirectories(first.WorkingDirectory, second.TemporaryDirectory));
        Assert.ThrowsException<ArgumentException>(() => RunSession.ValidateDirectories(first.TemporaryDirectory, first.WorkingDirectory));
    }
}
