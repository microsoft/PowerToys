// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesEditorIO
    {
        private readonly Func<ProcessStartInfo, TimeSpan?, Task<bool>> _runProcess;

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

        public ParsingResult ParseWorkspaces(MainViewModel mainViewModel)
        {
            try
            {
                WorkspacesData parser = new();
                if (!File.Exists(parser.File))
                {
                    Logger.LogWarning($"Workspaces storage file not found: {parser.File}");
                    return new ParsingResult(true);
                }

                WorkspacesData.WorkspacesListWrapper workspaces = parser.Read(parser.File);
                if (workspaces.Workspaces == null)
                {
                    return new ParsingResult(true);
                }

                if (!SetWorkspaces(mainViewModel, workspaces))
                {
                    Logger.LogWarning("Workspaces storage file content could not be set.");
                    return new ParsingResult(false, "Error parsing Workspaces data.");
                }

                return new ParsingResult(true);
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

            File.Move(path, TempProjectPath, overwrite: true);
            return project;
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
            new IOUtils().WriteFile(serializer.File, serializer.Serialize(data));
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

        private static bool SetWorkspaces(MainViewModel mainViewModel, WorkspacesData.WorkspacesListWrapper workspaces)
        {
            mainViewModel.Workspaces.Clear();
            foreach (ProjectWrapper project in workspaces.Workspaces)
            {
                try
                {
                    Project newProject = new(project);
                    mainViewModel.Workspaces.Add(newProject);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception while adding workspace {project.Name}: {e.Message}");
                }
            }

            return true;
        }
    }
}
