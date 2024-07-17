// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
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

        public ParsingResult ParseTempProject(out Project project)
        {
            project = null;
            try
            {
                ProjectData parser = new ProjectData();
                if (!File.Exists(TempProjectData.File))
                {
                    Logger.LogWarning($"ParseProject method. Projects storage file not found: {TempProjectData.File}");
                    return new ParsingResult(false);
                }

                project = GetProjectFromWrapper(parser.Read(TempProjectData.File));
                TempProjectData.DeleteTempFile();

                return new ParsingResult(true);
            }
            catch (Exception e)
            {
                Logger.LogError($"ParseProject method. Exception while parsing storage file: {e.Message}");
                return new ParsingResult(false, e.Message);
            }
        }

        private Project GetProjectFromWrapper(ProjectData.ProjectWrapper project)
        {
            Project newProject = new Project()
            {
                Id = project.Id,
                Name = project.Name,
                CreationTime = project.CreationTime,
                LastLaunchedTime = project.LastLaunchedTime,
                IsShortcutNeeded = project.IsShortcutNeeded,
                Monitors = new List<MonitorSetup>() { },
                Applications = new List<Models.Application> { },
            };

            foreach (var app in project.Applications)
            {
                Models.Application newApp = new Models.Application()
                {
                    AppName = app.Application,
                    AppPath = app.ApplicationPath,
                    AppTitle = app.Title,
                    PackageFullName = app.PackageFullName,
                    Parent = newProject,
                    CommandLineArguments = app.CommandLineArguments,
                    IsElevated = app.IsElevated,
                    Maximized = app.Maximized,
                    Minimized = app.Minimized,
                    IsNotFound = false,
                    Position = new Models.Application.WindowPosition()
                    {
                        Height = app.Position.Height,
                        Width = app.Position.Width,
                        X = app.Position.X,
                        Y = app.Position.Y,
                    },
                    MonitorNumber = app.Monitor,
                };
                newApp.InitializationFinished();
                newProject.Applications.Add(newApp);
            }

            foreach (var monitor in project.MonitorConfiguration)
            {
                Rect dpiAware = new Rect(monitor.MonitorRectDpiAware.Left, monitor.MonitorRectDpiAware.Top, monitor.MonitorRectDpiAware.Width, monitor.MonitorRectDpiAware.Height);
                Rect dpiUnaware = new Rect(monitor.MonitorRectDpiUnaware.Left, monitor.MonitorRectDpiUnaware.Top, monitor.MonitorRectDpiUnaware.Width, monitor.MonitorRectDpiUnaware.Height);
                newProject.Monitors.Add(new MonitorSetup(monitor.Id, monitor.InstanceId, monitor.MonitorNumber, monitor.Dpi, dpiAware, dpiUnaware));
            }

            return newProject;
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
                mainViewModel.Projects.Add(GetProjectFromWrapper(project));
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
