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
    public void MediaMatchingChecksHeadersMimeAndSingleSelectionWithoutExcludingFileActions()
    {
        using var fixture = new Fixture();
        var image = fixture.Write("image.PNG", Fixture.Png());
        var audio = fixture.Write("audio.wav", Fixture.Wave());
        var fake = fixture.Write("not-image.jpg", "not an image");
        var mislabeled = fixture.Write("wrong.png", Fixture.Wave());
        var png = new PowerScriptInfo { Input = PowerScriptIO.Image, InputSchema = Fixture.MediaSchema("image", "image/png") };
        var wav = new PowerScriptInfo { Input = PowerScriptIO.Audio, InputSchema = Fixture.MediaSchema("audio", "audio/wav") };
        Assert.IsTrue(ExplorerScriptService.IsApplicable(png, [image]));
        Assert.IsTrue(ExplorerScriptService.IsApplicable(wav, [audio]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(png, [audio]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(wav, [image]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(png, [fake]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(png, [mislabeled]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(png, [image, image]));
        Assert.IsTrue(ExplorerScriptService.IsApplicable(new PowerScriptInfo { Input = PowerScriptIO.Files, InputExtensions = ["*"] }, [fake]));
        Assert.IsFalse(ExplorerScriptService.IsApplicable(new PowerScriptInfo { Input = PowerScriptIO.Image, InputSchema = Fixture.MediaSchema("image", "image/jpeg") }, [image]));
    }

    [TestMethod]
    public void OversizedAndTruncatedMediaRemainAvailableOnlyToFileScripts()
    {
        using var fixture = new Fixture();
        var path = fixture.Write("large.png", Fixture.Png());
        using (var stream = File.OpenWrite(path))
        {
            stream.SetLength(PowerScriptMedia.MaxItemBytes + 1L);
        }

        var png = new PowerScriptInfo { Input = PowerScriptIO.Image, InputSchema = Fixture.MediaSchema("image", "image/png") };
        Assert.IsFalse(ExplorerScriptService.IsApplicable(png, [path]));
        Assert.IsTrue(ExplorerScriptService.IsApplicable(new PowerScriptInfo { Input = PowerScriptIO.Files }, [path]));
        var truncated = fixture.Write("short.png", Fixture.Png()[..15]);
        Assert.IsFalse(ExplorerScriptService.IsApplicable(png, [truncated]));
    }

    [TestMethod]
    public void TaggedAudioChecksTheFrameAfterMetadataInsteadOfAcceptingAnId3Label()
    {
        using var fixture = new Fixture();
        var bytes = new byte[1028];
        "ID3"u8.CopyTo(bytes);
        bytes[3] = 4;
        bytes[8] = 7;
        bytes[9] = 118;
        bytes[1024] = 0xff;
        bytes[1025] = 0xfb;
        bytes[1026] = 0x90;
        var path = fixture.Write("tagged.mp3", bytes);
        var script = new PowerScriptInfo { Input = PowerScriptIO.Audio, InputSchema = Fixture.MediaSchema("audio", "audio/mpeg") };
        Assert.IsTrue(ExplorerScriptService.IsApplicable(script, [path]));
        bytes[1024] = 0;
        File.WriteAllBytes(path, bytes);
        Assert.IsFalse(ExplorerScriptService.IsApplicable(script, [path]));
    }

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
