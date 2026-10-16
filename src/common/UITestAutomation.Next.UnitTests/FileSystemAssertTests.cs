// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class FileSystemAssertTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"FileSystemAssertTests-{Guid.NewGuid():N}");

    [TestInitialize]
    public void CreateFixture() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteFixture() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void EqualFilesIncludeBinaryContentsAndUnicodeNames()
    {
        var source = Path.Combine(root, "Cafe\u0301-\u6F22-\U0001F680.bin");
        var destination = Path.Combine(root, "copy.bin");
        byte[] contents = [0, 1, 127, 128, 255];
        File.WriteAllBytes(source, contents);
        File.WriteAllBytes(destination, contents);

        FileSystemAssert.AreFilesEqual(source, destination, timeoutMS: 1_000);
    }

    [TestMethod]
    public void DifferentFileContentsFail()
    {
        var source = Path.Combine(root, "source.bin");
        var destination = Path.Combine(root, "copy.bin");
        File.WriteAllBytes(source, [1, 2, 3]);
        File.WriteAllBytes(destination, [1, 2, 4]);

        Assert.Throws<AssertFailedException>(() => FileSystemAssert.AreFilesEqual(source, destination, timeoutMS: 50));
    }

    [TestMethod]
    public void MissingDestinationFileFails()
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "source");

        Assert.Throws<AssertFailedException>(() =>
            FileSystemAssert.AreFilesEqual(source, Path.Combine(root, "missing.txt"), timeoutMS: 50));
    }

    [TestMethod]
    public void SharedReadsAllowAnExistingWriter()
    {
        var path = Path.Combine(root, "live.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, FileSystemAssert.ReadSharedBytes(path));
    }

    [TestMethod]
    public void EqualTreesIncludeNestedAndEmptyDirectories()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
        foreach (var path in new[] { source, destination })
        {
            Directory.CreateDirectory(Path.Combine(path, "nested", "empty"));
            File.WriteAllBytes(Path.Combine(path, "nested", "data.bin"), [0, 128, 255]);
        }

        FileSystemAssert.AreDirectoryTreesEqual(source, destination, timeoutMS: 1_000);
    }

    [TestMethod]
    public void ExtraDestinationFileFails()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
        File.WriteAllText(Path.Combine(destination, "extra.txt"), "extra");

        Assert.Throws<AssertFailedException>(() =>
            FileSystemAssert.AreDirectoryTreesEqual(source, destination, timeoutMS: 1_000));
    }

    [TestMethod]
    public void ExtraDestinationDirectoryFails()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
        Directory.CreateDirectory(Path.Combine(destination, "extra"));

        Assert.Throws<AssertFailedException>(() =>
            FileSystemAssert.AreDirectoryTreesEqual(source, destination, timeoutMS: 1_000));
    }

    [TestMethod]
    public void MissingEmptyDirectoryFails()
    {
        var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
        Directory.CreateDirectory(Path.Combine(source, "empty"));

        Assert.Throws<AssertFailedException>(() =>
            FileSystemAssert.AreDirectoryTreesEqual(source, destination, timeoutMS: 50));
    }
}
