// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AdvancedPaste.Cli;

internal static class CliOutputWriter
{
    internal static async Task<CliOutputResult> WriteAsync(
        DataPackage package,
        FileInfo? outputFile,
        bool stdoutRequested,
        IClipboardAdapter clipboard,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var view = package.GetView();
        var text = await view.GetTextOrEmptyAsync();
        var storageFile = await GetSingleStorageFileAsync(view);

        if (outputFile is not null)
        {
            if (storageFile is not null)
            {
                try
                {
                    Directory.CreateDirectory(outputFile.DirectoryName!);
                    await CopyFileAsync(storageFile.Path, outputFile, cancellationToken);
                    return new CliOutputResult("file", null, outputFile.FullName, OutputClipboard: false);
                }
                finally
                {
                    await view.TryCleanupAfterDelayAsync(TimeSpan.Zero);
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                throw new UnsupportedOutputException("The transformation did not produce text or a file.");
            }

            await WriteTextFileAsync(text, outputFile, cancellationToken);
            return new CliOutputResult("text", text, outputFile.FullName, OutputClipboard: false);
        }

        if (stdoutRequested)
        {
            if (storageFile is not null)
            {
                await view.TryCleanupAfterDelayAsync(TimeSpan.Zero);
                throw new UnsupportedOutputException("File-producing actions require --output or clipboard output.");
            }

            if (string.IsNullOrEmpty(text))
            {
                throw new UnsupportedOutputException("The transformation did not produce text.");
            }

            await stdout.WriteAsync(text.AsMemory(), cancellationToken);
            return new CliOutputResult("text", text, null, OutputClipboard: false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await view.HasUsableDataAsync())
        {
            throw new UnsupportedOutputException("The transformation did not produce supported content.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        clipboard.Write(package);
        return new CliOutputResult(storageFile is null ? "text" : "file", text, null, OutputClipboard: true);
    }

    private static async Task WriteTextFileAsync(string text, FileInfo outputFile, CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(outputFile.DirectoryName!, $".{outputFile.Name}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, outputFile.FullName, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task CopyFileAsync(string sourcePath, FileInfo outputFile, CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(outputFile.DirectoryName!, $".{outputFile.Name}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true))
            await using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, outputFile.FullName, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task<StorageFile?> GetSingleStorageFileAsync(DataPackageView view)
    {
        if (!view.Contains(StandardDataFormats.StorageItems))
        {
            return null;
        }

        var items = await view.GetStorageItemsAsync();
        return items.Count == 1 ? items.Single() as StorageFile : null;
    }
}
