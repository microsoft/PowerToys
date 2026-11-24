// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Utils;

namespace WorkspacesCsharpLibrary.UnitTests;

[TestClass]
[DoNotParallelize]
public class ModuleProcessTests
{
    [TestMethod]
    [DataRow("PowerToys.WorkspacesLauncher.exe")]
    [DataRow("PowerToys.WorkspacesSnapshotTool.exe")]
    public void SiblingToolsAreResolvedIndependentlyOfCurrentDirectory(string executable)
    {
        var originalDirectory = Environment.CurrentDirectory;
        var unrelatedDirectory = Path.Combine(Path.GetTempPath(), "WorkspacesForeignCwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(unrelatedDirectory);
        try
        {
            Environment.CurrentDirectory = unrelatedDirectory;
            const string arguments = "{6CF910A2-D2E0-436D-A50E-41432A88452A} 0";
            var start = ModuleProcess.CreateStartInfo(executable, arguments);
            Assert.IsTrue(Path.IsPathFullyQualified(start.FileName));
            Assert.AreEqual(Path.Combine(AppContext.BaseDirectory, executable), start.FileName);
            Assert.AreEqual(AppContext.BaseDirectory, start.WorkingDirectory);
            Assert.AreEqual(arguments, start.Arguments);
            Assert.IsTrue(start.CreateNoWindow);
            Assert.IsFalse(start.UseShellExecute);
            Assert.AreEqual(unrelatedDirectory, Environment.CurrentDirectory);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(unrelatedDirectory);
        }
    }
}
