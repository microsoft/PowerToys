// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.Cli;

internal static class CliInputReader
{
    private const int TextDetectionSampleLength = 4096;

    internal static async Task<DataPackageView> ReadAsync(
        FileInfo? inputFile,
        bool stdinRequested,
        IClipboardAdapter clipboard,
        TextReader stdin,
        int maximumTextCharacters,
        CancellationToken cancellationToken)
    {
        if (inputFile is null && !stdinRequested)
        {
            var clipboardInput = clipboard.Read();
            await EnsureBoundedTextAsync(clipboardInput, maximumTextCharacters);
            return clipboardInput;
        }

        if (stdinRequested)
        {
            var package = new DataPackage();
            package.SetText(await ReadBoundedAsync(stdin, maximumTextCharacters, cancellationToken));
            return package.GetView();
        }

        return await ReadFileAsync(inputFile!, maximumTextCharacters, cancellationToken);
    }

    internal static async Task<DataPackageView> ReadClipboardFileContentAsync(
        DataPackageView input,
        int maximumTextCharacters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!input.Contains(StandardDataFormats.StorageItems) ||
            input.Contains(StandardDataFormats.Text) ||
            input.Contains(StandardDataFormats.Html) ||
            input.Contains(StandardDataFormats.Bitmap))
        {
            return input;
        }

        var items = await input.GetStorageItemsAsync();
        if (items.Count != 1 || items[0] is not StorageFile file)
        {
            return input;
        }

        return await ReadFileAsync(new FileInfo(file.Path), maximumTextCharacters, cancellationToken);
    }

    private static async Task<DataPackageView> ReadFileAsync(
        FileInfo inputFile,
        int maximumTextCharacters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!inputFile.Exists)
        {
            throw new IOException();
        }

        using var reader = new StreamReader(inputFile.OpenRead(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
        var sample = new char[TextDetectionSampleLength];
        int sampleLength;
        try
        {
            sampleLength = await reader.ReadAsync(sample.AsMemory(), cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return await CreateFilePackageViewAsync(inputFile);
        }

        if (!LooksLikeText(sample.AsSpan(0, sampleLength)))
        {
            return await CreateFilePackageViewAsync(inputFile);
        }

        if (sampleLength > maximumTextCharacters)
        {
            throw new InputTooLargeException();
        }

        var textBuilder = new StringBuilder(sampleLength);
        textBuilder.Append(sample, 0, sampleLength);
        try
        {
            textBuilder.Append(await ReadBoundedAsync(reader, maximumTextCharacters - sampleLength, cancellationToken));
        }
        catch (DecoderFallbackException)
        {
            return await CreateFilePackageViewAsync(inputFile);
        }

        var text = textBuilder.ToString();
        if (!LooksLikeText(text.AsSpan()))
        {
            return await CreateFilePackageViewAsync(inputFile);
        }

        var package = new DataPackage();
        package.SetText(text);
        if (text.Length > 0 &&
            (inputFile.Extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
             inputFile.Extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)))
        {
            package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(text));
        }

        return package.GetView();
    }

    private static bool LooksLikeText(ReadOnlySpan<char> sample)
    {
        foreach (var character in sample)
        {
            if (character == '\0' ||
                (char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<DataPackageView> CreateFilePackageViewAsync(FileInfo inputFile)
    {
        var storageFile = await StorageFile.GetFileFromPathAsync(inputFile.FullName);
        var package = new DataPackage();
        package.SetStorageItems([storageFile]);
        return package.GetView();
    }

    private static async Task<string> ReadBoundedAsync(TextReader reader, int maximumCharacters, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[8192];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return builder.ToString();
            }

            if (builder.Length + read > maximumCharacters)
            {
                throw new InputTooLargeException();
            }

            builder.Append(buffer, 0, read);
        }
    }

    private static async Task EnsureBoundedTextAsync(DataPackageView input, int maximumCharacters)
    {
        if (input.Contains(StandardDataFormats.Text) && (await input.GetTextAsync()).Length > maximumCharacters)
        {
            throw new InputTooLargeException();
        }

        if (input.Contains(StandardDataFormats.Html) && (await input.GetHtmlFormatAsync()).Length > maximumCharacters)
        {
            throw new InputTooLargeException();
        }
    }
}
