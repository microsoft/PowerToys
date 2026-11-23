// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesEditorIO
    {
        private readonly Func<ProcessStartInfo, TimeSpan?, Task<bool>> _runProcess;
        private readonly ConditionalWeakTable<Project, JsonObject> _storedRows = new();
        private (long Length, byte[] Hash)? _publishedCapture;

        public WorkspacesEditorIO()
            : this(TempProjectData.File, RunProcessAsync)
        {
        }

        internal WorkspacesEditorIO(string tempProjectPath, Func<ProcessStartInfo, TimeSpan?, Task<bool>> runProcess)
        {
            TempProjectPath = tempProjectPath;
            _runProcess = runProcess;
        }

        internal string TempProjectPath { get; }

        private string WorkspacePath => Path.Combine(Path.GetDirectoryName(TempProjectPath), "workspaces.json");

        internal Task<bool> LaunchAsync(string projectId, InvokePoint invokePoint, Guid? draftRequest = null)
        {
            string arguments = $"{projectId} {(int)invokePoint}";
            if (draftRequest is Guid requestId)
            {
                arguments += $" --editor-draft={RequestToken(requestId)}";
            }

            return RunToolAsync("PowerToys.WorkspacesLauncher.exe", arguments, null);
        }

        internal Task<bool> CaptureAsync(Guid requestId, bool recapturing) =>
            RunToolAsync("PowerToys.WorkspacesSnapshotTool.exe", $"{RequestToken(requestId)} {(int)(recapturing ? InvokePoint.LaunchAndEdit : InvokePoint.EditorButton)}", TimeSpan.FromMinutes(2));

        // The native tools use StringFromCLSID for their per-request filenames.
        private static string RequestToken(Guid requestId) => requestId.ToString("B").ToUpperInvariant();

        private Task<bool> RunToolAsync(string executable, string arguments, TimeSpan? timeout)
        {
            string editorDirectory = Path.GetDirectoryName(Environment.ProcessPath);
            string tool = Path.Combine(Path.GetDirectoryName(editorDirectory), executable);
            if (!File.Exists(tool))
            {
                tool = Path.Combine(editorDirectory, executable);
            }

            var startInfo = ModuleProcess.CreateStartInfo(tool, arguments);
            startInfo.WorkingDirectory = Path.GetDirectoryName(tool);
            return _runProcess(startInfo, timeout);
        }

        private static async Task<bool> RunProcessAsync(ProcessStartInfo startInfo, TimeSpan? timeout)
        {
            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
                using var deadline = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
                try
                {
                    await process.WaitForExitAsync(deadline?.Token ?? CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (deadline?.IsCancellationRequested == true)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }

                    await process.WaitForExitAsync().ConfigureAwait(false);
                    Logger.LogWarning($"Workspace tool timed out: {startInfo.FileName}");
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to run workspace tool {startInfo.FileName}", ex);
                return false;
            }
        }

        public ParsingResult ParseWorkspaces(MainViewModel mainViewModel) => ParseWorkspaces(mainViewModel.Workspaces);

        internal bool CanReadWorkspaces() => ParseWorkspaces((ICollection<Project>)null).Result;

        private ParsingResult ParseWorkspaces(ICollection<Project> destination)
        {
            try
            {
                var document = IOUtils.ReadWorkspaceFile(WorkspacePath);
                if (document == null)
                {
                    destination?.Clear();
                    return new ParsingResult(true);
                }

                var rows = document["workspaces"]?.AsArray() ?? throw new InvalidDataException("Missing workspace list.");
                return new ParsingResult(SetWorkspaces(destination, rows));
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception while parsing storage file: {e.Message}");
                return new ParsingResult(false, e.Message);
            }
        }

        public Project ParseTempProject() => ReadProject(TempProjectPath);

        internal string RequestPath(Guid requestId) => $"{TempProjectPath}.{RequestToken(requestId)}";

        internal void CleanupOrphanedRequests()
        {
            var folder = new DirectoryInfo(Path.GetDirectoryName(TempProjectPath));
            string prefix = Path.GetFileName(TempProjectPath) + ".";

            // A previous editor may have exited before it could remove a launch input or capture output.
            DateTime cutoff = DateTime.UtcNow.AddHours(-1);
            try
            {
                if (!folder.Exists)
                {
                    return;
                }

                foreach (FileInfo file in folder.EnumerateFiles(prefix + "{*}", SearchOption.TopDirectoryOnly))
                {
                    if (file.Name.Length == prefix.Length + 38 &&
                        Guid.TryParseExact(file.Name.AsSpan(prefix.Length), "B", out _) &&
                        !file.Attributes.HasFlag(FileAttributes.ReparsePoint) &&
                        file.LastWriteTimeUtc < cutoff)
                    {
                        DeleteTemporaryFile(file.FullName);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("Failed to clean up old workspace requests", ex);
            }
        }

        internal Project AcceptCapture(Guid requestId)
        {
            string path = RequestPath(requestId);
            Project project = ReadProject(path);
            if (project == null || !Guid.TryParse(project.Id, out var actualId) || actualId != requestId)
            {
                Logger.LogWarning("Discarded a workspace snapshot that does not match the capture request.");
                return null;
            }

            // Older observers use this shared filename. The editor already has the verified
            // per-request result even if another reader temporarily prevents replacement.
            try
            {
                using var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                var fingerprint = (source.Length, SHA256.HashData(source));
                File.Move(path, TempProjectPath, overwrite: true);
                _publishedCapture = fingerprint;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("Failed to publish the shared workspace snapshot", ex);
            }

            return project;
        }

        internal void DiscardPublishedCapture()
        {
            if (_publishedCapture is not { } fingerprint)
            {
                return;
            }

            try
            {
                // Another writer may use the same project ID for its own edited draft.
                WorkspaceShortcutFile.DeleteIf(TempProjectPath, stream =>
                    stream.Length == fingerprint.Length && SHA256.HashData(stream).AsSpan().SequenceEqual(fingerprint.Hash));
                _publishedCapture = null;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                _publishedCapture = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
            {
                Logger.LogError("Failed to remove the published workspace snapshot", ex);
            }
        }

        private static Project ReadProject(string path)
        {
            try
            {
                ProjectData parser = new();
                if (!File.Exists(path))
                {
                    Logger.LogWarning($"Workspaces storage file not found: {path}");
                    return null;
                }

                Project project = new(parser.Read(path));
                return project;
            }
            catch (Exception e)
            {
                Logger.LogError($"ParseProject method. Exception while parsing storage file: {e.Message}");
                return null;
            }
        }

        public void SerializeWorkspaces(List<Project> workspaces, bool useTempFile = false)
        {
            if (useTempFile)
            {
                WriteDraft(workspaces.Single(), TempProjectPath);
                return;
            }

            WorkspacesData serializer = new();
            var data = new WorkspacesData.WorkspacesListWrapper { Workspaces = workspaces.Select(ToWrapper).ToList() };
            new IOUtils().WriteFile(WorkspacePath, serializer.Serialize(data));
        }

        internal IReadOnlyCollection<string> GetStoredWorkspaceIds()
        {
            try
            {
                var document = IOUtils.ReadWorkspaceFile(WorkspacePath);
                if (document == null)
                {
                    return Array.Empty<string>();
                }

                if (document["workspaces"] is not JsonArray rows)
                {
                    return null;
                }

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    if (row is not JsonObject workspace || workspace["id"] is not JsonValue value || !value.TryGetValue<string>(out var id) ||
                        !(Guid.TryParseExact(id, "B", out var guid) || Guid.TryParseExact(id, "D", out guid)) || !ids.Add(guid.ToString("B")))
                    {
                        return null;
                    }
                }

                return ids;
            }
            catch (Exception ex)
            {
                Logger.LogError("Could not verify the stored workspace IDs.", ex);
                return null;
            }
        }

        internal void PersistChange(Project original, Project replacement)
        {
            var parser = new ProjectData();
            JsonObject ProjectJson(Project project) => JsonNode.Parse(parser.Serialize(ToWrapper(project))).AsObject();
            var originalView = original == null ? null : ProjectJson(original);
            var expected = original != null && _storedRows.TryGetValue(original, out var stored) ? stored : originalView;
            var edited = replacement == null ? null : ProjectJson(replacement);
            var saved = IOUtils.WriteWorkspaceChange(WorkspacePath, expected, originalView, edited);
            if (replacement == null)
            {
                _storedRows.Remove(original);
                return;
            }

            // The launcher may have assigned IDs or refreshed installation metadata while this
            // draft was open. Carry the committed values into the canonical editor model too.
            if (saved["last-launched-time"] is JsonValue history && history.TryGetValue<long>(out long timestamp))
            {
                replacement.UpdateLastLaunchedTime(timestamp);
            }

            var apps = saved["applications"].AsArray();
            for (int i = 0; i < replacement.Applications.Count; i++)
            {
                var app = replacement.Applications[i];
                app.Id = apps[i]["id"]?.GetValue<string>() ?? app.Id;
                app.AppPath = apps[i]["application-path"]?.GetValue<string>();
                app.PackageFullName = apps[i]["package-full-name"]?.GetValue<string>();
            }

            _storedRows.AddOrUpdate(original ?? replacement, saved.DeepClone().AsObject());
        }

        internal void WriteDraft(Project project, string path)
        {
            var data = ToWrapper(project);
            var contents = new ProjectData().Serialize(data);
            string staging = RequestPath(Guid.NewGuid());
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TempProjectPath));
                File.WriteAllText(staging, contents);
                File.Move(staging, path, overwrite: true);
            }
            finally
            {
                DeleteTemporaryFile(staging);
            }
        }

        internal static void DeleteTemporaryFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("Failed to remove the workspace draft", ex);
            }
        }

        private static ProjectWrapper ToWrapper(Project project)
        {
            ProjectWrapper wrapper = new()
            {
                Id = project.Id,
                Name = project.Name,
                CreationTime = project.CreationTime,
                LastLaunchedTime = project.LastLaunchedTime,
                IsShortcutNeeded = project.IsShortcutNeeded,
                MoveExistingWindows = project.MoveExistingWindows,
                Applications = [],
                MonitorConfiguration = [],
            };

            foreach (Application app in project.Applications.Where(app => app.IsIncluded))
            {
                ApplicationWrapper appWrapper = new()
                {
                    Id = app.Id,
                    Application = app.AppName,
                    ApplicationPath = app.AppPath,
                    Title = app.AppTitle,
                    PackageFullName = app.PackageFullName,
                    AppUserModelId = app.AppUserModelId,
                    PwaAppId = app.PwaAppId,
                    CommandLineArguments = app.CommandLineArguments,
                    IsElevated = app.IsElevated,
                    CanLaunchElevated = app.CanLaunchElevated,
                    Maximized = app.Maximized,
                    Minimized = app.Minimized,
                    Position = new ApplicationWrapper.WindowPositionWrapper()
                    {
                        X = app.Position.X,
                        Y = app.Position.Y,
                        Width = app.Position.Width,
                        Height = app.Position.Height,
                    },
                    Monitor = app.MonitorNumber,
                    Version = app.Version,
                };
                wrapper.Applications.Add(appWrapper);
            }

            foreach (MonitorSetup monitor in project.Monitors)
            {
                MonitorConfigurationWrapper monitorWrapper = new()
                {
                    Id = monitor.MonitorName,
                    InstanceId = monitor.MonitorInstanceId,
                    MonitorNumber = monitor.MonitorNumber,
                    Dpi = monitor.Dpi,
                    MonitorRectDpiAware = new MonitorConfigurationWrapper.MonitorRectWrapper()
                    {
                        Left = (int)monitor.MonitorDpiAwareBounds.X,
                        Top = (int)monitor.MonitorDpiAwareBounds.Y,
                        Width = (int)monitor.MonitorDpiAwareBounds.Width,
                        Height = (int)monitor.MonitorDpiAwareBounds.Height,
                    },
                    MonitorRectDpiUnaware = new MonitorConfigurationWrapper.MonitorRectWrapper()
                    {
                        Left = (int)monitor.MonitorDpiUnawareBounds.X,
                        Top = (int)monitor.MonitorDpiUnawareBounds.Y,
                        Width = (int)monitor.MonitorDpiUnawareBounds.Width,
                        Height = (int)monitor.MonitorDpiUnawareBounds.Height,
                    },
                };
                wrapper.MonitorConfiguration.Add(monitorWrapper);
            }

            return wrapper;
        }

        private bool SetWorkspaces(ICollection<Project> destination, JsonArray rows)
        {
            var parser = new ProjectData();
            var projects = new List<Project>(rows.Count);
            bool complete = true;
            foreach (var row in rows)
            {
                try
                {
                    Project newProject = Project.FromStorage(parser.Deserialize(row.ToJsonString()));
                    _storedRows.Add(newProject, row.AsObject());
                    projects.Add(newProject);
                }
                catch (Exception e)
                {
                    Logger.LogError("Exception while adding a saved workspace.", e);
                    complete = false;
                }
            }

            // Preserve the last usable snapshot on a failed refresh. At startup, rows we can
            // decode are still useful, but callers must present the partial-read warning.
            if (destination != null && (complete || destination.Count == 0))
            {
                destination.Clear();
                foreach (var project in projects)
                {
                    destination.Add(project);
                }
            }

            return complete;
        }
    }
}
