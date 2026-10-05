// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class AdvancedPasteFileCleanupTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LockedFilesDoNotMaskAFailedTestOrPreventScopeCleanup(bool scopeCleanupFails)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_CleanupTest_");
        var lockedPath = Path.Combine(directory.FullName, "locked.txt");
        var nextPath = Path.Combine(directory.FullName, "next.txt");
        File.WriteAllText(lockedPath, "locked");
        File.WriteAllText(nextPath, "next");
        var errors = new List<string>();
        var scopeStopped = false;
        try
        {
            using var lockedFile = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
            TestFileCleanup.Run(
                [lockedPath, nextPath],
                directory,
                testFailed: true,
                () =>
                {
                    scopeStopped = true;
                    if (scopeCleanupFails)
                    {
                        throw new AggregateException("Scope teardown failure.");
                    }
                },
                errors.Add);

            Assert.IsTrue(scopeStopped);
            Assert.IsFalse(File.Exists(nextPath), "Later artifacts must still be cleaned after an earlier deletion fails.");
            Assert.HasCount(scopeCleanupFails ? 3 : 2, errors, "Every file and scope cleanup failure must be reported.");
            Assert.IsTrue(errors.Take(2).All(message => message.Contains(directory.FullName, StringComparison.Ordinal)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void CleanupFailureFailsAPassingTestAndStopsItsScope()
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_CleanupTest_");
        var lockedPath = Path.Combine(directory.FullName, "locked.txt");
        File.WriteAllText(lockedPath, "locked");
        var scopeStopped = false;
        var errors = new List<string>();
        try
        {
            using var lockedFile = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var failure = Assert.ThrowsExactly<AggregateException>(
                () => TestFileCleanup.Run([lockedPath], directory, testFailed: false, () => scopeStopped = true, errors.Add));

            Assert.IsTrue(scopeStopped);
            Assert.HasCount(2, failure.InnerExceptions);
            Assert.HasCount(2, errors);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SuccessfulCleanupPreservesOnlyAHealthyScope(bool testFailed)
    {
        var directory = Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_CleanupTest_");
        File.WriteAllText(Path.Combine(directory.FullName, "source.txt"), "source");
        var generatedDirectory = Directory.CreateTempSubdirectory("PowerToys_AdvancedPaste_Output_");
        var generatedPath = Path.Combine(generatedDirectory.FullName, "result.txt");
        File.WriteAllText(generatedPath, "result");
        var scopeStopped = false;
        var errors = new List<string>();
        try
        {
            TestFileCleanup.Run([generatedPath], directory, testFailed, () => scopeStopped = true, errors.Add);

            Assert.AreEqual(testFailed, scopeStopped);
            Assert.IsFalse(Directory.Exists(directory.FullName));
            Assert.IsFalse(Directory.Exists(generatedDirectory.FullName));
            Assert.IsEmpty(errors);
        }
        finally
        {
            if (Directory.Exists(directory.FullName))
            {
                directory.Delete(recursive: true);
            }

            if (Directory.Exists(generatedDirectory.FullName))
            {
                generatedDirectory.Delete(recursive: true);
            }
        }
    }
}
