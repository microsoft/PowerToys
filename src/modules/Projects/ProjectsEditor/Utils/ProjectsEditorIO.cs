// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
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
                    return new ParsingResult(true);
                }

                ProjectsData.ProjectsListWrapper projects = parser.Read(parser.File);
                if (!SetProjects(mainViewModel, projects))
                {
                    return new ParsingResult(false, ProjectsEditor.Properties.Resources.Error_Parsing_Message);
                }

                return new ParsingResult(true);
            }
            catch (Exception e)
            {
                return new ParsingResult(false, e.Message);
            }
        }

        public ParsingResult ParseProject(string fileName, out Project project)
        {
            project = null;
            try
            {
                ProjectsData parser = new ProjectsData();
                if (!File.Exists(fileName))
                {
                    return new ParsingResult(true);
                }

                ProjectsData.ProjectsListWrapper projects = parser.Read(fileName);
                if (!ExtractProject(projects, out project))
                {
                    return new ParsingResult(false, ProjectsEditor.Properties.Resources.Error_Parsing_Message);
                }

                return new ParsingResult(true);
            }
            catch (Exception e)
            {
                return new ParsingResult(false, e.Message);
            }
        }

        private bool ExtractProject(ProjectsData.ProjectsListWrapper projects, out Project project)
        {
            project = null;
            if (projects.Projects == null)
            {
                return false;
            }

            if (projects.Projects.Count != 1)
            {
                return false;
            }

            ProjectsData.ProjectWrapper projectWrapper = projects.Projects[0];
            project = GetProjectFromWrappper(projectWrapper);
            return true;
        }

        private Project GetProjectFromWrappper(ProjectsData.ProjectWrapper project)
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
                newProject.Applications.Add(new Models.Application()
                {
                    Hwnd = new IntPtr(app.Hwnd),
                    AppPath = app.Application,
                    AppTitle = app.Title,
                    Parent = newProject,
                    CommandLineArguments = app.CommandLineArguments,
                    Maximized = app.Maximized,
                    Minimized = app.Minimized,
                    IsSelected = true,
                    Position = new Models.Application.WindowPosition()
                    {
                        Height = app.Position.Height,
                        Width = app.Position.Width,
                        X = app.Position.X,
                        Y = app.Position.Y,
                    },
                    MonitorNumber = app.Monitor,
                });
            }

            foreach (var monitor in project.MonitorConfiguration)
            {
                Rect dpiAware = new Rect(monitor.MonitorRectDpiAware.Left, monitor.MonitorRectDpiAware.Top, monitor.MonitorRectDpiAware.Width, monitor.MonitorRectDpiAware.Height);
                Rect dpiUnaware = new Rect(monitor.MonitorRectDpiUnaware.Left, monitor.MonitorRectDpiUnaware.Top, monitor.MonitorRectDpiUnaware.Width, monitor.MonitorRectDpiUnaware.Height);
                newProject.Monitors.Add(new MonitorSetup(monitor.Id, monitor.InstanceId, monitor.MonitorNumber, monitor.Dpi, dpiAware, dpiUnaware));
            }

            return newProject;
        }

        public void SerializeProjects(List<Project> projects)
        {
            ProjectsData serializer = new ProjectsData();
            ProjectsData.ProjectsListWrapper projectsWrapper = new ProjectsData.ProjectsListWrapper { };
            projectsWrapper.Projects = new List<ProjectsData.ProjectWrapper>();

            foreach (Project project in projects)
            {
                ProjectsData.ProjectWrapper wrapper = new ProjectsData.ProjectWrapper
                {
                    Id = project.Id,
                    Name = project.Name,
                    CreationTime = project.CreationTime,
                    IsShortcutNeeded = project.IsShortcutNeeded,
                    LastLaunchedTime = project.LastLaunchedTime,
                    Applications = new List<ProjectsData.ApplicationWrapper> { },
                    MonitorConfiguration = new List<ProjectsData.MonitorConfigurationWrapper> { },
                };

                foreach (var app in project.Applications)
                {
                    if (app.IsSelected)
                    {
                        wrapper.Applications.Add(new ProjectsData.ApplicationWrapper
                        {
                            Hwnd = app.Hwnd,
                            Application = app.AppPath,
                            Title = app.AppTitle,
                            CommandLineArguments = app.CommandLineArguments,
                            Maximized = app.Maximized,
                            Minimized = app.Minimized,
                            Position = new ProjectsData.ApplicationWrapper.WindowPositionWrapper
                            {
                                X = app.Position.X,
                                Y = app.Position.Y,
                                Height = app.Position.Height,
                                Width = app.Position.Width,
                            },
                            Monitor = app.MonitorNumber,
                        });
                    }
                }

                foreach (var monitor in project.Monitors)
                {
                    wrapper.MonitorConfiguration.Add(new ProjectsData.MonitorConfigurationWrapper
                    {
                        Id = monitor.MonitorName,
                        InstanceId = monitor.MonitorInstanceId,
                        MonitorNumber = monitor.MonitorNumber,
                        Dpi = monitor.Dpi,
                        MonitorRectDpiAware = new ProjectsData.MonitorConfigurationWrapper.MonitorRectWrapper
                        {
                            Left = (int)monitor.MonitorDpiAwareBounds.Left,
                            Top = (int)monitor.MonitorDpiAwareBounds.Top,
                            Width = (int)monitor.MonitorDpiAwareBounds.Width,
                            Height = (int)monitor.MonitorDpiAwareBounds.Height,
                        },
                        MonitorRectDpiUnaware = new ProjectsData.MonitorConfigurationWrapper.MonitorRectWrapper
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
                ioUtils.WriteFile(serializer.File, serializer.Serialize(projectsWrapper));
            }
            catch (Exception)
            {
                // TODO: show error
            }
        }

        private bool AddProjects(MainViewModel mainViewModel, ProjectsData.ProjectsListWrapper projects)
        {
            foreach (var project in projects.Projects)
            {
                mainViewModel.Projects.Add(GetProjectFromWrappper(project));
            }

            mainViewModel.Initialize();
            return true;
        }

        private bool SetProjects(MainViewModel mainViewModel, ProjectsData.ProjectsListWrapper projects)
        {
            mainViewModel.Projects = new System.Collections.ObjectModel.ObservableCollection<Project> { };
            return AddProjects(mainViewModel, projects);
        }
    }
}
