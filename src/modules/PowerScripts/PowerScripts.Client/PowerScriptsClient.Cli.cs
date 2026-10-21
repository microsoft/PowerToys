// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PowerScripts.Client;

public sealed partial class PowerScriptsClient
{
    /// <summary>
    /// Requests the host's built-in CLI catalog. Failures are returned explicitly rather than
    /// appearing as an empty catalog. The scripts-root override does not apply to built-in CLIs.
    /// </summary>
    public async Task<CliCatalogResult> GetCliCommandsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable)
        {
            return new CliCatalogResult { Error = "The PowerScripts host is unavailable." };
        }

        var response = await InvokeCliHostAsync(["cli", "list", "--json"], 5000, cancellationToken).ConfigureAwait(false);
        if (response.Error is not null)
        {
            return new CliCatalogResult { Error = response.Error };
        }

        return CliCatalogResult.Parse(response.StdOut, response.ExitCode);
    }

    /// <summary>
    /// Runs a built-in CLI command through the host and returns its structured result, including
    /// failures with a nonzero process exit code. <paramref name="timeoutMs"/> must be 1–120000.
    /// Caller cancellation throws; a client timeout returns a failed result with status <c>timeout</c>.
    /// </summary>
    public async Task<CliRunResult> RunCliAsync(
        string id,
        IReadOnlyDictionary<string, string?> parameters,
        int timeoutMs = 60000,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var arguments = GetCliRunArguments(id, parameters);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMs, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeoutMs, 120000);

        if (!IsAvailable)
        {
            return new CliRunResult
            {
                Status = "unavailable",
                ExitCode = PowerScriptsProtocol.ExitCode.Error,
                Error = "The PowerScripts host is unavailable.",
            };
        }

        arguments.Add("--timeout-ms");
        arguments.Add(timeoutMs.ToString(CultureInfo.InvariantCulture));
        arguments.Add("--json");

        // Give the host time to terminate its CLI child and emit a structured timeout result.
        var response = await InvokeCliHostAsync(arguments, timeoutMs + 5000, cancellationToken).ConfigureAwait(false);
        if (response.Error is not null)
        {
            return new CliRunResult
            {
                Status = response.TimedOut ? "timeout" : "error",
                ExitCode = response.ExitCode,
                StdErr = response.StdErr,
                Error = response.Error,
            };
        }

        return CliRunResult.Parse(response.StdOut, response.ExitCode, response.StdErr);
    }

    /// <summary>
    /// Builds arguments for a background shortcut invocation. The command ID is restricted to
    /// letters, digits, periods, underscores and hyphens; sorted parameter pairs are UTF-8/base64
    /// encoded so quotes, newlines and non-ASCII text cannot become additional command arguments.
    /// The returned command is non-interactive, has a 60-second timeout and does not request JSON output.
    /// </summary>
    public static string BuildCliRunArguments(string id, IReadOnlyDictionary<string, string?> parameters)
    {
        var arguments = GetCliRunArguments(id, parameters);
        arguments.Add("--timeout-ms");
        arguments.Add("60000");
        return string.Join(' ', arguments);
    }

    private static List<string> GetCliRunArguments(string id, IReadOnlyDictionary<string, string?> parameters)
    {
        if (!CliCommandInfo.IsValidId(id))
        {
            throw new ArgumentException("A CLI command ID may contain only ASCII letters, digits, periods, underscores and hyphens.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(parameters);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<string> { "cli", "run", id };
        foreach (var (name, value) in parameters.OrderBy(parameter => parameter.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(name) || name.Contains('\0') || value?.Contains('\0') == true || !names.Add(name))
            {
                throw new ArgumentException("CLI parameters must have nonempty, unique names and cannot contain NUL characters.", nameof(parameters));
            }

            arguments.Add("--set-base64");
            arguments.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(name + '\0' + (value ?? string.Empty))));
        }

        arguments.Add("--no-consent");
        return arguments;
    }

    private async Task<CliHostResponse> InvokeCliHostAsync(
        IEnumerable<string> arguments,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = CreateStartInfo(redirectStdOut: true, redirectStdErr: true) };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var started = false;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started)
            {
                return new CliHostResponse(string.Empty, string.Empty, PowerScriptsProtocol.ExitCode.Error, false, "Failed to start the PowerScripts host.");
            }

            var stdOut = process.StandardOutput.ReadToEndAsync(linked.Token);
            var stdErr = process.StandardError.ReadToEndAsync(linked.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                await Task.WhenAll(stdOut, stdErr).ConfigureAwait(false);
                return new CliHostResponse(await stdOut, await stdErr, process.ExitCode, false, null);
            }
            catch (OperationCanceledException)
            {
                var stopped = await StopCliHostAsync(process).ConfigureAwait(false);
                try
                {
                    await Task.WhenAll(stdOut, stdErr).WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Observe the canceled pipe reads after terminating the host.
                }

                cancellationToken.ThrowIfCancellationRequested();
                return TimeoutResponse(stopped);
            }
        }
        catch (OperationCanceledException)
        {
            var stopped = !started || await StopCliHostAsync(process).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return TimeoutResponse(stopped);
        }
        catch (Exception exception)
        {
            var stopped = !started || await StopCliHostAsync(process).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var terminationError = stopped ? string.Empty : " Host termination could not be confirmed.";
            return new CliHostResponse(string.Empty, string.Empty, PowerScriptsProtocol.ExitCode.Error, false, $"Could not communicate with the PowerScripts host: {exception.Message}{terminationError}");
        }
    }

    private static async Task<bool> StopCliHostAsync(Process process)
    {
        TryKill(process);
        using var cleanupTimeout = new CancellationTokenSource(2000);
        try
        {
            await process.WaitForExitAsync(cleanupTimeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static CliHostResponse TimeoutResponse(bool stopped) => new(
        string.Empty,
        string.Empty,
        PowerScriptsProtocol.ExitCode.Timeout,
        true,
        stopped ? "The PowerScripts host timed out." : "The PowerScripts host timed out and its termination could not be confirmed.");

    private sealed record CliHostResponse(string StdOut, string StdErr, int ExitCode, bool TimedOut, string? Error);
}
