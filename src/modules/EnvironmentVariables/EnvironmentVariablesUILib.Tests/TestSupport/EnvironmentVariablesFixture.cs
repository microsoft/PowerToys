// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.Telemetry;
using EnvironmentVariablesUILib.ViewModels;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace EnvironmentVariablesUILib.UnitTests.TestSupport
{
    internal sealed class EnvironmentVariablesFixture : IDisposable
    {
        private static int unsafeLifetime;
        private readonly ILogger? originalLogger = LoggerInstance.Logger;
        private readonly IElevationHelper? originalElevation = ElevationHelper.ElevationHelperInstance;
        private readonly ITelemetry? originalTelemetry = TelemetryInstance.Telemetry;

        internal EnvironmentVariablesFixture()
        {
            if (Volatile.Read(ref unsafeLifetime) != 0)
            {
                throw new InvalidOperationException("A previous fixture failed to quiesce. Restart the isolated test host.");
            }

            LoggerInstance.Logger = Logger;
        }

        internal RecordingEnvironment Environment { get; } = new();

        internal RecordingLogger Logger { get; } = new();

        internal ControlledWork Work { get; } = new();

        internal RecordingProfilesService Service { get; } = new();

        internal RecordingTelemetry Telemetry { get; } = new();

        internal MainViewModel CreateViewModel(bool elevated = false, IEnvironmentVariablesService? service = null)
        {
            Environment.Expand = value => value;
            return new MainViewModel(
                new FixedElevation(elevated),
                service ?? Service,
                Logger,
                Telemetry,
                Work.EnqueueRefresh,
                new DefaultVariablesSet(VariablesSet.UserGuid, "User", VariablesSetType.User),
                new DefaultVariablesSet(VariablesSet.SystemGuid, "System", VariablesSetType.System),
                new DefaultVariablesSet(Guid.NewGuid(), "Defaults", VariablesSetType.User),
                Environment,
                Work.Run,
                Work.RunAsync);
        }

        // No work survives this delegate: model actions finish before the returned task.
        internal static Task RunImmediately(Action action)
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        }

        public void Dispose()
        {
            try
            {
                Work.SettleAsync().GetAwaiter().GetResult();
            }
            finally
            {
                // Never restore shared holders underneath an unobserved continuation.
                if (Work.IsQuiescent)
                {
                    Work.Dispose();
                    LoggerInstance.Logger = originalLogger!;
                    ElevationHelper.ElevationHelperInstance = originalElevation!;
                    TelemetryInstance.Telemetry = originalTelemetry!;
                }
                else
                {
                    Interlocked.Exchange(ref unsafeLifetime, 1);
                }
            }
        }

        internal sealed class ControlledWork : IDisposable
        {
            private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
            private readonly List<(Action Action, TaskCompletionSource<bool> Completion)> actions = new();
            private readonly List<(Func<Task> Function, TaskCompletionSource<bool> Completion)> saves = new();
            private readonly List<Task> startedSaves = new();
            private readonly ConcurrentQueue<Action> refreshes = new();
            private readonly SemaphoreSlim receipts = new(0);
            private int receivedCount;
            private int settledActions;

            internal int ActionCount => actions.Count;

            internal int SaveCount => saves.Count;

            internal int RefreshReceiptCount => Volatile.Read(ref receivedCount);

            internal bool AcceptRefresh { get; set; } = true;

            internal bool IsQuiescent => settledActions == actions.Count
                && saves.All(x => x.Completion.Task.IsCompleted)
                && startedSaves.All(x => x.IsCompleted) && refreshes.IsEmpty;

            public void Dispose()
            {
                receipts.Dispose();
            }

            internal Task Run(Action action)
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                actions.Add((action, completion));
                return completion.Task;
            }

            internal Task RunAsync(Func<Task> function)
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                saves.Add((function, completion));
                return completion.Task;
            }

            internal bool EnqueueRefresh(Action action)
            {
                var accepted = AcceptRefresh;
                if (accepted)
                {
                    refreshes.Enqueue(action);
                }

                Interlocked.Increment(ref receivedCount);
                receipts.Release();
                return accepted;
            }

            internal async Task CompleteActionAsync(int index)
            {
                var work = actions[index];
                if (work.Completion.Task.IsCompleted)
                {
                    throw new InvalidOperationException("This work item was already released.");
                }

                try
                {
                    work.Action();
                    work.Completion.SetResult(true);
                }
                catch (Exception exception)
                {
                    work.Completion.SetException(exception);
                }

                if (!await receipts.WaitAsync(Timeout).ConfigureAwait(false))
                {
                    throw new TimeoutException("VM continuation did not report its unconditional enqueue attempt.");
                }

                settledActions++;
                await work.Completion.Task.ConfigureAwait(false);
            }

            internal async Task CompleteSaveAsync(int index)
            {
                var work = saves[index];
                if (work.Completion.Task.IsCompleted)
                {
                    throw new InvalidOperationException("This save was already released.");
                }

                try
                {
                    var task = work.Function();
                    startedSaves.Add(task);
                    await task.WaitAsync(Timeout).ConfigureAwait(false);
                    work.Completion.SetResult(true);
                }
                catch (Exception exception)
                {
                    work.Completion.SetException(exception);
                }

                await work.Completion.Task.ConfigureAwait(false);
            }

            internal void DrainRefreshes()
            {
                while (refreshes.TryDequeue(out var action))
                {
                    action();
                }
            }

            internal async Task SettleAsync()
            {
                var failures = new List<Exception>();
                for (var index = 0; index < actions.Count; index++)
                {
                    if (!actions[index].Completion.Task.IsCompleted)
                    {
                        try
                        {
                            await CompleteActionAsync(index).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }

                for (var index = 0; index < saves.Count; index++)
                {
                    if (!saves[index].Completion.Task.IsCompleted)
                    {
                        try
                        {
                            await CompleteSaveAsync(index).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }

                DrainRefreshes();
                if (!IsQuiescent)
                {
                    failures.Add(new InvalidOperationException("Owned work is not quiescent; shared holders were not restored."));
                }

                if (failures.Count > 0)
                {
                    throw new AggregateException("Fixture cleanup failed.", failures);
                }
            }
        }

        internal sealed class ScriptedProfileFileSystem : IFileSystem, IFile
        {
            internal Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

            internal List<(string Operation, string? Path)> Calls { get; } = new();

            internal List<(string Path, string? Contents, CancellationToken CancellationToken)> Writes { get; } = new();

            internal Exception? ReadFailure { get; set; }

            internal Exception? WriteFailure { get; set; }

            internal Func<string?, bool>? ExistsCallback { get; set; }

            internal Func<string, string>? ReadAllTextCallback { get; set; }

            internal Func<string, string?, CancellationToken, Task>? WriteAllTextAsyncCallback { get; set; }

            IFile IFileSystem.File => this;

            IFileSystem IFileSystemEntity.FileSystem => this;

            bool IFile.Exists(string? path)
            {
                Calls.Add(("Exists", path));
                if (ExistsCallback != null)
                {
                    return ExistsCallback(path);
                }

                return path != null && Files.ContainsKey(path);
            }

            string IFile.ReadAllText(string path)
            {
                Calls.Add(("ReadAllText", path));
                if (ReadFailure != null)
                {
                    throw ReadFailure;
                }

                return ReadAllTextCallback != null ? ReadAllTextCallback(path) : Files[path];
            }

            Task IFile.WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken)
            {
                Calls.Add(("WriteAllTextAsync", path));
                Writes.Add((path, contents, cancellationToken));
                if (WriteFailure != null)
                {
                    return Task.FromException(WriteFailure);
                }

                if (WriteAllTextAsyncCallback != null)
                {
                    return WriteAllTextAsyncCallback(path, contents, cancellationToken);
                }

                Files[path] = contents ?? string.Empty;
                return Task.CompletedTask;
            }

            IDirectory IFileSystem.Directory => throw new NotSupportedException();

            IDirectoryInfoFactory IFileSystem.DirectoryInfo => throw new NotSupportedException();

            IDriveInfoFactory IFileSystem.DriveInfo => throw new NotSupportedException();

            IFileInfoFactory IFileSystem.FileInfo => throw new NotSupportedException();

            IFileStreamFactory IFileSystem.FileStream => throw new NotSupportedException();

            IFileSystemWatcherFactory IFileSystem.FileSystemWatcher => throw new NotSupportedException();

            IFileVersionInfoFactory IFileSystem.FileVersionInfo => throw new NotSupportedException();

            IPath IFileSystem.Path => throw new NotSupportedException();

            void IFile.AppendAllBytes(string path, byte[] bytes) => throw new NotSupportedException();

            void IFile.AppendAllBytes(string path, ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

            Task IFile.AppendAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.AppendAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => throw new NotSupportedException();

            void IFile.AppendAllLines(string path, IEnumerable<string> contents) => throw new NotSupportedException();

            void IFile.AppendAllLines(string path, IEnumerable<string> contents, Encoding encoding) => throw new NotSupportedException();

            Task IFile.AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            void IFile.AppendAllText(string path, string? contents) => throw new NotSupportedException();

            void IFile.AppendAllText(string path, string? contents, Encoding encoding) => throw new NotSupportedException();

            void IFile.AppendAllText(string path, ReadOnlySpan<char> contents) => throw new NotSupportedException();

            void IFile.AppendAllText(string path, ReadOnlySpan<char> contents, Encoding encoding) => throw new NotSupportedException();

            Task IFile.AppendAllTextAsync(string path, string? contents, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.AppendAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.AppendAllTextAsync(string path, ReadOnlyMemory<char> contents, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.AppendAllTextAsync(string path, ReadOnlyMemory<char> contents, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            StreamWriter IFile.AppendText(string path) => throw new NotSupportedException();

            void IFile.Copy(string sourceFileName, string destFileName) => throw new NotSupportedException();

            void IFile.Copy(string sourceFileName, string destFileName, bool overwrite) => throw new NotSupportedException();

            FileSystemStream IFile.Create(string path) => throw new NotSupportedException();

            FileSystemStream IFile.Create(string path, int bufferSize) => throw new NotSupportedException();

            FileSystemStream IFile.Create(string path, int bufferSize, FileOptions options) => throw new NotSupportedException();

            IFileSystemInfo IFile.CreateSymbolicLink(string path, string pathToTarget) => throw new NotSupportedException();

            StreamWriter IFile.CreateText(string path) => throw new NotSupportedException();

            void IFile.Decrypt(string path) => throw new NotSupportedException();

            void IFile.Delete(string path) => throw new NotSupportedException();

            void IFile.Encrypt(string path) => throw new NotSupportedException();

            FileAttributes IFile.GetAttributes(string path) => throw new NotSupportedException();

            FileAttributes IFile.GetAttributes(SafeFileHandle fileHandle) => throw new NotSupportedException();

            DateTime IFile.GetCreationTime(string path) => throw new NotSupportedException();

            DateTime IFile.GetCreationTime(SafeFileHandle fileHandle) => throw new NotSupportedException();

            DateTime IFile.GetCreationTimeUtc(string path) => throw new NotSupportedException();

            DateTime IFile.GetCreationTimeUtc(SafeFileHandle fileHandle) => throw new NotSupportedException();

            DateTime IFile.GetLastAccessTime(string path) => throw new NotSupportedException();

            DateTime IFile.GetLastAccessTime(SafeFileHandle fileHandle) => throw new NotSupportedException();

            DateTime IFile.GetLastAccessTimeUtc(string path) => throw new NotSupportedException();

            DateTime IFile.GetLastAccessTimeUtc(SafeFileHandle fileHandle) => throw new NotSupportedException();

            DateTime IFile.GetLastWriteTime(string path) => throw new NotSupportedException();

            DateTime IFile.GetLastWriteTime(SafeFileHandle fileHandle) => throw new NotSupportedException();

            DateTime IFile.GetLastWriteTimeUtc(string path) => throw new NotSupportedException();

            DateTime IFile.GetLastWriteTimeUtc(SafeFileHandle fileHandle) => throw new NotSupportedException();

            UnixFileMode IFile.GetUnixFileMode(string path) => throw new NotSupportedException();

            UnixFileMode IFile.GetUnixFileMode(SafeFileHandle fileHandle) => throw new NotSupportedException();

            void IFile.Move(string sourceFileName, string destFileName) => throw new NotSupportedException();

            void IFile.Move(string sourceFileName, string destFileName, bool overwrite) => throw new NotSupportedException();

            FileSystemStream IFile.Open(string path, FileMode mode) => throw new NotSupportedException();

            FileSystemStream IFile.Open(string path, FileMode mode, FileAccess access) => throw new NotSupportedException();

            FileSystemStream IFile.Open(string path, FileMode mode, FileAccess access, FileShare share) => throw new NotSupportedException();

            FileSystemStream IFile.Open(string path, FileStreamOptions options) => throw new NotSupportedException();

            FileSystemStream IFile.OpenRead(string path) => throw new NotSupportedException();

            StreamReader IFile.OpenText(string path) => throw new NotSupportedException();

            FileSystemStream IFile.OpenWrite(string path) => throw new NotSupportedException();

            byte[] IFile.ReadAllBytes(string path) => throw new NotSupportedException();

            Task<byte[]> IFile.ReadAllBytesAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();

            string[] IFile.ReadAllLines(string path) => throw new NotSupportedException();

            string[] IFile.ReadAllLines(string path, Encoding encoding) => throw new NotSupportedException();

            Task<string[]> IFile.ReadAllLinesAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task<string[]> IFile.ReadAllLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            string IFile.ReadAllText(string path, Encoding encoding) => throw new NotSupportedException();

            Task<string> IFile.ReadAllTextAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task<string> IFile.ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            IEnumerable<string> IFile.ReadLines(string path) => throw new NotSupportedException();

            IEnumerable<string> IFile.ReadLines(string path, Encoding encoding) => throw new NotSupportedException();

            IAsyncEnumerable<string> IFile.ReadLinesAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();

            IAsyncEnumerable<string> IFile.ReadLinesAsync(string path, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            void IFile.Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName) => throw new NotSupportedException();

            void IFile.Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName, bool ignoreMetadataErrors) => throw new NotSupportedException();

            IFileSystemInfo? IFile.ResolveLinkTarget(string linkPath, bool returnFinalTarget) => throw new NotSupportedException();

            void IFile.SetAttributes(string path, FileAttributes fileAttributes) => throw new NotSupportedException();

            void IFile.SetAttributes(SafeFileHandle fileHandle, FileAttributes fileAttributes) => throw new NotSupportedException();

            void IFile.SetCreationTime(string path, DateTime creationTime) => throw new NotSupportedException();

            void IFile.SetCreationTime(SafeFileHandle fileHandle, DateTime creationTime) => throw new NotSupportedException();

            void IFile.SetCreationTimeUtc(string path, DateTime creationTimeUtc) => throw new NotSupportedException();

            void IFile.SetCreationTimeUtc(SafeFileHandle fileHandle, DateTime creationTimeUtc) => throw new NotSupportedException();

            void IFile.SetLastAccessTime(string path, DateTime lastAccessTime) => throw new NotSupportedException();

            void IFile.SetLastAccessTime(SafeFileHandle fileHandle, DateTime lastAccessTime) => throw new NotSupportedException();

            void IFile.SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => throw new NotSupportedException();

            void IFile.SetLastAccessTimeUtc(SafeFileHandle fileHandle, DateTime lastAccessTimeUtc) => throw new NotSupportedException();

            void IFile.SetLastWriteTime(string path, DateTime lastWriteTime) => throw new NotSupportedException();

            void IFile.SetLastWriteTime(SafeFileHandle fileHandle, DateTime lastWriteTime) => throw new NotSupportedException();

            void IFile.SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => throw new NotSupportedException();

            void IFile.SetLastWriteTimeUtc(SafeFileHandle fileHandle, DateTime lastWriteTimeUtc) => throw new NotSupportedException();

            void IFile.SetUnixFileMode(string path, UnixFileMode mode) => throw new NotSupportedException();

            void IFile.SetUnixFileMode(SafeFileHandle fileHandle, UnixFileMode mode) => throw new NotSupportedException();

            void IFile.WriteAllBytes(string path, byte[] bytes) => throw new NotSupportedException();

            void IFile.WriteAllBytes(string path, ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

            Task IFile.WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.WriteAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) => throw new NotSupportedException();

            void IFile.WriteAllLines(string path, string[] contents) => throw new NotSupportedException();

            void IFile.WriteAllLines(string path, IEnumerable<string> contents) => throw new NotSupportedException();

            void IFile.WriteAllLines(string path, string[] contents, Encoding encoding) => throw new NotSupportedException();

            void IFile.WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding) => throw new NotSupportedException();

            Task IFile.WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            void IFile.WriteAllText(string path, string? contents) => throw new NotSupportedException();

            void IFile.WriteAllText(string path, string? contents, Encoding encoding) => throw new NotSupportedException();

            void IFile.WriteAllText(string path, ReadOnlySpan<char> contents) => throw new NotSupportedException();

            void IFile.WriteAllText(string path, ReadOnlySpan<char> contents, Encoding encoding) => throw new NotSupportedException();

            Task IFile.WriteAllTextAsync(string path, string? contents, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.WriteAllTextAsync(string path, ReadOnlyMemory<char> contents, CancellationToken cancellationToken) => throw new NotSupportedException();

            Task IFile.WriteAllTextAsync(string path, ReadOnlyMemory<char> contents, Encoding encoding, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        internal sealed class RecordingProfilesService : IEnvironmentVariablesService
        {
            internal Func<List<ProfileVariablesSet>> Read { get; set; } = () => new();

            internal Exception? WriteFailure { get; set; }

            internal List<ProfileVariablesSet[]> Writes { get; } = new();

            public string ProfilesJsonFilePath => "scripted-profiles.json";

            public List<ProfileVariablesSet> ReadProfiles() => Read();

            public Task WriteAsync(IEnumerable<ProfileVariablesSet> profiles)
            {
                Writes.Add(profiles.ToArray());
                return WriteFailure == null ? Task.CompletedTask : Task.FromException(WriteFailure);
            }

            public void Dispose()
            {
            }
        }

        internal sealed class RecordingTelemetry : ITelemetry
        {
            internal List<bool> Enabled { get; } = new();

            internal List<VariablesSetType> Changed { get; } = new();

            public void LogEnvironmentVariablesProfileEnabledEvent(bool enabled) => Enabled.Add(enabled);

            public void LogEnvironmentVariablesVariableChangedEvent(VariablesSetType type) => Changed.Add(type);
        }

        internal sealed class FixedElevation : IElevationHelper
        {
            internal FixedElevation(bool elevated)
            {
                IsElevated = elevated;
            }

            public bool IsElevated { get; }
        }

        internal sealed class RecordingEnvironment : IEnvironmentVariablesEnvironment
        {
            internal Dictionary<string, object?> User { get; } = new(StringComparer.OrdinalIgnoreCase);

            internal Dictionary<string, object?> Machine { get; } = new(StringComparer.OrdinalIgnoreCase);

            internal HashSet<RegistryHive> MissingHives { get; } = new();

            internal List<(RegistryHive Hive, string SubKey, bool Writable)> Opens { get; } = new();

            internal List<RecordingKey> Keys { get; } = new();

            internal List<(RegistryHive Hive, string Operation, string Name, object? Value)> Operations { get; } = new();

            internal List<(IntPtr Window, int Message, IntPtr WParam, string LParam)> Notifications { get; } = new();

            internal Action<string, RegistryHive, string>? BeforeOperation { get; set; }

            internal Func<string, string>? Expand { get; set; }

            internal Action? BeforeNotify { get; set; }

            internal int NotifyResult { get; set; }

            public IEnvironmentVariablesRegistryKey? OpenKey(RegistryHive hive, string subKey, bool writable)
            {
                Opens.Add((hive, subKey, writable));
                BeforeOperation?.Invoke("Open", hive, subKey);
                if (MissingHives.Contains(hive))
                {
                    return null;
                }

                var values = hive switch
                {
                    RegistryHive.CurrentUser => User,
                    RegistryHive.LocalMachine => Machine,
                    _ => throw new NotSupportedException(),
                };
                var key = new RecordingKey(this, hive, values);
                Keys.Add(key);
                return key;
            }

            public string ExpandEnvironmentVariables(string value)
            {
                return (Expand ?? throw new NotSupportedException("Expansion was not configured."))(value);
            }

            public int SendNotifyMessage(IntPtr window, int message, IntPtr wParam, string lParam)
            {
                BeforeNotify?.Invoke();
                Notifications.Add((window, message, wParam, lParam));
                return NotifyResult;
            }
        }

        internal sealed class RecordingKey : IEnvironmentVariablesRegistryKey
        {
            private readonly RecordingEnvironment environment;
            private readonly RegistryHive hive;
            private readonly Dictionary<string, object?> values;

            internal RecordingKey(RecordingEnvironment environment, RegistryHive hive, Dictionary<string, object?> values)
            {
                this.environment = environment;
                this.hive = hive;
                this.values = values;
            }

            internal bool IsDisposed { get; private set; }

            public string[] GetValueNames()
            {
                Record("Names", string.Empty, null);
                return values.Keys.ToArray();
            }

            public object? GetValue(string name, object defaultValue, RegistryValueOptions options)
            {
                Record("Read", name, (defaultValue, options));
                return values.TryGetValue(name, out var value) ? value : defaultValue;
            }

            public void SetValue(string name, object value, RegistryValueKind kind)
            {
                Record("Write", name, (value, kind));
                values[name] = value;
            }

            public void DeleteValue(string name, bool throwOnMissingValue)
            {
                Record("Delete", name, throwOnMissingValue);
                if (!values.Remove(name) && throwOnMissingValue)
                {
                    throw new ArgumentException("Missing scripted value.", nameof(name));
                }
            }

            public void Dispose()
            {
                IsDisposed = true;
                environment.Operations.Add((hive, "Dispose", string.Empty, null));
            }

            private void Record(string operation, string name, object? value)
            {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                environment.Operations.Add((hive, operation, name, value));
                environment.BeforeOperation?.Invoke(operation, hive, name);
            }
        }

        internal sealed class RecordingLogger : ILogger
        {
            internal List<(string Level, string Message, Exception? Exception)> Entries { get; } = new();

            public void LogError(string message) => Entries.Add(("Error", message, null));

            public void LogError(string message, Exception ex) => Entries.Add(("Error", message, ex));

            public void LogWarning(string message) => Entries.Add(("Warning", message, null));

            public void LogInfo(string message) => Entries.Add(("Info", message, null));

            public void LogDebug(string message) => Entries.Add(("Debug", message, null));

            public void LogTrace() => Entries.Add(("Trace", string.Empty, null));
        }
    }
}
