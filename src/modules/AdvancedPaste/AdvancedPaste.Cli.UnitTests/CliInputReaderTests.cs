// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Cli.UnitTests;

[TestClass]
public class CliInputReaderTests
{
    [TestMethod]
    public async Task TextFileLimit_IsMeasuredInCharactersLikeOtherTextInputs()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(inputPath, "ééé");
        try
        {
            var input = await CliInputReader.ReadAsync(
                inputFile: new FileInfo(inputPath),
                stdinRequested: false,
                clipboard: new TestClipboardAdapter(),
                stdin: TextReader.Null,
                maximumTextCharacters: 4,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual("ééé", await input.GetTextAsync());
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [TestMethod]
    public async Task TextFilesWithUnlistedOrNoExtension_AreReadAsText()
    {
        foreach (var extension in new[] { ".cs", string.Empty })
        {
            var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
            await File.WriteAllTextAsync(inputPath, "var answer = 42;");
            try
            {
                var input = await CliInputReader.ReadAsync(
                    inputFile: new FileInfo(inputPath),
                    stdinRequested: false,
                    clipboard: new TestClipboardAdapter(),
                    stdin: TextReader.Null,
                    maximumTextCharacters: 100,
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual("var answer = 42;", await input.GetTextAsync());
                Assert.IsFalse(input.Contains(StandardDataFormats.StorageItems));
            }
            finally
            {
                File.Delete(inputPath);
            }
        }
    }

    [TestMethod]
    public async Task BinaryFile_IsNotExposedAsText()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(inputPath, [0, 1, 2, 0, 255]);
        try
        {
            var input = await CliInputReader.ReadAsync(
                inputFile: new FileInfo(inputPath),
                stdinRequested: false,
                clipboard: new TestClipboardAdapter(),
                stdin: TextReader.Null,
                maximumTextCharacters: 100,
                cancellationToken: CancellationToken.None);

            Assert.IsFalse(input.Contains(StandardDataFormats.Text));
            Assert.IsTrue(input.Contains(StandardDataFormats.StorageItems));
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ValidReplacementCharacter_IsTextWithOrWithoutUtf8Bom(bool bom)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(path, "valid \uFFFD text", new UTF8Encoding(bom, throwOnInvalidBytes: true));
            var input = await ReadFileAsync(path);

            Assert.AreEqual("valid \uFFFD text", await input.GetTextAsync());
            Assert.IsFalse(input.Contains(StandardDataFormats.StorageItems));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(0, true)]
    [DataRow(10000, false)]
    [DataRow(10000, true)]
    public async Task MalformedUtf8_UsesOnlyOriginalFile(int validPrefixLength, bool bom)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var encoding = new UTF8Encoding(bom);
        var bytes = new System.Collections.Generic.List<byte>(encoding.GetPreamble());
        bytes.AddRange(Encoding.UTF8.GetBytes(new string('x', validPrefixLength)));
        bytes.Add(0xff);
        try
        {
            await File.WriteAllBytesAsync(path, bytes.ToArray());
            var input = await ReadFileAsync(path);

            Assert.IsFalse(input.Contains(StandardDataFormats.Text));
            Assert.IsFalse(input.Contains(StandardDataFormats.Html));
            Assert.AreEqual(path, (await input.GetStorageItemsAsync())[0].Path);
            CollectionAssert.AreEqual(bytes.ToArray(), await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task BinaryControlAfterSample_UsesOnlyOriginalFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dat");
        try
        {
            await File.WriteAllTextAsync(path, new string('x', 10000) + '\0');
            var input = await ReadFileAsync(path);

            Assert.IsFalse(input.Contains(StandardDataFormats.Text));
            Assert.IsTrue(input.Contains(StandardDataFormats.StorageItems));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task HtmlTextFile_DoesNotAdvertiseAnImageAttachment()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        try
        {
            await File.WriteAllTextAsync(path, "<p>text</p>");
            var input = await ReadFileAsync(path);

            Assert.IsTrue(input.Contains(StandardDataFormats.Text));
            Assert.IsTrue(input.Contains(StandardDataFormats.Html));
            Assert.IsFalse(input.Contains(StandardDataFormats.StorageItems));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow(".csv", "name,age\r\nAda,37", false)]
    [DataRow(".html", "<p>Hello <strong>world</strong></p>", true)]
    public async Task ClipboardFile_UsesSameTextAndHtmlClassification(string extension, string text, bool html)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        try
        {
            await File.WriteAllTextAsync(path, text);
            var package = new DataPackage();
            package.SetStorageItems([await StorageFile.GetFileFromPathAsync(path)]);

            var input = await CliInputReader.ReadClipboardFileContentAsync(package.GetView(), 100, CancellationToken.None);

            Assert.AreEqual(text, await input.GetTextAsync());
            Assert.AreEqual(html, input.Contains(StandardDataFormats.Html));
            Assert.IsFalse(input.Contains(StandardDataFormats.StorageItems));
            Assert.AreEqual(path, (await package.GetView().GetStorageItemsAsync())[0].Path);
            Assert.AreEqual(text, await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ClipboardBinaryOrMalformedFile_PreservesOriginalFile(bool malformed)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dat");
        byte[] bytes = malformed ? [0xff] : [0, 1, 2, 3];
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            var package = new DataPackage();
            package.SetStorageItems([await StorageFile.GetFileFromPathAsync(path)]);

            var input = await CliInputReader.ReadClipboardFileContentAsync(package.GetView(), 100, CancellationToken.None);

            Assert.IsFalse(input.Contains(StandardDataFormats.Text));
            Assert.AreEqual(path, (await input.GetStorageItemsAsync())[0].Path);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow("Text")]
    [DataRow("HTML Format")]
    [DataRow("Bitmap")]
    public async Task ClipboardRichContent_TakesPrecedenceOverFile(string format)
    {
        var path = Path.GetTempFileName();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var package = new DataPackage();
            package.SetStorageItems([file]);
            if (format == StandardDataFormats.Text)
            {
                package.SetText("clipboard text");
            }
            else if (format == StandardDataFormats.Html)
            {
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat("<p>clipboard HTML</p>"));
            }
            else
            {
                package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            }

            var original = package.GetView();
            var input = await CliInputReader.ReadClipboardFileContentAsync(original, 100, CancellationToken.None);

            Assert.AreSame(original, input);
            Assert.IsTrue(input.Contains(format));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ClipboardMultipleFilesOrFolder_AreNotReadAsText()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "do not choose one file");
            var file = await StorageFile.GetFileFromPathAsync(path);
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
            foreach (var items in new IStorageItem[][] { [file, file], [folder] })
            {
                var package = new DataPackage();
                package.SetStorageItems(items);
                var original = package.GetView();

                var input = await CliInputReader.ReadClipboardFileContentAsync(original, 100, CancellationToken.None);

                Assert.AreSame(original, input);
                Assert.IsFalse(input.Contains(StandardDataFormats.Text));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ClipboardTextFile_EnforcesCharacterLimitAndCancellation()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "ééé");
            var package = new DataPackage();
            package.SetStorageItems([await StorageFile.GetFileFromPathAsync(path)]);

            var input = await CliInputReader.ReadClipboardFileContentAsync(package.GetView(), 3, CancellationToken.None);
            Assert.AreEqual("ééé", await input.GetTextAsync());
            await Assert.ThrowsExactlyAsync<InputTooLargeException>(() =>
                CliInputReader.ReadClipboardFileContentAsync(package.GetView(), 2, CancellationToken.None));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                CliInputReader.ReadClipboardFileContentAsync(package.GetView(), 3, cancellation.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Task<DataPackageView> ReadFileAsync(string path)
        => CliInputReader.ReadAsync(new FileInfo(path), false, new TestClipboardAdapter(), TextReader.Null, 20000, CancellationToken.None);

    private sealed class TestClipboardAdapter : IClipboardAdapter
    {
        public DataPackageView Read() => throw new NotSupportedException();

        public void Write(DataPackage content) => throw new NotSupportedException();
    }
}
