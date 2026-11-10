// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
public sealed class ResultExporterTests
{
    [TestMethod]
    public async Task TextIsUtf8NamedFromSelectedBasenameAndSanitizedScriptName()
    {
        using var fixture = new Fixture();
        var result = new PowerScriptTransformResult { Text = "Hello 世界" };
        var paths = await Export(result, PowerScriptIO.Text, fixture, name: @"C:\bad/name");
        Assert.AreEqual("sample - C__bad_name.txt", Path.GetFileName(paths.Single()));
        CollectionAssert.AreEqual(new UTF8Encoding(false).GetBytes(result.Text), await File.ReadAllBytesAsync(paths[0]));
        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
    }

    [TestMethod]
    public async Task ConcurrentExportsDoNotOverwriteExistingResultsOrSources()
    {
        using var fixture = new Fixture();
        var source = fixture.Write("result.md", "produced");
        var existing = Path.Combine(fixture.Destination, "result.md");
        await File.WriteAllTextAsync(existing, "existing");
        var result = new PowerScriptTransformResult { FilePaths = [source] };
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Export(result, PowerScriptIO.Files, fixture)));
        var outputs = results.SelectMany(paths => paths).ToArray();
        Assert.AreEqual(16, outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var output in outputs)
        {
            Assert.AreEqual("produced", await File.ReadAllTextAsync(output));
        }

        Assert.AreEqual("existing", await File.ReadAllTextAsync(existing));
        Assert.AreEqual("produced", await File.ReadAllTextAsync(source));
        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination, "*.tmp").Length);
    }

    [TestMethod]
    public async Task DirectoryReparseAndOriginalOutputSourcesAreRejected()
    {
        using var fixture = new Fixture();
        var source = fixture.Write("result.txt", "produced");
        var link = Path.Combine(fixture.Root, "link.txt");
        File.CreateSymbolicLink(link, source);
        foreach (var invalid in new[] { fixture.Destination, link, fixture.Input, Path.Combine(fixture.Root, "out", "..", "result.txt") })
        {
            await Assert.ThrowsExactlyAsync<IOException>(() => Export(new PowerScriptTransformResult { FilePaths = [invalid] }, PowerScriptIO.Files, fixture));
        }

        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
    }

    [TestMethod]
    public async Task SourceAlreadyInDestinationIsCopiedToAnotherNameWithoutMovingIt()
    {
        using var fixture = new Fixture();
        var source = Path.Combine(fixture.Destination, "result.md");
        await File.WriteAllTextAsync(source, "source");
        var paths = await Export(new PowerScriptTransformResult { FilePaths = [source] }, PowerScriptIO.Files, fixture);
        Assert.AreEqual("result (1).md", Path.GetFileName(paths.Single()));
        Assert.AreEqual("source", await File.ReadAllTextAsync(source));
        Assert.AreEqual("source", await File.ReadAllTextAsync(paths[0]));
    }

    [TestMethod]
    public async Task InvalidDestinationsAreRejectedBeforePublishing()
    {
        using var fixture = new Fixture();
        var link = Path.Combine(fixture.Root, "linked");
        Directory.CreateSymbolicLink(link, fixture.Destination);
        foreach (var invalid in new[] { fixture.Input, link, @"\\server\share", @"\\?\C:\", @"C:\parent\..\other" })
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => ResultExporter.ExportAsync(
                new PowerScriptTransformResult { Text = "data" },
                Script(PowerScriptIO.Text),
                [fixture.Input],
                invalid));
        }
    }

    [TestMethod]
    public async Task CancellationDuringWriteCleansStagingAndNeverPublishesPartialFile()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        using var watcher = new FileSystemWatcher(fixture.Destination, "*.tmp");
        watcher.Created += (_, _) => cancellation.Cancel();
        watcher.EnableRaisingEvents = true;
        var result = new PowerScriptTransformResult { Text = new string('x', 32 * 1024 * 1024) };
        var error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Export(result, PowerScriptIO.Text, fixture, token: cancellation.Token));
        Assert.IsTrue(cancellation.IsCancellationRequested);
        StringAssert.Contains(error.Message, "0 output file(s)");
        Assert.AreEqual(0, ((IReadOnlyList<string>)error.Data["ExportedPaths"]!).Count);
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
    }

    [TestMethod]
    public async Task ErrorAfterFirstExportReportsRetainedCommittedPathsAndLeavesNoStagingFile()
    {
        using var fixture = new Fixture();
        var source = fixture.Write("result.md", "complete");
        var result = new PowerScriptTransformResult { FilePaths = [source, fixture.Destination] };
        var error = await Assert.ThrowsExactlyAsync<IOException>(() => Export(result, PowerScriptIO.Files, fixture));
        StringAssert.Contains(error.Message, "1 output file(s) already published and retained");
        var committed = (IReadOnlyList<string>)error.Data["ExportedPaths"]!;
        Assert.AreEqual(1, committed.Count);
        Assert.AreEqual("complete", await File.ReadAllTextAsync(committed[0]));
        Assert.AreEqual("complete", await File.ReadAllTextAsync(source));
        Assert.AreEqual(1, Directory.GetFiles(fixture.Destination).Length);
    }

    [TestMethod]
    public async Task OverlongPublishedPathFailsAfterStagingAndCleansUp()
    {
        using var fixture = new Fixture();
        var input = fixture.Write(new string('i', 80) + ".md", "original");
        var error = await Assert.ThrowsExactlyAsync<IOException>(() => ResultExporter.ExportAsync(
            new PowerScriptTransformResult { Text = "complete staging bytes" },
            Script(PowerScriptIO.Text, new string('s', 80)),
            [input],
            fixture.Destination));
        Assert.IsInstanceOfType<PathTooLongException>(error.InnerException);
        StringAssert.Contains(error.Message, "0 output file(s)");
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
        Assert.AreEqual("original", await File.ReadAllTextAsync(input));
    }

    [TestMethod]
    public async Task CancellationBeforeExportAndHostFailurePublishNothing()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Export(new PowerScriptTransformResult { Text = "data" }, PowerScriptIO.Text, fixture, token: cancellation.Token));
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Export(new PowerScriptTransformResult { ExitCode = 7, StdErr = "diagnostic-" + new string('e', 6000) }, PowerScriptIO.None, fixture));
        StringAssert.Contains(error.Message, "diagnostic-");
        StringAssert.Contains(error.Message, "exit 7");
        Assert.IsTrue(error.Message.Length < 2400);
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
        Assert.AreEqual(0, (await Export(new PowerScriptTransformResult(), PowerScriptIO.None, fixture)).Count);
    }

    [TestMethod]
    public async Task WrongShapesAndTooManyOutputFilesAreRejected()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsExactlyAsync<IOException>(() => Export(new PowerScriptTransformResult(), PowerScriptIO.Text, fixture));
        await Assert.ThrowsExactlyAsync<IOException>(() => Export(new PowerScriptTransformResult { Text = "text", FilePaths = [] }, PowerScriptIO.Text, fixture));
        await Assert.ThrowsExactlyAsync<IOException>(() => Export(new PowerScriptTransformResult { FilePaths = Enumerable.Repeat(fixture.Input, 1025).ToArray() }, PowerScriptIO.Files, fixture));
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
    }

    [TestMethod]
    public async Task KnownMediaFormatsAreValidatedAndExportedWithCorrectExtensions()
    {
        using var fixture = new Fixture();
        var bitmap = Bitmap();
        var wave = Wave();
        var images = await Export(new PowerScriptTransformResult { Image = new PowerScriptMedia(new PowerScriptMediaItem("image/bmp", bitmap)) }, PowerScriptIO.Image, fixture);
        var audio = await Export(new PowerScriptTransformResult { Audio = new PowerScriptMedia(new PowerScriptMediaItem("audio/wav", wave)) }, PowerScriptIO.Audio, fixture);
        Assert.AreEqual(".bmp", Path.GetExtension(images.Single()));
        Assert.AreEqual(".wav", Path.GetExtension(audio.Single()));
        CollectionAssert.AreEqual(bitmap, await File.ReadAllBytesAsync(images[0]));
        CollectionAssert.AreEqual(wave, await File.ReadAllBytesAsync(audio[0]));
        var png = Fixture.Png();
        var compressed = await Export(new PowerScriptTransformResult { Image = new PowerScriptMedia(new PowerScriptMediaItem("image/png", png)) }, PowerScriptIO.Image, fixture);
        Assert.AreEqual(".png", Path.GetExtension(compressed.Single()));
        CollectionAssert.AreEqual(png, await File.ReadAllBytesAsync(compressed[0]));
    }

    [TestMethod]
    public async Task UnknownMismatchedTruncatedAndMalformedMediaAreRefused()
    {
        using var fixture = new Fixture();
        foreach (var (mime, bytes, type) in new[]
        {
            ("image/png", Bitmap(), PowerScriptIO.Image),
            ("image/bmp", Wave(), PowerScriptIO.Image),
            ("image/bmp", Bitmap()[..^1], PowerScriptIO.Image),
            ("audio/wav", Bitmap(), PowerScriptIO.Audio),
            ("audio/wav", Wave()[..^1], PowerScriptIO.Audio),
            ("audio/mpeg", Wave(), PowerScriptIO.Audio),
        })
        {
            var media = new PowerScriptMedia(new PowerScriptMediaItem(mime, bytes));
            var result = type == PowerScriptIO.Image ? new PowerScriptTransformResult { Image = media } : new PowerScriptTransformResult { Audio = media };
            await Assert.ThrowsExactlyAsync<IOException>(() => Export(result, type, fixture));
        }

        var bitmap = Bitmap();
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(18), int.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(22), int.MaxValue);
        await Assert.ThrowsExactlyAsync<IOException>(() => Export(new PowerScriptTransformResult { Image = new PowerScriptMedia(new PowerScriptMediaItem("image/bmp", bitmap)) }, PowerScriptIO.Image, fixture));
        var wave = Wave();
        wave[32] = 0;
        await Assert.ThrowsExactlyAsync<IOException>(() => Export(new PowerScriptTransformResult { Audio = new PowerScriptMedia(new PowerScriptMediaItem("audio/wav", wave)) }, PowerScriptIO.Audio, fixture));
        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
    }

    [TestMethod]
    public async Task FuzzInlineMediaDoesNotPublishUnknownBytesOrLeakStagingFiles()
    {
        using var fixture = new Fixture();
        var random = new Random(4309);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var bytes = new byte[random.Next(1, 200)];
            random.NextBytes(bytes);
            var image = iteration % 2 == 0;
            var media = new PowerScriptMedia(new PowerScriptMediaItem(image ? "image/bmp" : "audio/wav", bytes));
            await Assert.ThrowsExactlyAsync<IOException>(() => Export(
                image ? new PowerScriptTransformResult { Image = media } : new PowerScriptTransformResult { Audio = media },
                image ? PowerScriptIO.Image : PowerScriptIO.Audio,
                fixture));
        }

        Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
    }

    [TestMethod]
    public async Task StructurallyValidMediaMutationsCannotCauseParserCrashesOrPartialFiles()
    {
        using var fixture = new Fixture();
        var random = new Random(6301);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var image = iteration % 2 == 0;
            var bytes = image ? Bitmap() : Wave();
            bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            var media = new PowerScriptMedia(new PowerScriptMediaItem(image ? "image/bmp" : "audio/wav", bytes));
            try
            {
                var paths = await Export(
                    image ? new PowerScriptTransformResult { Image = media } : new PowerScriptTransformResult { Audio = media },
                    image ? PowerScriptIO.Image : PowerScriptIO.Audio,
                    fixture);
                CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(paths.Single()));
                File.Delete(paths[0]);
            }
            catch (IOException error)
            {
                Assert.AreEqual(0, ((IReadOnlyList<string>)error.Data["ExportedPaths"]!).Count);
            }

            Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
        }
    }

    [TestMethod]
    public async Task FuzzUnsafeExportSourceNamesAreRejectedWithoutPublishingOrChangingFiles()
    {
        using var fixture = new Fixture();
        var source = fixture.Write("produced.txt", "produced source");
        var random = new Random(20063);
        const string invalidCharacters = "<>:\"|?*\0\u001f";
        for (var iteration = 0; iteration < 180; iteration++)
        {
            var token = new string(Enumerable.Range(0, random.Next(1, 16)).Select(_ => (char)random.Next('a', 'z' + 1)).ToArray());
            var unsafeName = (iteration % 9) switch
            {
                0 => token + invalidCharacters[random.Next(invalidCharacters.Length)] + ".txt",
                1 => token + ".txt.",
                2 => token + ".txt ",
                3 => "cOn." + token,
                4 => "COM" + random.Next(1, 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + token,
                5 => "LPT¹." + token,
                6 => @"out\..\produced.txt",
                7 => @"out\.\produced.txt",
                _ => @"\" + token + ".txt",
            };
            var path = fixture.Root + "\\" + unsafeName;
            var error = await Assert.ThrowsExactlyAsync<IOException>(() => Export(
                new PowerScriptTransformResult { FilePaths = [path] },
                PowerScriptIO.Files,
                fixture));
            Assert.IsInstanceOfType<ArgumentException>(error.InnerException);
            Assert.AreEqual(0, ((IReadOnlyList<string>)error.Data["ExportedPaths"]!).Count);
            Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
        }

        Assert.AreEqual("produced source", await File.ReadAllTextAsync(source));
        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
    }

    [TestMethod]
    public async Task FuzzPathLikeScriptDisplayNamesStayInsideDestinationAsSafeBasenames()
    {
        using var fixture = new Fixture();
        var random = new Random(21143);
        for (var iteration = 0; iteration < 80; iteration++)
        {
            var suffix = new string(Enumerable.Range(0, random.Next(0, 70)).Select(_ => (char)random.Next(0, 128)).ToArray());
            var name = (iteration % 4) switch
            {
                0 => @"C:\outside\..\script-" + suffix,
                1 => @"\\server\share\script-" + suffix,
                2 => @"..\..\script-" + suffix,
                _ => "<>:\"|?*\0" + suffix,
            };
            var paths = await Export(new PowerScriptTransformResult { Text = "text output" }, PowerScriptIO.Text, fixture, name);
            var path = paths.Single();
            Assert.AreEqual(fixture.Destination, Path.GetDirectoryName(path));
            Assert.AreEqual(".txt", Path.GetExtension(path));
            Assert.IsTrue(Path.GetFileName(path).StartsWith("sample - ", StringComparison.Ordinal));
            Assert.IsFalse(Path.GetFileName(path).Any(c => Path.GetInvalidFileNameChars().Contains(c)));
            Assert.AreEqual("text output", await File.ReadAllTextAsync(path));
            File.Delete(path);
            Assert.AreEqual(0, Directory.GetFiles(fixture.Destination).Length);
        }

        Assert.AreEqual("original input", await File.ReadAllTextAsync(fixture.Input));
    }

    private static Task<IReadOnlyList<string>> Export(PowerScriptTransformResult result, PowerScriptIO output, Fixture fixture, string name = "Test script", CancellationToken token = default) =>
        ResultExporter.ExportAsync(result, Script(output, name), [fixture.Input], fixture.Destination, token);

    private static PowerScriptInfo Script(PowerScriptIO output, string name = "Test script") => new() { Output = output, Name = name };

    private static byte[] Bitmap()
    {
        var bytes = new byte[58];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), 24);
        return bytes;
    }

    private static byte[] Wave() => Fixture.Wave();
}
