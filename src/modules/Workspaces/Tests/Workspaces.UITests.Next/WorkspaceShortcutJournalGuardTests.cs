// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests;

[TestClass]
public sealed class WorkspaceShortcutJournalGuardTests
{
    [TestMethod]
    public void OnlyExistingRecoveryJournalsAreProtected()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            string original = Path.Combine(folder.FullName, $"{Guid.NewGuid():B}.links.json");
            string other = Path.Combine(folder.FullName, $"{Guid.NewGuid():D}.links.json");
            string createdByTest = Path.Combine(folder.FullName, $"{Guid.NewGuid():B}.links.json");
            File.WriteAllText(original, "original");
            File.WriteAllText(other, "unrelated");
            using (WorkspaceShortcutJournalGuard.Acquire(folder.FullName))
            {
                Assert.Throws<IOException>(() => File.ReadAllText(original));
                Assert.AreEqual("unrelated", File.ReadAllText(other));
                File.WriteAllText(createdByTest, "test-owned");
                Assert.AreEqual("test-owned", File.ReadAllText(createdByTest));
            }

            Assert.AreEqual("original", File.ReadAllText(original));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void AcquisitionFailureReleasesOtherJournals()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            // The guard opens journals in ordinal name order, independent of Windows directory enumeration.
            string first = Path.Combine(folder.FullName, "{00000000-0000-0000-0000-000000000001}.links.json");
            string busy = Path.Combine(folder.FullName, "{00000000-0000-0000-0000-000000000002}.links.json");
            File.WriteAllText(first, "unchanged");
            File.WriteAllText(busy, "busy");
            using (File.OpenRead(busy))
            {
                var error = Assert.Throws<Win32Exception>(() =>
                {
                    using var guard = WorkspaceShortcutJournalGuard.Acquire(folder.FullName);
                });
                Assert.AreEqual(32, error.NativeErrorCode, "The fixture must stop on a sharing violation.");
                Assert.AreEqual("unchanged", File.ReadAllText(first));
            }
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ExistingJournalsRemainProtectedAcrossProfileIsolationAndRestore()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var profile = folder.CreateSubdirectory("Workspaces");
            var icons = profile.CreateSubdirectory("WorkspacesIcons");
            string journal = Path.Combine(icons.FullName, $"{Guid.NewGuid():B}.links.json");
            string data = Path.Combine(profile.FullName, "workspaces.json");
            File.WriteAllText(journal, "journal");
            File.WriteAllText(data, "user workspace");
            using (var guard = WorkspaceShortcutJournalGuard.AcquireForProfile(profile.FullName))
            {
                Assert.Throws<IOException>(() => File.ReadAllText(journal));
                Assert.AreEqual("user workspace", File.ReadAllText(data));
                Assert.Throws<IOException>(() => Directory.Move(profile.FullName, Path.Combine(folder.FullName, "redirected")));
                using (var lease = new WorkspaceProfileSnapshot(profile.FullName))
                {
                    lease.Isolate();
                    File.WriteAllText(data, "test workspace");
                }

                Assert.AreEqual("user workspace", File.ReadAllText(data));
                Assert.Throws<IOException>(() => File.ReadAllText(journal));
            }

            Assert.AreEqual("user workspace", File.ReadAllText(data));
            Assert.AreEqual("journal", File.ReadAllText(journal));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RedirectedDirectoryOrJournalIsRejected(bool redirectDirectory)
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            string directory = Path.Combine(folder.FullName, "WorkspacesIcons");
            string target = folder.CreateSubdirectory("unrelated").FullName;
            string file = Path.Combine(target, "untouched");
            File.WriteAllText(file, "unchanged");
            try
            {
                if (redirectDirectory)
                {
                    Directory.CreateSymbolicLink(directory, target);
                }
                else
                {
                    Directory.CreateDirectory(directory);
                    File.CreateSymbolicLink(Path.Combine(directory, $"{Guid.NewGuid():B}.links.json"), file);
                }
            }
            catch (IOException error) when ((error.HResult & 0xFFFF) == 1314)
            {
                Assert.Inconclusive("The test account cannot create a symbolic link.");
            }

            Assert.Throws<IOException>(() =>
            {
                using var guard = WorkspaceShortcutJournalGuard.Acquire(directory);
            });
            Assert.AreEqual("unchanged", File.ReadAllText(file));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
