// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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

    [TestMethod]
    public void Begin_WritesLockFile()
    {
        using var scope = CrashDetectionScope.Begin(_lockPath);

        Assert.IsTrue(File.Exists(_lockPath), "lock file should exist after Begin()");
        var contents = File.ReadAllText(_lockPath);
        StringAssert.Contains(contents, "\"version\":1");
        StringAssert.Contains(contents, "\"pid\":");
        StringAssert.Contains(contents, "\"startedAt\":");
    }

    [TestMethod]
    [ExpectedException(typeof(IOException))]
    public void Begin_ThrowsIfLockAlreadyExists()
    {
        File.WriteAllText(_lockPath, "stale");

        // Should throw because of FileMode.CreateNew
        _ = CrashDetectionScope.Begin(_lockPath);
    }

    [TestMethod]
    public void Dispose_DeletesLockFile()
    {
        var scope = CrashDetectionScope.Begin(_lockPath);
        Assert.IsTrue(File.Exists(_lockPath));

        scope.Dispose();

        Assert.IsFalse(File.Exists(_lockPath), "lock file should be gone after Dispose()");
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var scope = CrashDetectionScope.Begin(_lockPath);

        scope.Dispose();
        scope.Dispose();  // must not throw
    }

    [TestMethod]
    public void Dispose_DoesNotThrowWhenLockMissing()
    {
        var scope = CrashDetectionScope.Begin(_lockPath);
        File.Delete(_lockPath);  // simulate lock removed externally

        scope.Dispose();  // must not throw

        Assert.IsFalse(File.Exists(_lockPath));
    }
}
