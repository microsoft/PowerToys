// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class AdvancedPasteTempFileManagerTests
{
    [TestMethod]
    public void CleanupStaleDirectories_PreservesUnownedDirectories()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"PowerToys_AdvancedPaste_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        var userFilePath = Path.Combine(directoryPath, "user-data.txt");
        File.WriteAllText(userFilePath, "preserve");
        Directory.SetCreationTimeUtc(directoryPath, DateTime.UtcNow.AddDays(-2));

        try
        {
            AdvancedPasteTempFileManager.CleanupStaleDirectories(TimeSpan.FromDays(1));

            Assert.IsTrue(File.Exists(userFilePath));
            Assert.AreEqual("preserve", File.ReadAllText(userFilePath));
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CleanupStaleDirectories_RemovesOwnedDirectories()
    {
        var directory = AdvancedPasteTempFileManager.CreateDirectory();
        File.WriteAllText(Path.Combine(directory.FullName, "stale.txt"), "stale");
        Directory.SetCreationTimeUtc(directory.FullName, DateTime.UtcNow.AddDays(-2));

        try
        {
            AdvancedPasteTempFileManager.CleanupStaleDirectories(TimeSpan.FromDays(1));

            Assert.IsFalse(Directory.Exists(directory.FullName));
        }
        finally
        {
            if (Directory.Exists(directory.FullName))
            {
                Directory.Delete(directory.FullName, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task TryCleanupAfterDelayAsync_RemovesOwnershipMarkerFromConsumedFileDirectory()
    {
        var directory = AdvancedPasteTempFileManager.CreateDirectory();
        var filePath = Path.Combine(directory.FullName, "result.txt");
        File.WriteAllText(filePath, "generated");
        var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
        var package = new DataPackage();
        package.SetStorageItems([storageFile]);

        await package.GetView().TryCleanupAfterDelayAsync(TimeSpan.Zero);

        Assert.IsFalse(Directory.Exists(directory.FullName));
    }
}
