// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using PowerToys.ProtectedStorage;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesEditorIO
    {
        private readonly WorkspacesRepository repository = new();
        private Revision revision;

        public Guid? PreviewId { get; set; }

        public async Task<ParsingResult> ParseWorkspacesAsync(MainViewModel mainViewModel, bool initialize = false)
        {
            try
            {
                var snapshot = initialize ? await repository.EnsureReadyAsync() : await repository.LoadAsync();
                revision = snapshot.Revision;
                WorkspacesData.WorkspacesListWrapper workspaces = new() { Workspaces = snapshot.Projects.ToList() };

                if (!SetWorkspaces(mainViewModel, workspaces))
                {
                    Logger.LogWarning($"Workspaces storage file content could not be set. Reason: {Properties.Resources.Error_Parsing_Message}");
                    return new ParsingResult(false, WorkspacesEditor.Properties.Resources.Error_Parsing_Message);
                }

                return new ParsingResult(true, snapshot.SourceCleanupPending ? Properties.Resources.ProtectedStorageCleanupPending : string.Empty);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception while parsing storage file: {e.Message}");
                throw;
            }
        }

        public async Task<Project> ParsePreviewAsync()
        {
            if (PreviewId is Guid id)
            {
                return new Project(await repository.ReadPreviewAsync(id));
            }

            throw new InvalidOperationException("No capture preview is available.");
        }

        public async Task SerializeWorkspacesAsync(List<Project> workspaces)
        {
            if (revision == null)
            {
                throw new ProtectedStorageException("NotProvisioned");
            }

            revision = await repository.SaveAsync(ToWrappers(workspaces), revision);
        }

        private static List<ProjectWrapper> ToWrappers(List<Project> workspaces)
        {
            WorkspacesData serializer = new();
            WorkspacesData.WorkspacesListWrapper workspacesWrapper = new() { };
            workspacesWrapper.Workspaces = [];

            foreach (Project project in workspaces)
            {
                ProjectWrapper wrapper = new()
                {
                    Id = project.Id,
                    Name = project.Name,
                    CreationTime = project.CreationTime,
                    IsShortcutNeeded = project.IsShortcutNeeded,
                    MoveExistingWindows = project.MoveExistingWindows,
                    LastLaunchedTime = project.LastLaunchedTime,
                    Applications = [],
                    MonitorConfiguration = [],
                };

                foreach (Application app in project.Applications.Where(x => x.IsIncluded))
                {
                    wrapper.Applications.Add(new ApplicationWrapper
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
                        Version = app.Version,
                        Maximized = app.Maximized,
                        Minimized = app.Minimized,
                        Position = new ApplicationWrapper.WindowPositionWrapper
                        {
                            X = app.Position.X,
                            Y = app.Position.Y,
                            Height = app.Position.Height,
                            Width = app.Position.Width,
                        },
                        Monitor = app.MonitorNumber,
                    });
                }

                foreach (MonitorSetup monitor in project.Monitors)
                {
                    wrapper.MonitorConfiguration.Add(new MonitorConfigurationWrapper
                    {
                        Id = monitor.MonitorName,
                        InstanceId = monitor.MonitorInstanceId,
                        MonitorNumber = monitor.MonitorNumber,
                        Dpi = monitor.Dpi,
                        MonitorRectDpiAware = new MonitorConfigurationWrapper.MonitorRectWrapper
                        {
                            Left = (int)monitor.MonitorDpiAwareBounds.Left,
                            Top = (int)monitor.MonitorDpiAwareBounds.Top,
                            Width = (int)monitor.MonitorDpiAwareBounds.Width,
                            Height = (int)monitor.MonitorDpiAwareBounds.Height,
                        },
                        MonitorRectDpiUnaware = new MonitorConfigurationWrapper.MonitorRectWrapper
                        {
                            Left = (int)monitor.MonitorDpiUnawareBounds.Left,
                            Top = (int)monitor.MonitorDpiUnawareBounds.Top,
                            Width = (int)monitor.MonitorDpiUnawareBounds.Width,
                            Height = (int)monitor.MonitorDpiUnawareBounds.Height,
                        },
                    });
                }

                workspacesWrapper.Workspaces.Add(wrapper);
            }

            return workspacesWrapper.Workspaces;
        }

        private bool AddWorkspaces(MainViewModel mainViewModel, WorkspacesData.WorkspacesListWrapper workspaces)
        {
            foreach (ProjectWrapper project in workspaces.Workspaces)
            {
                mainViewModel.Workspaces.Add(new Project(project));
            }

            mainViewModel.Initialize();
            return true;
        }

        private bool SetWorkspaces(MainViewModel mainViewModel, WorkspacesData.WorkspacesListWrapper workspaces)
        {
            mainViewModel.Workspaces = [];
            return AddWorkspaces(mainViewModel, workspaces);
        }

        internal async Task SerializePreviewAsync(Project project)
        {
            var wrapper = ToWrappers([project])[0];
            if (PreviewId is Guid current)
            {
                try
                {
                    await repository.UpdatePreviewAsync(current, wrapper);
                    return;
                }
                catch (ProtectedStorageException exception) when (exception.ErrorCode == "NotFound")
                {
                    // A restarted service or expired preview can be recreated from the
                    // retained, validated draft, never from the old temporary file.
                }
            }

            var id = await repository.CreatePreviewAsync(wrapper);
            await ReleasePreviewAsync();
            PreviewId = id;
        }

        public async Task ReleasePreviewAsync()
        {
            if (PreviewId is Guid id)
            {
                await repository.ReleasePreviewAsync(id);
                PreviewId = null;
            }
        }

        public Task<bool> RetrySourceCleanupAsync()
        {
            return repository.RetrySourceCleanupAsync();
        }

        public async Task ImportAsync(string path)
        {
            if (revision == null)
            {
                throw new ProtectedStorageException("NotProvisioned");
            }

            revision = await repository.ImportAsync(path, revision);
        }

        public Task ExportAsync(string path)
        {
            return repository.ExportAsync(path);
        }
    }
}
