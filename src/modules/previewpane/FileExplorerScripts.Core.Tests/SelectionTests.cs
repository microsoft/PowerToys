// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
public sealed class SelectionTests
{
    [TestMethod]
    public void EveryFileAndSelectionCountMustMatchWithoutMutatingSelection()
    {
        using var fixture = new Fixture();
        var second = fixture.Write("UPPER.MD", "second");
        var other = fixture.Write("other.txt", "other");
        var files = new[] { fixture.Input, second };
        var script = new PowerScriptInfo { Input = PowerScriptIO.Files, InputExtensions = [".md"], InputMinFiles = 2, InputMaxFiles = 2 };
        Assert.IsTrue(ExplorerScriptService.IsApplicable(script, files));
        CollectionAssert.AreEqual(new[] { fixture.Input, second }, files);
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, [fixture.Input]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, [fixture.Input, other]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, [fixture.Input, second, fixture.Input]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(new PowerScriptInfo { Input = PowerScriptIO.Image }, files));
        Assert.IsTrue(ExplorerScriptService.IsApplicable(new PowerScriptInfo { Input = PowerScriptIO.Files, InputExtensions = ["*"] }, [other]));
        ExplorerScriptService.ValidateSelection(Enumerable.Repeat(fixture.Input, 1024).ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => ExplorerScriptService.ValidateSelection(Enumerable.Repeat(fixture.Input, 1025).ToArray()));
        Assert.ThrowsExactly<ArgumentException>(() => ExplorerScriptService.ValidateSelection([]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, []));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, [fixture.Destination]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, [Path.Combine(fixture.Root, "absent.md")]));
    }

    [TestMethod]
    public void DirectoriesMissingFilesAndNonLocalOrUnsafePathsAreRejected()
    {
        using var fixture = new Fixture();
        foreach (var path in new[]
        {
            fixture.Destination, "relative.md", @"C:relative.md", @"\\server\share\a.md",
            @"\\?\C:\file.md", @"\\.\NUL", @"C:\NUL", @"C:\CON.txt",
            @"C:\file.md:stream", @"C:\trailing. ", @"C:\parent\..\file.md",
            "C:\\" + new string('a', 260), fixture.Input.Replace('\\', '/'), fixture.Input + "\\",
        })
        {
            Assert.ThrowsExactly<ArgumentException>(() => ExplorerScriptService.ValidateSelection([path]), path);
        }

        Assert.ThrowsExactly<FileNotFoundException>(() => ExplorerScriptService.ValidateSelection([Path.Combine(fixture.Root, "absent.md")]));
    }

    [TestMethod]
    public void FileAndAncestorReparsePointsAreRejected()
    {
        using var fixture = new Fixture();
        var fileLink = Path.Combine(fixture.Root, "link.md");
        var directoryLink = Path.Combine(fixture.Root, "linked");
        File.CreateSymbolicLink(fileLink, fixture.Input);
        Directory.CreateSymbolicLink(directoryLink, fixture.Destination);
        File.WriteAllText(Path.Combine(fixture.Destination, "inside.md"), "inside");
        Assert.ThrowsExactly<ArgumentException>(() => ExplorerScriptService.ValidateSelection([fileLink]));
        Assert.ThrowsExactly<ArgumentException>(() => ExplorerScriptService.ValidateSelection([Path.Combine(directoryLink, "inside.md")]));
    }

    [TestMethod]
    public void FuzzPathGrammarFailsClosed()
    {
        var random = new Random(1107);
        for (var iteration = 0; iteration < 400; iteration++)
        {
            var text = new string(Enumerable.Range(0, random.Next(0, 300)).Select(_ => (char)random.Next(0, 256)).ToArray());
            Assert.ThrowsExactly<ArgumentException>(() => ExplorerScriptService.ValidateSelection([text]));
        }
    }
}
