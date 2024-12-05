// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ManagedCommon;
using WorkspacesEditor.Data;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesEditorIO
    {
        public WorkspacesEditorIO()
        {
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
                    Logger.LogWarning($"Workspaces storage file content could not be set. Reason: {Properties.Resources.Error_Parsing_Message}");
                    return new ParsingResult(false, WorkspacesEditor.Properties.Resources.Error_Parsing_Message);
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
            WorkspacesData.WorkspacesListWrapper workspacesWrapper = new() { };
            workspacesWrapper.Workspaces = [];

            foreach (Project project in workspaces)
            {
                ProjectData.ProjectWrapper wrapper = new()
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
                    wrapper.Applications.Add(new ProjectData.ApplicationWrapper
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
                        Position = new ProjectData.ApplicationWrapper.WindowPositionWrapper
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
                    wrapper.MonitorConfiguration.Add(new ProjectData.MonitorConfigurationWrapper
                    {
                        Id = monitor.MonitorName,
                        InstanceId = monitor.MonitorInstanceId,
                        MonitorNumber = monitor.MonitorNumber,
                        Dpi = monitor.Dpi,
                        MonitorRectDpiAware = new ProjectData.MonitorConfigurationWrapper.MonitorRectWrapper
                        {
                            Left = (int)monitor.MonitorDpiAwareBounds.Left,
                            Top = (int)monitor.MonitorDpiAwareBounds.Top,
                            Width = (int)monitor.MonitorDpiAwareBounds.Width,
                            Height = (int)monitor.MonitorDpiAwareBounds.Height,
                        },
                        MonitorRectDpiUnaware = new ProjectData.MonitorConfigurationWrapper.MonitorRectWrapper
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

            try
            {
                IOUtils ioUtils = new();
                ioUtils.WriteFile(useTempFile ? TempProjectData.File : serializer.File, serializer.Serialize(workspacesWrapper));
            }
            catch (Exception e)
            {
                // TODO: show error
                Logger.LogError($"Exception while writing storage file: {e.Message}");
            }
        }

        private bool AddWorkspaces(MainViewModel mainViewModel, WorkspacesData.WorkspacesListWrapper workspaces)
        {
            foreach (ProjectData.ProjectWrapper project in workspaces.Workspaces)
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

        internal void SerializeTempProject(Project project)
        {
            SerializeWorkspaces([project], true);
        }
    }
}
