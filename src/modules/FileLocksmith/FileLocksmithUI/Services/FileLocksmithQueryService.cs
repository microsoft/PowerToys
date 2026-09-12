// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PowerToys.FileLocksmithUI.Services
{
    internal sealed class FileLocksmithQueryService
    {
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private static readonly Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);

        private const string WorkerExecutableName = "FileLocksmithCLI.exe";
        private const string WorkerArgument = "--worker-json";

        private readonly Func<ProcessStartInfo> _startInfoFactory;
        private readonly TimeSpan _timeout;
        private readonly Action<int>? _processStarted;

        internal FileLocksmithQueryService()
            : this(CreateWorkerStartInfo, DefaultTimeout, null)
        {
        }

        internal FileLocksmithQueryService(
            Func<ProcessStartInfo> startInfoFactory,
            TimeSpan timeout,
            Action<int>? processStarted)
        {
            ArgumentNullException.ThrowIfNull(startInfoFactory);

            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

            _startInfoFactory = startInfoFactory;
            _timeout = timeout;
            _processStarted = processStarted;
        }

        internal async Task<FileLocksmithQueryResult> FindProcessesAsync(
            IReadOnlyCollection<string> paths,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(paths);

            if (paths.Count == 0)
            {
                return new FileLocksmithQueryResult(FileLocksmithQueryStatus.Success, Array.Empty<FileLocksmithProcessInfo>());
            }

            WorkerProcessJob job;
            try
            {
                job = WorkerProcessJob.Create();
            }
            catch (Win32Exception)
            {
                return FailedToStart();
            }

            using var workerJob = job;
            using var process = new Process
            {
                StartInfo = _startInfoFactory(),
            };

            try
            {
                if (!process.Start())
                {
                    return FailedToStart();
                }
            }
            catch (Win32Exception)
            {
                return FailedToStart();
            }
            catch (InvalidOperationException)
            {
                return FailedToStart();
            }

            try
            {
                workerJob.Assign(process);
            }
            catch (Win32Exception)
            {
                await TerminateAsync(process);
                return FailedToStart();
            }

            _processStarted?.Invoke(process.Id);

#pragma warning disable CA2016 // These reads must drain the redirected pipes after timeout cancellation.
            var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
#pragma warning restore CA2016
            var inputClosed = false;

            try
            {
                using var timeoutCancellation = new CancellationTokenSource(_timeout);
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCancellation.Token,
                    cancellationToken);

                try
                {
                    var request = JsonSerializer.Serialize(new WorkerRequest(paths));
                    await process.StandardInput.WriteAsync(request.AsMemory(), linkedCancellation.Token);
                    process.StandardInput.Close();
                    inputClosed = true;

                    await process.WaitForExitAsync(linkedCancellation.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await TerminateAsync(process);
                    await Task.WhenAll(outputTask, errorTask);
                    return new FileLocksmithQueryResult(
                        FileLocksmithQueryStatus.TimedOut,
                        Array.Empty<FileLocksmithProcessInfo>());
                }
                catch (IOException)
                {
                    return Failed(process.HasExited ? process.ExitCode : null);
                }
                catch (InvalidOperationException)
                {
                    return Failed(process.HasExited ? process.ExitCode : null);
                }

                if (process.ExitCode != 0)
                {
                    await Task.WhenAll(outputTask, errorTask);
                    return Failed(process.ExitCode);
                }

                var output = await outputTask;
                await errorTask;

                try
                {
                    var response = JsonSerializer.Deserialize<WorkerResponse>(output);
                    if (response?.Processes is null)
                    {
                        return MalformedOutput();
                    }

                    var processes = new List<FileLocksmithProcessInfo>(response.Processes.Length);
                    foreach (var processInfo in response.Processes)
                    {
                        if (processInfo.Name is null ||
                            processInfo.User is null ||
                            processInfo.Files is null)
                        {
                            return MalformedOutput();
                        }

                        processes.Add(new FileLocksmithProcessInfo(
                            processInfo.Name,
                            processInfo.Pid,
                            processInfo.User,
                            processInfo.Files));
                    }

                    return new FileLocksmithQueryResult(
                        FileLocksmithQueryStatus.Success,
                        processes);
                }
                catch (JsonException)
                {
                    return MalformedOutput();
                }
            }
            finally
            {
                if (!process.HasExited)
                {
                    await TerminateAsync(process);
                }

                if (!inputClosed)
                {
                    process.StandardInput.BaseStream.Dispose();
                }

                await Task.WhenAll(outputTask, errorTask);
            }
        }

        private static ProcessStartInfo CreateWorkerStartInfo()
        {
            var installedPath = Path.Combine(AppContext.BaseDirectory, WorkerExecutableName);
            var buildOutputPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", WorkerExecutableName));
            var workerPath = File.Exists(installedPath) ? installedPath : buildOutputPath;
            return CreateWorkerStartInfo(workerPath);
        }

        internal static ProcessStartInfo CreateWorkerStartInfo(string workerPath)
        {
            var startInfo = new ProcessStartInfo(workerPath)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Utf8WithoutByteOrderMark,
                StandardInputEncoding = Utf8WithoutByteOrderMark,
                StandardOutputEncoding = Utf8WithoutByteOrderMark,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(WorkerArgument);
            return startInfo;
        }

        private static async Task TerminateAsync(Process process)
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }

        private static FileLocksmithQueryResult FailedToStart() =>
            Failed(null);

        private static FileLocksmithQueryResult Failed(int? exitCode) =>
            new(FileLocksmithQueryStatus.Failed, Array.Empty<FileLocksmithProcessInfo>(), exitCode);

        private static FileLocksmithQueryResult MalformedOutput() =>
            new(FileLocksmithQueryStatus.MalformedOutput, Array.Empty<FileLocksmithProcessInfo>());

        private sealed record WorkerRequest([property: JsonPropertyName("paths")] IReadOnlyCollection<string> Paths);

        private sealed class WorkerResponse
        {
            [JsonPropertyName("processes")]
            public WorkerProcessInfo[]? Processes { get; init; }
        }

        private sealed class WorkerProcessInfo
        {
            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("pid")]
            public uint Pid { get; init; }

            [JsonPropertyName("user")]
            public string? User { get; init; }

            [JsonPropertyName("files")]
            public string[]? Files { get; init; }
        }
    }
}
