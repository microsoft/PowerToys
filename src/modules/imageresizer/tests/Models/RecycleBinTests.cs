// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ImageResizer.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Models;

[TestClass]
public sealed class RecycleBinTests : IDisposable
{
    private MockWindowsRecycleBinService _binServiceMock;
    private WindowsRecycleBinService _binService;

    private TestDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [TestInitialize]
    public void TestInitialize()
    {
        _binService = new WindowsRecycleBinService();
        _binServiceMock = new MockWindowsRecycleBinService();
        _binServiceMock.SetDriveHasRecycleBin("C", true);
        _binServiceMock.SetDriveHasRecycleBin("D", false);
    }

    [TestMethod]
    public void DeleteToRecycleBin_ActualFile_Succeeds()
    {
        var path = Path.Combine(_directory, "Test.png");
        File.Copy("Test.png", path);

        _binService.DeleteToRecycleBin(path);

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void DeleteToRecycleBin_FileInUse_ThrowsWin32Exception()
    {
        string testFile = Path.Combine(_directory, $"_{Guid.NewGuid()}.txt");

        using var stream = File.OpenWrite(testFile);
        var ex = Assert.Throws<Win32Exception>(() =>
            _binService.DeleteToRecycleBin(testFile));

        // ERROR_SHARING_VIOLATION = 32
        Assert.AreEqual(32, ex.NativeErrorCode);
    }

    [TestMethod]
    public void DeleteToRecycleBin_NonExistentFile_ThrowsWin32Exception()
    {
        string nonExistentPath = Path.Combine(_directory, $"_{Guid.NewGuid()}.txt");

        var ex = Assert.Throws<Win32Exception>(() =>
            _binService.DeleteToRecycleBin(nonExistentPath));

        // ERROR_FILE_NOT_FOUND = 2
        Assert.AreEqual(2, ex.NativeErrorCode);
    }

    [DataTestMethod]
    [DataRow(@"C:\SomeFile.png", DisplayName = "File in root")]
    [DataRow(@"C:\Folder\File.doc", DisplayName = "File in folder")]
    [DataRow(@"C:\Nested\Folder\Path\File.jpg", DisplayName = "File in nested folders")]
    [DataRow(@"C:\", DisplayName = "Drive root")]
    public void QueryRecycleBin_ValidDrive_ReturnsInfo(string path)
    {
        var result = _binServiceMock.QueryRecycleBin(path);
        Assert.AreEqual(_binServiceMock.SingleDriveQueryResult, result);
    }

    [DataTestMethod]
    [DataRow("", DisplayName = "Empty string")]
    [DataRow(@"relative\path", DisplayName = "Relative path")]
    [DataRow("file.txt", DisplayName = "Filename only")]
    public void QueryRecycleBin_InvalidPath_ThrowsPathException(string invalidPath)
    {
        Assert.Throws<ArgumentException>(() => _binServiceMock.QueryRecycleBin(invalidPath));
    }

    [TestMethod]
    public void QueryRecycleBin_Default_ReturnsAllDrivesResult()
    {
        var result = _binServiceMock.QueryRecycleBin();
        Assert.AreEqual(_binServiceMock.AllDrivesQueryResult, result);
    }

    [TestMethod]
    public void QueryRecycleBin_NullPath_ReturnsAllDrivesResult()
    {
        var result = _binServiceMock.QueryRecycleBin(null);
        Assert.AreEqual(_binServiceMock.AllDrivesQueryResult, result);
    }

    [DataTestMethod]
    [DataRow(@"C:\SomePath\SomeFile.jpg", DisplayName = "File in subfolder")]
    [DataRow(@"C:\File.txt", DisplayName = "File in root")]
    [DataRow(@"C:\Nested\Folder\Path\File.jpg", DisplayName = "File in nested folders")]
    public void DeleteToRecycleBin_ValidFileOnValidDrive_DeletesFile(string pathToDelete)
    {
        _binServiceMock.DeleteToRecycleBin(pathToDelete);

        Assert.IsTrue(_binServiceMock.DeletedFiles.Contains(pathToDelete));
        Assert.AreEqual(1, _binServiceMock.DeletedFiles.Count);
    }

    [DataTestMethod]
    [DataRow(@"D:\Path\File.doc", DisplayName = "File on drive without Recycle Bin")]
    [DataRow(@"D:\File.txt", DisplayName = "File in root on drive without Recycle Bin")]
    [DataRow(@"E:\Unconfigured", DisplayName = "File on unconfigured drive")]
    public void DeleteToRecycleBin_InvalidDrive_ThrowsException(string filePath)
    {
        Assert.Throws<NoRecycleBinException>(() =>
        {
            _binServiceMock.DeleteToRecycleBin(filePath);
        });

        Assert.IsFalse(_binServiceMock.DeletedFiles.Contains(filePath));
        Assert.AreEqual(0, _binServiceMock.DeletedFiles.Count);
    }

    [TestMethod]
    public void DeleteToRecycleBin_MultipleFiles_TracksAll()
    {
        var files = new[]
        {
            @"C:\File.txt",
            @"C:\Folder\File2.txt",
            @"C:\Nested\Folder\File3.txt",
        };

        foreach (var file in files)
        {
            _binServiceMock.DeleteToRecycleBin(file);
        }

        Assert.AreEqual(files.Length, _binServiceMock.DeletedFiles.Count);
        foreach (var file in files)
        {
            Assert.IsTrue(_binServiceMock.DeletedFiles.Contains(file));
        }
    }

    [DataTestMethod]
    [DataRow(@"C:\", DisplayName = "Drive root")]
    [DataRow(@"C:\Folder\File.doc", DisplayName = "File path")]
    [DataRow(@"C:\File.txt", DisplayName = "File in root")]
    [DataRow(@"C:\Nested\Folder\Pic.jpg", DisplayName = "File in nested folder")]
    public void HasRecycleBin_ValidDrive_ReturnsTrue(string path)
    {
        Assert.IsTrue(_binServiceMock.HasRecycleBin(path));
    }

    [DataTestMethod]
    [DataRow(@"D:\", DisplayName = "Drive root")]
    [DataRow(@"D:\File.txt", DisplayName = "File in root")]
    [DataRow(@"D:\Folder\File.doc", DisplayName = "File in folder")]
    [DataRow(@"D:\Nested\Folder\Pic.png", DisplayName = "File in nested folder")]
    public void HasRecycleBin_InvalidDrive_ReturnsFalse(string path)
    {
        Assert.IsFalse(_binServiceMock.HasRecycleBin(path));
    }

    [TestMethod]
    public void SetDriveHasRecycleBin_AddAndRemove_WorksCorrectly()
    {
        Assert.IsFalse(_binServiceMock.HasRecycleBin(@"E:\"));

        _binServiceMock.SetDriveHasRecycleBin("E", true);

        Assert.IsTrue(_binServiceMock.HasRecycleBin(@"E:\"));

        _binServiceMock.SetDriveHasRecycleBin("E", false);

        Assert.IsFalse(_binServiceMock.HasRecycleBin(@"E:\"));
    }

    [TestMethod]
    public void Reset_ClearsDeletedFilesAndDrives()
    {
        _binServiceMock.DeleteToRecycleBin(@"C:\SomeFile.bin");
        _binServiceMock.SetDriveHasRecycleBin("E", true);

        _binServiceMock.Reset();

        Assert.AreEqual(0, _binServiceMock.DeletedFiles.Count);

        Assert.IsFalse(_binServiceMock.HasRecycleBin(@"C:\"));
        Assert.IsFalse(_binServiceMock.HasRecycleBin(@"E:\"));
    }

    [DataTestMethod]
    [DataRow("E", DisplayName = "Drive letter only")]
    [DataRow("E:", DisplayName = "Drive letter with colon")]
    [DataRow("E:\\", DisplayName = "Drive letter with path")]
    public void SetDriveHasRecycleBin_VariousFormats_NormalizesCorrectly(string input)
    {
        _binServiceMock.SetDriveHasRecycleBin(input, true);

        Assert.IsTrue(_binServiceMock.HasRecycleBin(@"E:\"));
    }
}
