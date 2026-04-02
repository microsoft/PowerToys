// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class JsonSettingsManagerMigrationTests
{
    private string _tempDir = string.Empty;

    /// <summary>
    /// Concrete subclass that exposes the protected migration method for testing.
    /// </summary>
    private sealed class TestSettingsManager : JsonSettingsManager
    {
        public TestSettingsManager(string filePath)
        {
            FilePath = filePath;
        }
    }

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CmdPalTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
