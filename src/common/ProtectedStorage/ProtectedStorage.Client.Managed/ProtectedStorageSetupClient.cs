// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PowerToys.ProtectedStorage;

/// <summary>Fixed sibling Setup entrypoints. Ensure and repair require explicit user action.</summary>
public sealed class ProtectedStorageSetupClient : IProtectedStorageSetupClient
{
    public Task<MaintenanceResult> InspectAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["inspect", "--json"], cancellationToken);

    public Task<MaintenanceResult> SyncAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["sync", "--json"], cancellationToken);

    public Task<MaintenanceResult> EnsureReadyAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["ensure", "--json"], cancellationToken);

    public Task<MaintenanceResult> RepairAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["repair", "--json"], cancellationToken);

    public Task<MaintenanceResult> RepairBootstrapWithAuthorizationAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["repair", "--authorize-repair", "--json"], cancellationToken);

    public Task<MaintenanceResult> RetryFailedOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("A maintenance operation ID is required.", nameof(operationId));
        }

        return RunAsync(["retry", operationId.ToString("N").ToUpperInvariant(), "--json"], cancellationToken);
    }

    public static string FindSetupPath(string hostDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostDirectory);
        for (var directory = new DirectoryInfo(Path.GetFullPath(hostDirectory)); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PowerToys.exe")))
            {
                var setup = Path.Combine(directory.FullName, "PowerToys.ProtectedStorageSetup.exe");
                if (File.Exists(setup))
                {
                    return setup;
                }

                break;
            }
        }

        throw new ProtectedStorageException("SetupUnavailable");
    }

    private static async Task<MaintenanceResult> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientIdentity.RequireNormalMaintenanceOwner();

        var start = new ProcessStartInfo(FindSetupPath(AppContext.BaseDirectory))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
            {
                throw new ProtectedStorageException("SetupUnavailable");
            }
        }
        catch (Win32Exception exception)
        {
            throw new ProtectedStorageException("SetupUnavailable", nativeCode: exception.NativeErrorCode, innerException: exception);
        }

        var output = ReadTailAsync(process.StandardOutput);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            // Cancellation is not proof that an MSI operation did not commit. Never kill or replay it.
            _ = output.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw new ProtectedStorageException("SetupOutcomeUnknown", innerException: exception);
        }

        try
        {
            return MaintenanceResult.Parse(process.ExitCode, await output.ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new ProtectedStorageException("SetupOutcomeUnknown", nativeCode: process.ExitCode, innerException: exception);
        }
    }

    private static async Task<string> ReadTailAsync(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
        {
            text.Append(buffer, 0, count);
            if (text.Length > 65536)
            {
                text.Remove(0, text.Length - 65536);
            }
        }

        return text.ToString();
    }
}
