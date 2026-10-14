// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers the single write path <see cref="MonitorStateManager"/> funnels both saves through — the
/// debounced save and the flush <c>Dispose</c> performs — and the publish-by-rename that keeps an
/// interrupted write from damaging what is already on disk. These assert the current design; the
/// original two-writer collision stopped being expressible once there was one write method.
/// </summary>
[TestClass]
public sealed class MonitorStateSaveTests
{
    private const string MonitorA = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1";

    private string _directory = null!;
    private string _statePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"PowerDisplaySave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _statePath = Path.Combine(_directory, "monitor_state.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Leaving a temp directory behind is not worth failing an otherwise green test over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    public void ConcurrentWrites_DoNotCollideOnTheStateFile()
    {
        // Drives what `_writeLock` exists for. Every write goes through one temp path opened for
        // exclusive write, so without the lock two threads landing together fail at CreateFile with
        // "The process cannot access the file because it is being used by another process".
        using var manager = new MonitorStateManager(_statePath);
        manager.UpdateMonitorParameter(MonitorA, "Brightness", 42);

        const int Writers = 4;
        const int WritesPerThread = 40;

        var failures = new List<Exception>();
        using var start = new Barrier(Writers);
        var threads = new List<Thread>();

        for (var i = 0; i < Writers; i++)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    // Inside the try: a throw here would otherwise be unhandled on a background
                    // thread and take down the test host instead of failing the test.
                    start.SignalAndWait();

                    for (var write = 0; write < WritesPerThread; write++)
                    {
                        manager.WriteStateFile();
                    }
                }
                catch (Exception ex)
                {
                    lock (failures)
                    {
                        failures.Add(ex);
                    }
                }
            })
            {
                IsBackground = true,
            };

            threads.Add(thread);
            thread.Start();
        }

        foreach (var thread in threads)
        {
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "A writer thread did not finish.");
        }

        Assert.AreEqual(
            0,
            failures.Count,
            $"Concurrent writes collided on the state file: {(failures.Count > 0 ? failures[0].ToString() : string.Empty)}");
    }

    [TestMethod]
    public void Dispose_FlushesPendingChanges()
    {
        // The debounce window is 2 s and this test does not wait it out, so the value on disk can
        // only have come from the flush in Dispose.
        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(MonitorA, "Brightness", 37);
            Assert.IsFalse(File.Exists(_statePath), "The debounced save must not have run yet.");
        }

        using var reloaded = new MonitorStateManager(_statePath);
        Assert.AreEqual(37, reloaded.GetMonitorParameters(MonitorA)?.Brightness);
    }

    [TestMethod]
    public void Dispose_WithoutChanges_WritesNothing()
    {
        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.GetMonitorParameters(MonitorA);
        }

        Assert.IsFalse(File.Exists(_statePath), "A manager that observed no change must not write.");
    }

    [TestMethod]
    public void FailedWrite_LeavesTheExistingStateFileIntact()
    {
        using (var seed = new MonitorStateManager(_statePath))
        {
            seed.UpdateMonitorParameter(MonitorA, "Brightness", 42);
        }

        var published = File.ReadAllText(_statePath);

        // Occupying the temp path with a directory fails the write at exactly the point where an
        // in-place File.WriteAllText would already have truncated the published file.
        Directory.CreateDirectory(_statePath + ".tmp");

        using (var manager = new MonitorStateManager(_statePath))
        {
            manager.UpdateMonitorParameter(MonitorA, "Brightness", 99);
        }

        Assert.AreEqual(
            published,
            File.ReadAllText(_statePath),
            "A write that could not complete must leave the published file untouched.");
    }
}
