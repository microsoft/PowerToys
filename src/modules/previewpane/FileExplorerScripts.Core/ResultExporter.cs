// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using PowerScripts.Client;

namespace FileExplorerScripts.Core;

public static class ResultExporter
{
    /// <summary>
    /// Copies results without executing them. On failure, exception Data["ExportedPaths"] contains
    /// an immutable list of already published files; these files are not rolled back.
    /// Cancellation remains an OperationCanceledException.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ExportAsync(
        PowerScriptTransformResult result,
        PowerScriptInfo script,
        IReadOnlyList<string> inputs,
        string destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(inputs);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            var diagnostics = result.StdErr ?? string.Empty;
            diagnostics = diagnostics[..Math.Min(diagnostics.Length, 2048)];
            throw new InvalidOperationException($"PowerScripts failed (exit {result.ExitCode}). Review the script's trust, policy and runtime in PowerScripts. {diagnostics}");
        }

        var selection = inputs.ToArray();
        ExplorerScriptService.ValidateSelection(selection);
        destination = LocalPaths.Validate(destination, directory: true);
        var committed = new List<string>();
        try
        {
            switch (script.Output)
            {
                case PowerScriptIO.None:
                    return Array.Empty<string>();
                case PowerScriptIO.Text:
                    if (result.Text is null || result.FilePaths is not null || result.Image is not null || result.Audio is not null)
                    {
                        throw new InvalidDataException("Expected only text output from this script.");
                    }

                    await PublishAsync(
                        destination,
                        MakeName(selection[0], script.Name, ".txt"),
                        selection,
                        async (stream, token) =>
                        {
                            using var writer = new StreamWriter(stream, new UTF8Encoding(false, true), leaveOpen: true);
                            await writer.WriteAsync(result.Text.AsMemory(), token).ConfigureAwait(false);
                            await writer.FlushAsync(token).ConfigureAwait(false);
                        },
                        committed,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case PowerScriptIO.Files:
                    if (result.FilePaths is null || result.FilePaths.Count > LocalPaths.MaxCount ||
                        result.Text is not null || result.Image is not null || result.Audio is not null)
                    {
                        throw new InvalidDataException("Expected only file output, with at most 1024 files.");
                    }

                    foreach (var source in result.FilePaths.ToArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var path = LocalPaths.Validate(source, directory: false);
                        if (selection.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException("The script returned a selected original as output; only separate produced files can be exported.");
                        }

                        await using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        if ((File.GetAttributes(read.SafeFileHandle) & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                        {
                            throw new InvalidDataException("Output sources must be regular files, not folders or reparse points.");
                        }

                        await PublishAsync(
                            destination,
                            Path.GetFileName(path),
                            selection.Append(path).ToArray(),
                            (stream, token) => read.CopyToAsync(stream, token),
                            committed,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case PowerScriptIO.Image:
                case PowerScriptIO.Audio:
                    var media = script.Output == PowerScriptIO.Image ? result.Image : result.Audio;
                    if (media is null || result.Text is not null || result.FilePaths is not null ||
                        (script.Output == PowerScriptIO.Image ? result.Audio is not null : result.Image is not null) ||
                        media.Items.Count is < 1 or > 64 || (!media.IsArray && media.Items.Count != 1))
                    {
                        throw new InvalidDataException("Expected only inline media of the declared output shape (1–64 items).");
                    }

                    long total = 0;
                    foreach (var item in media.Items.ToArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (item?.Data is null || item.Data.Length is 0 or > PowerScriptMedia.MaxItemBytes ||
                            (total += item.Data.Length) > PowerScriptMedia.MaxTotalBytes)
                        {
                            throw new InvalidDataException("Inline media exceeds the supported 16 MiB/item or 32 MiB/collection limits.");
                        }

                        var data = item.Data.ToArray();
                        var extension = MediaFormats.GetExtension(item.MimeType, data, script.Output);
                        await PublishAsync(
                            destination,
                            MakeName(selection[0], script.Name, extension),
                            selection,
                            (stream, token) => stream.WriteAsync(data, token).AsTask(),
                            committed,
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
                default:
                    throw new NotSupportedException($"Explorer cannot export '{script.Output}' output. Use files or text instead.");
            }

            return new ReadOnlyCollection<string>(committed);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OperationCanceledException or NotSupportedException)
        {
            var status = $"Export stopped: {committed.Count} output file(s) already published and retained.";
            Exception failure = ex is OperationCanceledException
                ? new OperationCanceledException(status, ex, cancellationToken)
                : new IOException(status + " " + ex.Message, ex);
            failure.Data["ExportedPaths"] = new ReadOnlyCollection<string>(committed.ToArray());
            if (ex.Data.Contains("TemporaryPath"))
            {
                failure.Data["TemporaryPath"] = ex.Data["TemporaryPath"];
                failure.Data["CleanupError"] = ex.Data["CleanupError"];
            }

            throw failure;
        }
    }

    private static async Task PublishAsync(
        string destination,
        string name,
        IReadOnlyList<string> inputs,
        Func<FileStream, CancellationToken, Task> write,
        List<string> committed,
        CancellationToken cancellationToken)
    {
        if (!LocalPaths.IsSafeName(name))
        {
            throw new InvalidDataException("Output has an unsafe basename.");
        }

        LocalPaths.Validate(destination, directory: true);
        var temporary = Path.Combine(destination, ".powerscripts-" + Guid.NewGuid().ToString("N") + ".tmp");
        if (temporary.Length > LocalPaths.MaxPathLength)
        {
            throw new PathTooLongException("Choose a shorter destination path (room is required for an atomic staging file).");
        }

        var created = false;
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                created = true;
                await write(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            for (var suffix = 0; suffix < 4096; suffix++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LocalPaths.Validate(destination, directory: true);
                var candidate = suffix == 0 ? name :
                    Path.GetFileNameWithoutExtension(name) + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")" + Path.GetExtension(name);
                var final = Path.Combine(destination, candidate);
                if (final.Length > LocalPaths.MaxPathLength)
                {
                    throw new PathTooLongException("Output path is too long; choose a shorter destination or output name.");
                }

                if (inputs.Contains(final, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Move(temporary, final, overwrite: false);
                    created = false;
                    committed.Add(final);
                    return;
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) is 80 or 183)
                {
                    // Retry only name collisions; writes and script execution are never retried.
                }
            }

            throw new IOException("No free output name after 4096 collisions; choose a different destination.");
        }
        catch (Exception ex)
        {
            if (created)
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    ex.Data["TemporaryPath"] = temporary;
                    ex.Data["CleanupError"] = cleanup.Message;
                }
            }

            throw;
        }
    }

    private static string MakeName(string input, string scriptName, string extension)
    {
        // A display name is never treated as a path, even if it is absolute.
        var safe = new string(scriptName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').Take(60).ToArray());
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "script";
        }

        var stem = Path.GetFileNameWithoutExtension(input);
        return stem[..Math.Min(stem.Length, 80)] + " - " + safe + extension;
    }
}
