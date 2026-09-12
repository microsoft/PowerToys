// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Byte-exact assertions for files and directory trees produced asynchronously by a UI action.</summary>
public static class FileSystemAssert
{
    public static void AreFilesEqual(string source, string destination, int timeoutMS = 30_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);

        var expected = ReadSharedBytes(source);
        var copied = WaitHelper.WaitForStable(
            observe: () => File.Exists(destination) ? ReadSharedBytes(destination) : null,
            isMatch: bytes => bytes is not null && expected.SequenceEqual(bytes),
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100,
            shouldRetryException: exception => exception is IOException);
        Assert.IsTrue(copied.Succeeded, $"'{destination}' did not acquire the exact contents of '{source}'. Last error: {copied.LastException}");
    }

    public static void AreDirectoryTreesEqual(string source, string destination, int timeoutMS = 30_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);

        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var directories = Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
        var copied = WaitHelper.WaitForStable(
            observe: () => Directory.Exists(destination) &&
                directories.All(path => Directory.Exists(Path.Combine(destination, Path.GetRelativePath(source, path)))) &&
                files.All(path => File.Exists(Path.Combine(destination, Path.GetRelativePath(source, path)))),
            isMatch: value => value,
            timeoutMS: timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(copied.Succeeded, $"The complete directory tree from '{source}' did not appear at '{destination}'.");

        CollectionAssert.AreEquivalent(
            files.Select(path => Path.GetRelativePath(source, path)).ToArray(),
            Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(destination, path)).ToArray(),
            "The destination file inventory differs from the source.");
        CollectionAssert.AreEquivalent(
            directories.Select(path => Path.GetRelativePath(source, path)).ToArray(),
            Directory.GetDirectories(destination, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(destination, path)).ToArray(),
            "The destination directory inventory differs from the source.");
        foreach (var file in files)
        {
            AreFilesEqual(file, Path.Combine(destination, Path.GetRelativePath(source, file)), timeoutMS);
        }
    }

    internal static byte[] ReadSharedBytes(string path)
    {
        // An observer must not deny the producer's write, timestamp update, or atomic replacement.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var contents = new MemoryStream();
        stream.CopyTo(contents);
        return contents.ToArray();
    }
}
