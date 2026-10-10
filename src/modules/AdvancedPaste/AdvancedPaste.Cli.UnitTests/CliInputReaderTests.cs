// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private sealed class TestClipboardAdapter : IClipboardAdapter
    {
        public DataPackageView Read() => throw new NotSupportedException();

        public void Write(DataPackage content) => throw new NotSupportedException();
    }
}
