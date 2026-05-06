// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class CrashRecoveryTests
{
    private string _tempDir = string.Empty;
    private string _lockPath = string.Empty;
    private string _flagPath = string.Empty;
    private string _settingsPath = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pd-rec-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _lockPath = Path.Combine(_tempDir, "discovery.lock");
        _flagPath = Path.Combine(_tempDir, "crash_detected.flag");
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(_settingsPath, "{\"enabled\":{\"PowerDisplay\":true}}");
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

    private CrashRecovery NewRecovery(Func<string, bool>? signal = null)
        => new CrashRecovery(_lockPath, _flagPath, _settingsPath, signal ?? (_ => true));

    [TestMethod]
    public void DetectOrphanAndDisable_ReturnsFalseWhenNoLock()
    {
        var rec = NewRecovery();

        var result = rec.DetectOrphanAndDisable();

        Assert.IsFalse(result);
        Assert.IsFalse(File.Exists(_flagPath), "flag must not be written when no orphan");
    }

    [TestMethod]
    public void DetectOrphanAndDisable_RunsFullSequenceWhenOrphanPresent()
    {
        File.WriteAllText(_lockPath, "{\"version\":1,\"pid\":1234,\"startedAt\":\"2026-05-06T10:00:00Z\"}");
        var signaled = false;
        var rec = NewRecovery(_ =>
        {
            signaled = true;
            return true;
        });

        var result = rec.DetectOrphanAndDisable();

        Assert.IsTrue(result);
        Assert.IsTrue(File.Exists(_flagPath), "flag should be written");
        Assert.IsFalse(File.Exists(_lockPath), "lock should be deleted (commit)");
        Assert.IsTrue(signaled, "auto-disable event should be signaled");

        var settingsJson = File.ReadAllText(_settingsPath);

        // WriteIndented produces "PowerDisplay": false (with space); strip whitespace for portability.
        var settingsCompact = settingsJson.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        StringAssert.Contains(settingsCompact, "\"PowerDisplay\":false");
    }

    [TestMethod]
    public void DetectOrphanAndDisable_HandlesUnknownVersionAsOrphan()
    {
        File.WriteAllText(_lockPath, "{\"version\":99,\"pid\":1234}");
        var rec = NewRecovery();

        var result = rec.DetectOrphanAndDisable();

        Assert.IsTrue(result, "unknown version should still be treated as orphan");
        Assert.IsTrue(File.Exists(_flagPath));
    }

    [TestMethod]
    public void DetectOrphanAndDisable_LeavesLockIntactOnFlagWriteFailure()
    {
        File.WriteAllText(_lockPath, "{\"version\":1}");

        // Make the flag path unwritable: create a regular FILE at the directory path
        // we'd need to create. Then flag-path's parent directory cannot be created
        // because there's a file in the way. Directory.CreateDirectory throws IOException
        // (or a subclass).
        var blockingFile = Path.Combine(_tempDir, "blocked");
        File.WriteAllText(blockingFile, "I'm a file blocking the dir creation");
        var unwritableFlag = Path.Combine(_tempDir, "blocked", "crash_detected.flag");
        var rec = new CrashRecovery(_lockPath, unwritableFlag, _settingsPath, _ => true);

        try
        {
            rec.DetectOrphanAndDisable();
            Assert.Fail("expected an IOException-family exception");
        }
        catch (IOException)
        {
            // expected — Directory.CreateDirectory or File.WriteAllText threw
        }
        finally
        {
            Assert.IsTrue(File.Exists(_lockPath), "lock must remain on failure");
        }
    }

    [TestMethod]
    public void DetectOrphanAndDisable_LeavesLockIntactOnSignalFailure()
    {
        File.WriteAllText(_lockPath, "{\"version\":1}");
        var rec = new CrashRecovery(
            lockPath: _lockPath,
            flagPath: _flagPath,
            settingsPath: _settingsPath,
            signalEvent: _ => throw new InvalidOperationException("simulated"));

        try
        {
            rec.DetectOrphanAndDisable();
            Assert.Fail("expected the signal failure to propagate");
        }
        catch (InvalidOperationException)
        {
            // expected
        }
        finally
        {
            Assert.IsTrue(File.Exists(_lockPath), "lock must remain on failure");

            // Steps 1 and 2 already ran; flag and settings are written. That's expected —
            // they'll be no-ops on retry.
            Assert.IsTrue(File.Exists(_flagPath));
        }
    }
}
