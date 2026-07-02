// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ManagedCommon;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesEditorIO
    {
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

        public Project ParseTempProject()
        {
            try
            {
                ProjectData parser = new();
                if (!File.Exists(TempProjectData.File))
                {
                    Logger.LogWarning($"ParseProject method. Workspaces storage file not found: {TempProjectData.File}");
                    return null;
                }

                Project project = new(parser.Read(TempProjectData.File));
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
            WorkspacesData serializer = new();
            WorkspacesData.WorkspacesListWrapper workspacesWrapper = new() { Workspaces = [] };

            foreach (Project project in workspaces)
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

                foreach (Application app in project.Applications)
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

                workspacesWrapper.Workspaces.Add(wrapper);
            }

            string file = useTempFile ? TempProjectData.File : serializer.File;
            try
            {
                WorkspacesCsharpLibrary.Utils.IOUtils ioUtils = new();
                ioUtils.WriteFile(file, serializer.Serialize(workspacesWrapper));
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception while writing storage file: {e.Message}");
            }
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
