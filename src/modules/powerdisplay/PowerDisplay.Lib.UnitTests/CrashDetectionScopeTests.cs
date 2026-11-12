// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class CrashDetectionScopeTests
{
    private string _tempDir = string.Empty;
    private string _lockPath = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pd-scope-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _lockPath = Path.Combine(_tempDir, "discovery.lock");
    }

    [TestCleanup]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private sealed class FakeProcessExitHook : IProcessExitHook
    {
        public List<EventHandler> Handlers { get; } = new();

        public void Subscribe(EventHandler handler) => Handlers.Add(handler);

        public void Unsubscribe(EventHandler handler) => Handlers.Remove(handler);

        public void RaiseExit()
        {
            // Copy to tolerate handlers that mutate the list (e.g. self-unsubscribe).
            foreach (var h in Handlers.ToList())
            {
                h(this, EventArgs.Empty);
            }
        }
    }

    [TestMethod]
    public void Begin_WritesLockFileAtomically()
    {
        var hook = new FakeProcessExitHook();

        using var scope = CrashDetectionScope.Begin(_lockPath, hook);

        Assert.IsTrue(File.Exists(_lockPath), "lock file should exist after Begin");
        Assert.IsFalse(File.Exists(_lockPath + ".tmp"), "temp file should not linger");
    }

    [TestMethod]
    public void Begin_SubscribesToProcessExit()
    {
        var hook = new FakeProcessExitHook();

        using var scope = CrashDetectionScope.Begin(_lockPath, hook);

        Assert.AreEqual(1, hook.Handlers.Count, "Begin should subscribe exactly one ProcessExit handler");
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromProcessExit()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);

        scope.Dispose();

        Assert.AreEqual(0, hook.Handlers.Count, "Dispose should unsubscribe the ProcessExit handler");
    }

    [TestMethod]
    public void Dispose_DeletesLockFile()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);
        Assert.IsTrue(File.Exists(_lockPath));

        scope.Dispose();

        Assert.IsFalse(File.Exists(_lockPath), "Dispose should delete the lock file");
    }

    [TestMethod]
    public void ProcessExitFired_BeforeDispose_DeletesLock()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);
        Assert.IsTrue(File.Exists(_lockPath));

        // Simulate Environment.Exit firing ProcessExit while the scope is still alive
        // (i.e. discovery is mid-flight, Dispose hasn't run).
        hook.RaiseExit();

        Assert.IsFalse(File.Exists(_lockPath), "ProcessExit handler should best-effort delete the lock");
    }

    [TestMethod]
    public void ProcessExitFired_AfterDispose_DoesNothing()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);

        scope.Dispose();
        Assert.IsFalse(File.Exists(_lockPath));

        // Even if a stale handler somehow fires after Dispose, it must not throw.
        // (In practice Dispose unsubscribed it; this guards against ordering races.)
        hook.RaiseExit();

        Assert.IsFalse(File.Exists(_lockPath), "lock should remain deleted");
    }

    [TestMethod]
    public void Dispose_AfterProcessExit_DoesNotThrow()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);

        hook.RaiseExit();
        Assert.IsFalse(File.Exists(_lockPath));

        // Late Dispose (e.g. from a finally block on a still-running thread) must be safe.
        scope.Dispose();

        Assert.IsFalse(File.Exists(_lockPath));
    }

    [TestMethod]
    public void ProcessExitFired_LockFileMissing_DoesNotThrow()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);

        // Simulate the lock being removed by something else first.
        File.Delete(_lockPath);

        // No exception expected — ProcessExit handlers must not throw or they'd
        // disrupt the CLR shutdown sequence.
        hook.RaiseExit();
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var hook = new FakeProcessExitHook();
        var scope = CrashDetectionScope.Begin(_lockPath, hook);

        scope.Dispose();
        scope.Dispose();

        Assert.AreEqual(0, hook.Handlers.Count);
    }

    [TestMethod]
    public void MultipleScopes_DoNotShareState()
    {
        var hook1 = new FakeProcessExitHook();
        var path1 = Path.Combine(_tempDir, "lock1");
        var path2 = Path.Combine(_tempDir, "lock2");

        var scope1 = CrashDetectionScope.Begin(path1, hook1);
        scope1.Dispose();

        var hook2 = new FakeProcessExitHook();
        using var scope2 = CrashDetectionScope.Begin(path2, hook2);

        Assert.AreEqual(0, hook1.Handlers.Count, "first scope's handler should be unsubscribed");
        Assert.AreEqual(1, hook2.Handlers.Count, "second scope should subscribe on its own hook");
        Assert.IsFalse(File.Exists(path1));
        Assert.IsTrue(File.Exists(path2));
    }
}
