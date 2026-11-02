// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class CmdPalIndexedFixtureCleanupTests
{
    [TestMethod]
    public void RemovesReadOnlyFixturesWithoutChangingTheirParentOrSibling()
    {
        WithFixture((root, file, folder, child) =>
        {
            var sibling = Path.Combine(root, "unrelated.txt");
            File.WriteAllText(sibling, "preserve");
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
            File.SetAttributes(child, File.GetAttributes(child) | FileAttributes.ReadOnly);
            File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);
            File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.ReadOnly);
            var parentAttributes = File.GetAttributes(root);

            IndexedFixtureCleanup.Remove(file, folder, Path.GetFileName(child));

            Assert.IsFalse(File.Exists(file));
            Assert.IsFalse(Directory.Exists(folder));
            Assert.AreEqual(parentAttributes, File.GetAttributes(root));
            Assert.AreEqual("preserve", File.ReadAllText(sibling));
        });
    }

    [TestMethod]
    public void ReportsUnexpectedFolderContentInsteadOfDeletingIt()
    {
        WithFixture((root, file, folder, child) =>
        {
            var unrelated = Path.Combine(folder, "unrelated.txt");
            File.WriteAllText(unrelated, "preserve");

            Assert.ThrowsExactly<IOException>(
                () => IndexedFixtureCleanup.Remove(file, folder, Path.GetFileName(child)));

            Assert.AreEqual("preserve", File.ReadAllText(unrelated));
            Assert.IsTrue(Directory.Exists(folder));
        });
    }

    private static void WithFixture(Action<string, string, string, string> assertion)
    {
        var root = Path.Combine(Path.GetTempPath(), $"CmdPalFixtureCleanup-{Guid.NewGuid():N}");
        var folder = Path.Combine(root, "fixture-folder");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(root, "fixture.txt");
        var child = Path.Combine(folder, "child.txt");
        File.WriteAllText(file, "fixture");
        File.WriteAllText(child, "fixture");
        try
        {
            assertion(root, file, folder, child);
        }
        finally
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Prepend(root))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(root, recursive: true);
        }
    }
}
