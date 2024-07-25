// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedCommon;
using ProjectsEditor.Data;
using ProjectsEditor.Models;
using ProjectsEditor.ViewModels;

namespace ProjectsEditor.Utils
{
    public class ProjectsEditorIO
    {
        public ProjectsEditorIO()
        {
        }

        public ParsingResult ParseProjects(MainViewModel mainViewModel)
        {
            try
            {
                ProjectsData parser = new ProjectsData();
                if (!File.Exists(parser.File))
                {
                    Logger.LogWarning($"Projects storage file not found: {parser.File}");
                    return new ParsingResult(true);
                }

                ProjectsData.ProjectsListWrapper projects = parser.Read(parser.File);
                if (!SetProjects(mainViewModel, projects))
                {
                    Logger.LogWarning($"Projects storage file content could not be set. Reason: {Properties.Resources.Error_Parsing_Message}");
                    return new ParsingResult(false, ProjectsEditor.Properties.Resources.Error_Parsing_Message);
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
                ProjectData parser = new ProjectData();
                if (!File.Exists(TempProjectData.File))
                {
                    Logger.LogWarning($"ParseProject method. Projects storage file not found: {TempProjectData.File}");
                    return null;
                }

                Project project = new Project(parser.Read(TempProjectData.File));
                TempProjectData.DeleteTempFile();

                return project;
            }
            catch (Exception e)
            {
                Logger.LogError($"ParseProject method. Exception while parsing storage file: {e.Message}");
                return null;
            }
        }

        public void SerializeProjects(List<Project> projects, bool useTempFile = false)
        {
            ProjectsData serializer = new ProjectsData();
            ProjectsData.ProjectsListWrapper projectsWrapper = new ProjectsData.ProjectsListWrapper { };
            projectsWrapper.Projects = new List<ProjectData.ProjectWrapper>();

            foreach (Project project in projects)
            {
                ProjectData.ProjectWrapper wrapper = new ProjectData.ProjectWrapper
                {
                    Id = project.Id,
                    Name = project.Name,
                    CreationTime = project.CreationTime,
                    IsShortcutNeeded = project.IsShortcutNeeded,
                    MoveExistingWindows = project.MoveExistingWindows,
                    LastLaunchedTime = project.LastLaunchedTime,
                    Applications = new List<ProjectData.ApplicationWrapper> { },
                    MonitorConfiguration = new List<ProjectData.MonitorConfigurationWrapper> { },
                };

                foreach (var app in project.Applications.Where(x => x.IsIncluded))
                {
                    wrapper.Applications.Add(new ProjectData.ApplicationWrapper
                    {
                        Application = app.AppName,
                        ApplicationPath = app.AppPath,
                        Title = app.AppTitle,
                        PackageFullName = app.PackageFullName,
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

                foreach (var monitor in project.Monitors)
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

                projectsWrapper.Projects.Add(wrapper);
            }

            try
            {
                IOUtils ioUtils = new IOUtils();
                ioUtils.WriteFile(useTempFile ? TempProjectData.File : serializer.File, serializer.Serialize(projectsWrapper));
            }
            catch (Exception e)
            {
                // TODO: show error
                Logger.LogError($"Exception while writing storage file: {e.Message}");
            }
        }

        private bool AddProjects(MainViewModel mainViewModel, ProjectsData.ProjectsListWrapper projects)
        {
            foreach (var project in projects.Projects)
            {
                mainViewModel.Projects.Add(new Project(project));
            }

            mainViewModel.Initialize();
            return true;
        }

        private bool SetProjects(MainViewModel mainViewModel, ProjectsData.ProjectsListWrapper projects)
        {
            mainViewModel.Projects = new System.Collections.ObjectModel.ObservableCollection<Project> { };
            return AddProjects(mainViewModel, projects);
        }

        internal void SerializeTempProject(Project project)
        {
            SerializeProjects(new List<Project>() { project }, true);
        }
    }
}
