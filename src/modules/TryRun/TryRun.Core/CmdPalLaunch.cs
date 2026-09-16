// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

namespace PowerToys.TryRun.Core;

public static class CmdPalLaunch
{
    public static string[] ParseQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length > 4096 || query.Any(char.IsControl))
        {
            throw new ArgumentException("Enter one local path, at most 4096 characters long.");
        }

        var path = query.Trim();
        if (path.Length == 0)
        {
            return [];
        }

        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
        {
            path = path[1..^1];
        }

        if (path.Contains('"') || path.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Use a file path without command-line switches.");
        }

        return TaskBundle.ParseLaunchArguments([WorkspacePath.LocalPath(path)]);
    }

    public static async Task<int> OpenAsync(string application, string[] selection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();
        var unavailable = RuntimeRequirements.GetUnavailableReason();
        if (unavailable is not null)
        {
            throw new InvalidOperationException(unavailable);
        }

        application = RuntimeFile.Resolve(application);
        if (!Path.GetFileName(application).Equals("PowerToys.TryRun.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The Command Palette extension must be beside the Try Run application.");
        }

        var payload = selection.Length == 0 ? null : SelectionPayload.Encode(selection);
        foreach (var path in selection)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException("The selected file or folder no longer exists.", path);
            }
        }

        var start = new ProcessStartInfo(application)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(application),
            RedirectStandardInput = payload is not null,
            StandardInputEncoding = payload is not null ? new UTF8Encoding(false) : null,
        };
        if (payload is not null)
        {
            start.ArgumentList.Add(SelectionPayload.InputSwitch);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(start) ?? throw new IOException("The Try Run window could not be started.");
        if (payload is not null)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch
            {
                // Only terminate the new configuration window if handoff failed.
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }
        }

        return process.Id;
    }
}
