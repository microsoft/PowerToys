// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

[TestClass]
public sealed class SecureFileSystemTests
{
    [TestMethod]
    public void FinalPathAndContainmentUseOpenedRootHandle()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);

        Assert.IsTrue(string.Equals(Path.GetFullPath(rootPath), root.FinalPath, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(SecurePath.IsContained(root.FinalPath, Path.Combine(root.FinalPath, "module", "settings.json")));
        Assert.IsFalse(SecurePath.IsContained(root.FinalPath, root.FinalPath + "-suffix\\settings.json"));
        Assert.IsFalse(SecurePath.IsContained(Path.Combine(test.Path, "σ"), Path.Combine(test.Path, "ς", "settings.json")));
    }

    [TestMethod]
    public void UncFinalPathCanonicalizationDoesNotRequireShareAccess()
    {
        const string extendedUnc = @"\\?\UNC\server.example\PowerToysBackup\module\settings.json";
        const string canonicalUnc = @"\\server.example\PowerToysBackup\module\settings.json";
        const string root = @"\\server.example\PowerToysBackup";

        Assert.AreEqual(canonicalUnc, SecurePath.NormalizeFinalPath(extendedUnc), ignoreCase: true, CultureInfo.InvariantCulture);
        Assert.IsTrue(SecurePath.IsContained(root, canonicalUnc));
        Assert.IsFalse(SecurePath.IsContained(root, @"\\server.example\PowerToysBackup-suffix\settings.json"));
    }

    [TestMethod]
    public void CloudPlaceholderMetadataFailsClosedWithoutHydration()
    {
        const uint cloudReparseTag = 0x9000001A;
        FileHandleMetadata placeholder = new(
            IsDirectory: false,
            IsReparsePoint: true,
            ReparseTag: cloudReparseTag,
            LinkCount: 1,
            Length: 0);

        IOException exception = Assert.ThrowsException<IOException>(() =>
            SecureDirectoryRoot.ValidateMetadata(placeholder, expectDirectory: false, rejectHardLinks: false));

        StringAssert.Contains(exception.Message, $"0x{cloudReparseTag:X8}");
    }

    [TestMethod]
    public void MissingSingleLinkMetadataFailsClosedBeforeOverwrite()
    {
        FileHandleMetadata metadataWithoutLinkSupport = new(
            IsDirectory: false,
            IsReparsePoint: false,
            ReparseTag: 0,
            LinkCount: 0,
            Length: 10);

        IOException exception = Assert.ThrowsException<IOException>(() =>
            SecureDirectoryRoot.ValidateMetadata(metadataWithoutLinkSupport, expectDirectory: false, rejectHardLinks: true));

        StringAssert.Contains(exception.Message, "0 hard links");
        StringAssert.Contains(exception.Message, "before truncation");
    }

    [TestMethod]
    public void StagingValidationFailureDeletesNewDirectoryAndPreservesOriginalException()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        const string suffix = "33333333333333333333333333333333";
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            root.CreateExclusiveStagingDirectory(
                "PowerToysRestore-",
                () => suffix,
                () => throw new InvalidOperationException("injected validation failure")));

        Assert.AreEqual("injected validation failure", exception.Message);
        Assert.IsFalse(Directory.Exists(Path.Combine(rootPath, $"PowerToysRestore-{suffix}")));
    }

    [TestMethod]
    public void ReadOnlyRootUsesHandleRelativeSameHandleRead()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        test.CreateDirectory("root\\module\\nested");
        test.CreateFile("root\\module\\nested\\settings.json", "content");

        using SecureDirectoryRoot root = SecureDirectoryRoot.OpenReadOnly(rootPath);
        List<uint> requestedDirectoryAccess = [];
        root.DirectoryOpenAccessObserver = requestedDirectoryAccess.Add;
        using SecureFile file = root.OpenFileForRead(@"module\nested\settings.json");

        Assert.AreEqual("content", file.ReadAllText());
        Assert.IsTrue(SecurePath.IsContained(root.FinalPath, file.FinalPath));
        CollectionAssert.AreEqual(
            new[] { SecureDirectoryRoot.ReadOnlyDirectoryAccess, SecureDirectoryRoot.ReadOnlyDirectoryAccess },
            requestedDirectoryAccess);
        Assert.IsTrue(requestedDirectoryAccess.TrueForAll(access => (access & NativeMethods.FileWriteData) == 0));
        Assert.IsTrue(requestedDirectoryAccess.TrueForAll(access => (access & NativeMethods.FileAppendData) == 0));
        Assert.IsTrue(requestedDirectoryAccess.TrueForAll(access => (access & NativeMethods.FileWriteAttributes) == 0));
    }

    [TestMethod]
    public void NestedDirectoryCreationRetainsWriteCapableAccess()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");

        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        List<uint> requestedDirectoryAccess = [];
        root.DirectoryOpenAccessObserver = requestedDirectoryAccess.Add;
        using SecureFile file = root.CreateNewFile(@"module\nested\settings.json");
        file.OverwriteAllText("content");

        CollectionAssert.AreEqual(
            new[]
            {
                SecureDirectoryRoot.WritableDirectoryAccess | NativeMethods.Delete,
                SecureDirectoryRoot.WritableDirectoryAccess | NativeMethods.Delete,
            },
            requestedDirectoryAccess);
        Assert.IsTrue(requestedDirectoryAccess.TrueForAll(access => (access & NativeMethods.FileWriteData) != 0));
        Assert.IsTrue(requestedDirectoryAccess.TrueForAll(access => (access & NativeMethods.Delete) != 0));
    }

    [TestMethod]
    public void NewlyCreatedDirectoryValidationFailureRollsBackWithDeleteAccess()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        bool injected = false;
        root.DirectoryBeforeValidationObserver = path =>
        {
            if (!injected && path == "module")
            {
                injected = true;
                throw new InvalidOperationException("injected directory validation failure");
            }
        };

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            root.CreateDirectory(@"module\nested"));

        Assert.AreEqual("injected directory validation failure", exception.Message);
        Assert.IsFalse(Directory.Exists(Path.Combine(rootPath, "module")));
    }

    [TestMethod]
    public void JunctionEscapeIsRejectedBeforeRead()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        string outsidePath = test.CreateDirectory("outside");
        test.CreateFile("outside\\secret.json", "outside");
        test.CreateDirectoryJunction("root\\escape", outsidePath);

        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        IOException exception = Assert.ThrowsException<IOException>(() => root.OpenFileForRead("escape\\secret.json"));
        StringAssert.Contains(exception.Message, "Reparse point rejected");
    }

    [TestMethod]
    public void SymbolicLinkEscapeIsRejectedBeforeRead()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        string outsidePath = test.CreateDirectory("outside");
        test.CreateFile("outside\\secret.json", "outside");

        try
        {
            test.CreateDirectorySymbolicLink("root\\escape", outsidePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            Assert.Inconclusive($"Directory symlink capability is unavailable: {ex.Message}");
        }
        catch (IOException ex)
        {
            Assert.Inconclusive($"Directory symlink capability is unavailable: {ex.Message}");
        }

        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        IOException exception = Assert.ThrowsException<IOException>(() => root.OpenFileForRead("escape\\secret.json"));
        StringAssert.Contains(exception.Message, "Reparse point rejected");
    }

    [TestMethod]
    public void HardlinkedExistingTargetIsRejectedBeforeTruncation()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        string targetPath = test.CreateFile("root\\settings.json", "original");
        string aliasPath = test.CreateHardLink("root\\alias.json", targetPath);

        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        IOException exception = Assert.ThrowsException<IOException>(() => root.OpenFileForOverwrite("settings.json"));

        StringAssert.Contains(exception.Message, "overwrite rejected before truncation");
        Assert.AreEqual("original", File.ReadAllText(targetPath));
        Assert.AreEqual("original", File.ReadAllText(aliasPath));
    }

    [TestMethod]
    public void StagingDirectoriesAreRandomAndCreateNew()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        string firstPath;
        string secondPath;

        using (SecureDirectoryRoot first = root.CreateExclusiveStagingDirectory())
        using (SecureDirectoryRoot second = root.CreateExclusiveStagingDirectory())
        {
            firstPath = first.FinalPath;
            secondPath = second.FinalPath;
            Assert.AreNotEqual(firstPath, secondPath, ignoreCase: true, CultureInfo.InvariantCulture);
            StringAssert.Matches(Path.GetFileName(firstPath), new Regex("^PowerToysRestore-[0-9a-f]{32}$", RegexOptions.CultureInvariant));
            StringAssert.Matches(Path.GetFileName(secondPath), new Regex("^PowerToysRestore-[0-9a-f]{32}$", RegexOptions.CultureInvariant));
            Assert.IsTrue(Directory.Exists(firstPath));
            Assert.IsTrue(Directory.Exists(secondPath));
        }

        Directory.Delete(firstPath);
        Directory.Delete(secondPath);
    }

    [TestMethod]
    public void StagingCollisionRetriesWithoutOpeningOrChangingExistingDirectory()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        const string collisionSuffix = "11111111111111111111111111111111";
        const string uniqueSuffix = "22222222222222222222222222222222";
        string collisionPath = test.CreateDirectory($"root\\PowerToysRestore-{collisionSuffix}");
        string markerPath = test.CreateFile($"root\\PowerToysRestore-{collisionSuffix}\\marker.txt", "unchanged");
        Queue<string> candidates = new([collisionSuffix, uniqueSuffix]);

        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        string stagingPath;
        using (SecureDirectoryRoot staging = root.CreateExclusiveStagingDirectory("PowerToysRestore-", candidates.Dequeue))
        {
            stagingPath = staging.FinalPath;
            Assert.AreEqual(Path.Combine(rootPath, $"PowerToysRestore-{uniqueSuffix}"), stagingPath, ignoreCase: true, CultureInfo.InvariantCulture);
        }

        Assert.IsTrue(Directory.Exists(collisionPath));
        Assert.AreEqual("unchanged", File.ReadAllText(markerPath));
        Directory.Delete(stagingPath);
    }

    [TestMethod]
    public void SameHandleWriteResistsDeterministicPathSwap()
    {
        using TestDirectory test = new();
        string rootPath = test.CreateDirectory("root");
        string victimPath = test.CreateFile("root\\victim.json", "original");
        string outsidePath = test.CreateFile("outside\\sensitive.json", "sensitive");
        string movedPath = Path.Combine(rootPath, "moved.json");

        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(rootPath);
        using (SecureFile openedVictim = root.OpenFileForOverwrite("victim.json", FileShare.ReadWrite | FileShare.Delete))
        {
            File.Move(victimPath, movedPath);
            test.CreateHardLink("root\\victim.json", outsidePath);
            openedVictim.OverwriteAllText("updated");
        }

        Assert.AreEqual("updated", File.ReadAllText(movedPath));
        Assert.AreEqual("sensitive", File.ReadAllText(outsidePath));
        Assert.AreEqual("sensitive", File.ReadAllText(victimPath));
    }
}
