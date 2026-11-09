// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;

namespace PowerScripts.Client;

internal sealed class PowerScriptsHostSession(Func<ProcessStartInfo> createStartInfo) : IDisposable
{
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly object _processLock = new();
    private Process? _process;
    private Task<string>? _diagnostics;
    private bool _disposed;

    internal async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(
        IEnumerable<string> arguments,
        string? input,
        CancellationToken cancellationToken)
    {
        var request = PowerScriptsSessionProtocol.CreateRequest(arguments, input);
        await _requests.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Process process;
            lock (_processLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_process is null)
                {
                    var start = createStartInfo();
                    start.ArgumentList.Add("session");
                    _process = Process.Start(start)
                        ?? throw new InvalidOperationException("Failed to start the PowerScripts Host session.");
                    _diagnostics = _process.StandardError.ReadToEndAsync();
                }

                process = _process;
            }

            await PowerScriptsSessionProtocol.WriteAsync(process.StandardInput.BaseStream, request, cancellationToken).ConfigureAwait(false);
            var response = await PowerScriptsSessionProtocol.ReadAsync(process.StandardOutput.BaseStream, cancellationToken).ConfigureAwait(false)
                ?? throw new EndOfStreamException("The PowerScripts Host session exited before returning a result.");
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            return (
                root.GetProperty("exitCode").GetInt32(),
                root.GetProperty("stdout").GetString() ?? throw new JsonException("Missing Host stdout."),
                root.GetProperty("stderr").GetString() ?? throw new JsonException("Missing Host stderr."));
        }
        catch (Exception exception)
        {
            // Never replay a request: an interrupted transform may already have produced side effects.
            StopProcess();
            if (exception is not (OperationCanceledException or ObjectDisposedException))
            {
                var diagnostics = _diagnostics is null ? string.Empty : await _diagnostics.ConfigureAwait(false);
                throw new InvalidOperationException($"PowerScripts Host session failed: {exception.Message} {diagnostics}", exception);
            }

            throw;
        }
        finally
        {
            _requests.Release();
        }
    }

    public void Dispose()
    {
        lock (_processLock)
        {
            _disposed = true;
        }

        StopProcess();
    }

    private void StopProcess()
    {
        Process? process;
        lock (_processLock)
        {
            process = _process;
            _process = null;
        }

        if (process is not null)
        {
            using (process)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                process.WaitForExit();
            }
        }
    }
}
