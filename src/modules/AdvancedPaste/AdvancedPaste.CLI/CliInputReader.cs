// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.Cli;

internal static class CliInputReader
{
    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".htm",
        ".html",
        ".ini",
        ".json",
        ".log",
        ".markdown",
        ".md",
        ".txt",
        ".xml",
    };

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

        var package = new DataPackage();
        if (stdinRequested)
        {
            package.SetText(await ReadBoundedAsync(stdin, maximumTextCharacters, cancellationToken));
            return package.GetView();
        }

        if (!inputFile!.Exists)
        {
            throw new IOException();
        }

        var storageFile = await StorageFile.GetFileFromPathAsync(inputFile.FullName);
        package.SetStorageItems([storageFile]);

        if (TextFileExtensions.Contains(inputFile.Extension))
        {
            using var reader = inputFile.OpenText();
            var text = await ReadBoundedAsync(reader, maximumTextCharacters, cancellationToken);

            package.SetText(text);
            if (text.Length > 0 &&
                (inputFile.Extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                 inputFile.Extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)))
            {
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(text));
            }
        }

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
