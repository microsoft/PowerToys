// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class ClipboardHelperTests
{
    [DataTestMethod]
    [DataRow(@"\\attacker\share\file.txt")]
    [DataRow(@"//attacker/share/file.txt")]
    [DataRow(@"\\?\UNC\attacker\share\file.txt")]
    [DataRow(@"\\.\GLOBALROOT\Device\Mup\attacker\share\file.txt")]
    public void LocalPathLease_RejectsRemoteOrDevicePath(string path)
    {
        using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);

        Assert.IsNull(lease);
    }

    [TestMethod]
    public void LocalPathLease_RejectsMissingPath()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.txt");
        using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);

        Assert.IsNull(lease);
    }

    [TestMethod]
    public void LocalPathLease_RejectsPathTraversingSymbolicLink()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string targetDirectory = Path.Combine(directory, "target");
        string linkDirectory = Path.Combine(directory, "link");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "file.txt"), "content");

        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkDirectory, targetDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Assert.Inconclusive($"Creating a symbolic link is unavailable: {exception.Message}");
            }

            using LocalPathLease lease =
                LocalPathLease.TryCreateForCurrentUser(Path.Combine(linkDirectory, "file.txt"));

            Assert.IsNull(lease);
        }
        finally
        {
            if (Directory.Exists(linkDirectory))
            {
                Directory.Delete(linkDirectory);
            }

            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_OpensExistingLocalFile()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        string movedDirectory = Path.Combine(directory, "moved");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        try
        {
            using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);

            Assert.IsNotNull(lease);
            Assert.IsFalse(lease.IsDirectory);
            Assert.AreEqual(new FileInfo(path).Length, lease.Length);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_AcquiredReferencePreventsPathReplacementUntilReleased()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        string movedDirectory = Path.Combine(directory, "moved");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        LocalPathLease? lease = null;
        LocalPathLease? acquiredLease = null;
        try
        {
            lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            acquiredLease = lease.Acquire();
            Assert.IsNotNull(acquiredLease);

            lease.Dispose();
            lease = null;
            Assert.IsFalse(TryMoveDirectory(sourceDirectory, movedDirectory));

            acquiredLease.Dispose();
            acquiredLease = null;
            Directory.Move(sourceDirectory, movedDirectory);
        }
        finally
        {
            acquiredLease?.Dispose();
            lease?.Dispose();
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LocalPathLease_AllowsFileReadWhilePreventingReplacement()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        string movedDirectory = Path.Combine(directory, "moved");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        try
        {
            using LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);

            Assert.IsFalse(TryOpenForWrite(path));
            using FileStream stream = new(lease.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using StreamReader reader = new(stream);
            Assert.AreEqual("content", reader.ReadToEnd());
            Assert.IsFalse(TryMoveDirectory(sourceDirectory, movedDirectory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Clipboard_DirectoryStatusDoesNotRetainLease()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string movedDirectory = directory + "-moved";

        try
        {
            using (LocalPathLease lease = LocalPathLease.TryCreateForCurrentUser(directory))
            {
                Assert.IsNotNull(lease);
                Assert.IsTrue(lease.IsDirectory);
            }

            Clipboard.SetLastDragDropFile(directory, null, isDirectory: true);

            Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(
                out string storedPath,
                out LocalPathLease storedLease,
                out bool isDirectory));
            Assert.AreEqual(directory, storedPath);
            Assert.IsNull(storedLease);
            Assert.IsTrue(isDirectory);

            Directory.Move(directory, movedDirectory);
        }
        finally
        {
            Clipboard.LastDragDropFile = null;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            if (Directory.Exists(movedDirectory))
            {
                Directory.Delete(movedDirectory, true);
            }
        }
    }

    [TestMethod]
    public void Clipboard_TransientLeaseReleasesAfterSendAcquiresReference()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        string sourceDirectory = Path.Combine(directory, "source");
        string movedDirectory = Path.Combine(directory, "moved");
        Directory.CreateDirectory(sourceDirectory);
        string path = Path.Combine(sourceDirectory, "file.txt");
        File.WriteAllText(path, "content");

        LocalPathLease? acquiredLease = null;
        try
        {
            LocalPathLease? lease = LocalPathLease.TryCreateForCurrentUser(path);
            Assert.IsNotNull(lease);
            Clipboard.SetLastDragDropFile(path, lease, isTransient: true);
            Clipboard.RequestLastDragDropFileReleaseAfterSend();

            Assert.IsFalse(TryMoveDirectory(sourceDirectory, movedDirectory));
            Assert.IsTrue(Clipboard.TryAcquireLastDragDropFile(
                out string storedPath,
                out acquiredLease,
                out bool isDirectory));
            Assert.AreEqual(path, storedPath);
            Assert.IsNotNull(acquiredLease);
            Assert.IsFalse(isDirectory);
            Assert.IsNull(Clipboard.LastDragDropFile);
            Assert.IsFalse(TryMoveDirectory(sourceDirectory, movedDirectory));

            acquiredLease.Dispose();
            acquiredLease = null;
            Directory.Move(sourceDirectory, movedDirectory);
        }
        finally
        {
            acquiredLease?.Dispose();
            Clipboard.LastDragDropFile = null;
            Directory.Delete(directory, true);
        }
    }

    private static bool TryMoveDirectory(string sourceDirectory, string destinationDirectory)
    {
        try
        {
            Directory.Move(sourceDirectory, destinationDirectory);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryOpenForWrite(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
