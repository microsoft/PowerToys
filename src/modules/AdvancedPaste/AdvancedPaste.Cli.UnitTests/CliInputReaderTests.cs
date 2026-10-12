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

    private static Task<DataPackageView> ReadFileAsync(string path)
        => CliInputReader.ReadAsync(new FileInfo(path), false, new TestClipboardAdapter(), TextReader.Null, 20000, CancellationToken.None);

    private sealed class TestClipboardAdapter : IClipboardAdapter
    {
        public DataPackageView Read() => throw new NotSupportedException();

        public void Write(DataPackage content) => throw new NotSupportedException();
    }
}
