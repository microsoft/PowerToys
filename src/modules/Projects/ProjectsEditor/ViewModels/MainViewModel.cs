// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ManagedCommon;
using ProjectsEditor.Models;
using ProjectsEditor.Utils;
using static ProjectsEditor.Data.ProjectsData;

namespace ProjectsEditor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private ProjectsEditorIO _projectsEditorIO;

        public ObservableCollection<Project> Projects { get; set; }

        public IEnumerable<Project> ProjectsView
        {
            get
            {
                IEnumerable<Project> projects = string.IsNullOrEmpty(_searchTerm) ? Projects : Projects.Where(x => x.Name.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase));
                if (projects == null)
                {
                    return Enumerable.Empty<Project>();
                }

                OrderBy orderBy = (OrderBy)_orderByIndex;
                if (orderBy == OrderBy.LastViewed)
                {
                    return projects.OrderByDescending(x => x.LastLaunchedTime);
                }
                else if (orderBy == OrderBy.Created)
                {
                    return projects.OrderByDescending(x => x.CreationTime);
                }
                else
                {
                    return projects.OrderBy(x => x.Name);
                }
            }
        }

        private string _searchTerm;

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ProjectsView)));
            }
        }

        private int _orderByIndex;

        public int OrderByIndex
        {
            get => _orderByIndex;
            set
            {
                _orderByIndex = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ProjectsView)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private Project editedProject;
        private bool isEditedProjectNewlyCreated;
        private string projectNameBeingEdited;
        private MainWindow _mainWindow;
        private System.Timers.Timer lastUpdatedTimer;

        public MainViewModel(ProjectsEditorIO projectsEditorIO)
        {
            _projectsEditorIO = projectsEditorIO;
            lastUpdatedTimer = new System.Timers.Timer();
            lastUpdatedTimer.Interval = 1000;
            lastUpdatedTimer.Elapsed += LastUpdatedTimerElapsed;
            lastUpdatedTimer.Start();
        }

        public void Initialize()
        {
            foreach (Project project in Projects)
            {
                project.Initialize();
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ProjectsView)));
        }

        public void SetEditedProject(Project editedProject)
        {
            this.editedProject = editedProject;
        }

        public void SaveProject(Project projectToSave)
        {
            editedProject.Name = projectToSave.Name;
            editedProject.IsShortcutNeeded = projectToSave.IsShortcutNeeded;
            editedProject.PreviewImage = projectToSave.PreviewImage;
            for (int appIndex = 0; appIndex < editedProject.Applications.Count; appIndex++)
            {
                editedProject.Applications[appIndex].IsSelected = projectToSave.Applications[appIndex].IsSelected;
                editedProject.Applications[appIndex].CommandLineArguments = projectToSave.Applications[appIndex].CommandLineArguments;
            }

            editedProject.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("AppsCountString"));
            editedProject.Initialize();
            _projectsEditorIO.SerializeProjects(Projects.ToList());
            if (editedProject.IsShortcutNeeded)
            {
                CreateShortcut(editedProject);
            }
        }

        private void CreateShortcut(Project editedProject)
        {
            object shDesktop = (object)"Desktop";
            IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
            string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + $"\\{editedProject.Name}.lnk";
            IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.Description = $"Project Launcher {editedProject.Id}";
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            shortcut.TargetPath = Path.Combine(basePath, "ProjectsEditor.exe");
            shortcut.Arguments = '"' + editedProject.Id + '"';
            shortcut.WorkingDirectory = basePath;
            shortcut.Save();
        }

        public void SaveProjectName(Project project)
        {
            projectNameBeingEdited = project.Name;
        }

        public void CancelProjectName(Project project)
        {
            project.Name = projectNameBeingEdited;
        }

        public async void AddNewProject()
        {
            await Task.Run(() => RunSnapshotTool());
            if (_projectsEditorIO.ParseProjects(this).Result == true && Projects.Count != 0)
            {
                int repeatCounter = 1;
                string newName = Projects.Count != 0 ? Projects.Last().Name : "Project 1"; // TODO: localizable project name
                while (Projects.Where(x => x.Name.Equals(Projects.Last().Name, StringComparison.Ordinal)).Count() > 1)
                {
                    Projects.Last().Name = $"{newName} ({repeatCounter})";
                    repeatCounter++;
                }

                _projectsEditorIO.SerializeProjects(Projects.ToList());
                EditProject(Projects.Last(), true);
            }
        }

        public void EditProject(Project selectedProject, bool isNewlyCreated = false)
        {
            isEditedProjectNewlyCreated = isNewlyCreated;
            var editPage = new ProjectEditor(this);
            SetEditedProject(selectedProject);
            Project projectEdited = new Project(selectedProject) { EditorWindowTitle = isNewlyCreated ? Properties.Resources.CreateProject : Properties.Resources.EditProject };
            if (isNewlyCreated)
            {
                projectEdited.Initialize();
            }

            editPage.DataContext = projectEdited;
            _mainWindow.ShowPage(editPage);
            lastUpdatedTimer.Stop();
        }

        public void CancelLastEdit()
        {
            if (isEditedProjectNewlyCreated)
            {
                Projects.Remove(editedProject);
                _projectsEditorIO.SerializeProjects(Projects.ToList());
            }
        }

        public void DeleteProject(Project selectedProject)
        {
            Projects.Remove(selectedProject);
            _projectsEditorIO.SerializeProjects(Projects.ToList());
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ProjectsView)));
        }

        public void SetMainWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void SwitchToMainView()
        {
            _mainWindow.SwitchToMainView();
            SearchTerm = string.Empty;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(SearchTerm)));
            lastUpdatedTimer.Start();
        }

        public void LaunchProject(string projectId)
        {
            if (!Projects.Where(x => x.Id == projectId).Any())
            {
                Logger.LogWarning($"Project to launch not find. Id: {projectId}");
                return;
            }

            LaunchProject(Projects.Where(x => x.Id == projectId).First(), true);
        }

        public async void LaunchProject(Project project, bool exitAfterLaunch = false)
        {
            await Task.Run(() => RunLauncher(project.Id));
            if (_projectsEditorIO.ParseProjects(this).Result == true)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ProjectsView)));
            }

            if (exitAfterLaunch)
            {
                Logger.LogInfo($"Launched the project {project.Name}. Exiting.");
                Environment.Exit(0);
            }
        }

        private void LastUpdatedTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (Projects == null)
            {
                return;
            }

            foreach (Project project in Projects)
            {
                project.OnPropertyChanged(new PropertyChangedEventArgs("LastLaunched"));
            }
        }

        private void RunSnapshotTool(string filename = null)
        {
            Process p = new Process();
            p.StartInfo = new ProcessStartInfo(@".\ProjectsSnapshotTool.exe");
            p.StartInfo.CreateNoWindow = true;
            if (!string.IsNullOrEmpty(filename))
            {
                p.StartInfo.Arguments = filename;
            }

            p.Start();
            p.WaitForExit();
        }

        private void RunLauncher(string projectId)
        {
            Process p = new Process();
            p.StartInfo = new ProcessStartInfo(@".\ProjectsLauncher.exe", projectId);
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
