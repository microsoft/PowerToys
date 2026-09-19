// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security.UnitTests;

[TestClass]
public sealed class BackupRestoreArchiveTests
{
    private static readonly string[] ExpectedLegacyPaths =
    [
        "manifest.json",
        @"FancyZones\layout-hotkeys.json",
        @"Workspaces\workspaces.json",
    ];

    [DataTestMethod]
    [DataRow(@"..\escaped.json")]
    [DataRow(@"\rooted.json")]
    [DataRow(@"C:\rooted.json")]
    [DataRow(@"\\server\share\rooted.json")]
    [DataRow(@"Module\settings.json:payload")]
    public void UnsafeArchiveNamesAreRejectedBeforeStaging(string entryName)
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(test.Path, "bad.ptb", (entryName, "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);
        int stagingCountBefore = Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length;

        Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root));

        Assert.AreEqual(stagingCountBefore, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
        Assert.IsFalse(File.Exists(Path.Combine(test.Path, "escaped.json")));
    }

    [TestMethod]
    public void NormalizedCaseCollisionIsRejected()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "collision.ptb",
            ("FancyZones/settings.json", "{\"first\":true}"),
            ("fancyzones\\SETTINGS.JSON", "{\"second\":true}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root));

        StringAssert.Contains(exception.Message, "collision");
    }

    [TestMethod]
    public void DistinctWindowsUnicodeNamesDoNotFalsePositiveAsCollisions()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "unicode-distinct.ptb",
            ("σ.json", "{\"name\":\"sigma\"}"),
            ("ς.json", "{\"name\":\"final-sigma\"}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);
        string stagingPath;

        using (SecureDirectoryRoot staging = BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root))
        {
            stagingPath = staging.FinalPath;
            using SecureFile sigma = staging.OpenFileForRead("σ.json");
            using SecureFile finalSigma = staging.OpenFileForRead("ς.json");
            Assert.AreEqual("{\"name\":\"sigma\"}", sigma.ReadAllText());
            Assert.AreEqual("{\"name\":\"final-sigma\"}", finalSigma.ReadAllText());
        }

        Directory.Delete(stagingPath, recursive: true);
    }

    [TestMethod]
    public void FileAndChildPathConflictIsRejectedBeforeStaging()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "file-child-conflict.ptb",
            ("Module", "{}"),
            ("Module/settings.json", "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root));

        StringAssert.Contains(exception.Message, "conflicts with a child path");
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void NonAdjacentFileAncestorConflictIsRejectedBeforeStaging()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "non-adjacent-file-ancestor.ptb",
            ("a", "{}"),
            ("a-b", "{}"),
            (@"a\c", "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root));

        StringAssert.Contains(exception.Message, "conflicts with a child path: a");
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [DataTestMethod]
    [DataRow("A|a-b|a\\c")]
    [DataRow("a\\b|a-b|a\\b\\c")]
    [DataRow("a\\c|a-b|a")]
    public void FileAncestorConflictVariantsAreRejectedBeforeStaging(string encodedEntries)
    {
        using TestDirectory test = new();
        (string Name, string Contents)[] entries = encodedEntries
            .Split('|')
            .Select(name => (name, "{}"))
            .ToArray();
        string archivePath = CreateArchive(test.Path, "ancestor-variant.ptb", entries);
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root));

        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void ExcessiveArchivePathDepthIsRejectedBeforeStaging()
    {
        using TestDirectory test = new();
        string allowedPath = string.Join('\\', Enumerable.Repeat("d", BackupRestoreArchive.MaximumPathDepth - 1)) + "\\settings.json";
        string deepPath = string.Join('\\', Enumerable.Repeat("d", BackupRestoreArchive.MaximumPathDepth)) + "\\settings.json";
        string allowedArchivePath = CreateArchive(test.Path, "allowed-depth.ptb", (allowedPath, "{}"));
        string archivePath = CreateArchive(test.Path, "deep-path.ptb", (deepPath, "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        using (FileStream stream = File.OpenRead(allowedArchivePath))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
        {
            Assert.AreEqual(1, BackupRestoreArchive.Validate(archive).Count);
        }

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root));

        StringAssert.Contains(exception.Message, "exceeds restore limits");
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void ArchivePathLengthBoundaryIsEnforcedBeforeStaging()
    {
        using TestDirectory test = new();
        string allowedPath = string.Join('\\', Enumerable.Repeat(new string('a', 204), 5));
        string excessivePath = new string('b', 205) + "\\" + string.Join('\\', Enumerable.Repeat(new string('b', 204), 4));
        Assert.AreEqual(BackupRestoreArchive.MaximumPathLength, allowedPath.Length);
        Assert.AreEqual(BackupRestoreArchive.MaximumPathLength + 1, excessivePath.Length);
        string allowedArchivePath = CreateArchive(test.Path, "allowed-length.ptb", (allowedPath, "{}"));
        string excessiveArchivePath = CreateArchive(test.Path, "excessive-length.ptb", (excessivePath, "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        using (FileStream stream = File.OpenRead(allowedArchivePath))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
        {
            Assert.AreEqual(1, BackupRestoreArchive.Validate(archive).Count);
        }

        Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(excessiveArchivePath), root));
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void AggregatePathComponentBoundaryIsEnforcedBeforeStaging()
    {
        using TestDirectory test = new();
        (string Name, string Contents)[] sharedEntries = Enumerable.Range(0, 1_365)
            .Select(index => ($"root{index}\\child\\settings.json", "{}"))
            .ToArray();
        (string Name, string Contents)[] allowedEntries = [.. sharedEntries, ("extra", "{}")];
        (string Name, string Contents)[] excessiveEntries = [.. allowedEntries, ("extra2", "{}")];
        string allowedArchivePath = CreateArchive(test.Path, "allowed-components.ptb", allowedEntries);
        string excessiveArchivePath = CreateArchive(test.Path, "excessive-components.ptb", excessiveEntries);
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        using (FileStream stream = File.OpenRead(allowedArchivePath))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Read))
        {
            Assert.AreEqual(allowedEntries.Length, BackupRestoreArchive.Validate(archive).Count);
        }

        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(excessiveArchivePath), root));

        StringAssert.Contains(exception.Message, "complexity limit");
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void LegacyPtbStructureExtractsWithoutShapeChanges()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "settings_123.ptb",
            ("manifest.json", "{\"Version\":\"0.0.0\"}"),
            ("Keyboard Manager/default.json", "{\"remapKeys\":[]}"),
            ("FancyZones/custom-layouts.json", "{\"custom-layouts\":[]}"),
            ("PowerToys Run/settings.json", "{\"plugins\":[]}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);
        string stagingPath;

        using (SecureDirectoryRoot staging = BackupRestoreArchive.ExtractToExclusiveStaging(root, Path.GetFileName(archivePath), root))
        {
            stagingPath = staging.FinalPath;
            using SecureFile manifest = staging.OpenFileForRead("manifest.json");
            using SecureFile keyboardManager = staging.OpenFileForRead(@"Keyboard Manager\default.json");
            using SecureFile fancyZones = staging.OpenFileForRead(@"FancyZones\custom-layouts.json");
            using SecureFile powerToysRun = staging.OpenFileForRead(@"PowerToys Run\settings.json");

            Assert.AreEqual("{\"Version\":\"0.0.0\"}", manifest.ReadAllText());
            Assert.AreEqual("{\"remapKeys\":[]}", keyboardManager.ReadAllText());
            Assert.AreEqual("{\"custom-layouts\":[]}", fancyZones.ReadAllText());
            Assert.AreEqual("{\"plugins\":[]}", powerToysRun.ReadAllText());
        }

        Directory.Delete(stagingPath, recursive: true);
    }

    [TestMethod]
    public void ValidationPreservesEntryOrderingAndRelativeNames()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "shape.ptb",
            ("manifest.json", "{}"),
            ("FancyZones/layout-hotkeys.json", "{}"),
            ("Workspaces/workspaces.json", "{}"));

        using FileStream stream = File.OpenRead(archivePath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);
        IReadOnlyList<ArchiveEntryDescriptor> entries = BackupRestoreArchive.Validate(archive);

        CollectionAssert.AreEqual(
            ExpectedLegacyPaths,
            entries.Select(entry => entry.RelativePath).ToArray());
    }

    [TestMethod]
    public void PostStagingFailurePreservesOriginalExceptionAndCleansStagingByHandle()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "failure.ptb",
            (@"Module\first.json", "{}"),
            (@"Module\second.json", "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(
                root,
                Path.GetFileName(archivePath),
                root,
                index =>
                {
                    if (index == 1)
                    {
                        throw new InvalidOperationException("injected copy failure");
                    }
                }));

        Assert.AreEqual("injected copy failure", exception.Message);
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void DirectoryPreparationFailureCleansAlreadyCreatedAncestors()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(test.Path, "directory-failure.ptb", (@"a\b\settings.json", "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(
                root,
                Path.GetFileName(archivePath),
                root,
                beforeEntryCopy: null,
                beforeDirectoryCreate: directory =>
                {
                    if (directory.Equals(@"a\b", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("injected directory failure");
                    }
                }));

        Assert.AreEqual("injected directory failure", exception.Message);
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void ExtractionDirectoryPostCreateValidationFailureCleansStaging()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(test.Path, "directory-validation-failure.ptb", (@"Module\settings.json", "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(
                root,
                Path.GetFileName(archivePath),
                root,
                beforeEntryCopy: null,
                afterStagingCreated: staging =>
                    staging.DirectoryBeforeValidationObserver = directory =>
                        throw new InvalidOperationException($"injected validation failure: {directory}")));

        StringAssert.Contains(exception.Message, "injected validation failure: Module");
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    [TestMethod]
    public void CleanupFailureDoesNotReplaceOriginalExtractionException()
    {
        using TestDirectory test = new();
        string archivePath = CreateArchive(
            test.Path,
            "cleanup-failure.ptb",
            (@"Module\first.json", "{}"),
            (@"Module\second.json", "{}"));
        using SecureDirectoryRoot root = SecureDirectoryRoot.Open(test.Path);
        bool cleanupFailureInjected = false;

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            BackupRestoreArchive.ExtractToExclusiveStaging(
                root,
                Path.GetFileName(archivePath),
                root,
                beforeEntryCopy: index =>
                {
                    if (index == 1)
                    {
                        throw new InvalidOperationException("original extraction failure");
                    }
                },
                afterCleanupStep: _ =>
                {
                    if (!cleanupFailureInjected)
                    {
                        cleanupFailureInjected = true;
                        throw new IOException("injected cleanup failure");
                    }
                }));

        Assert.AreEqual("original extraction failure", exception.Message);
        Assert.IsTrue(exception.Data.Contains("StagingCleanupErrors"));
        Assert.AreEqual(0, Directory.GetDirectories(test.Path, "PowerToysRestore-*").Length);
    }

    private static string CreateArchive(string root, string fileName, params (string Name, string Contents)[] entries)
    {
        string archivePath = Path.Combine(root, fileName);
        using FileStream stream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        foreach ((string name, string contents) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
            using StreamWriter writer = new(entry.Open());
            writer.Write(contents);
        }

        return archivePath;
    }
}
