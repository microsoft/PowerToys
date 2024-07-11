// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
        private SnapshotWindow _snapshotWindow;
        private List<OverlayWindow> _overlayWindows = new List<OverlayWindow>();

        public ObservableCollection<Project> Projects { get; set; } = new ObservableCollection<Project>();

        public IEnumerable<Project> ProjectsView
        {
            get
            {
                IEnumerable<Project> projects = GetFilteredProjects();
                IsProjectsViewEmpty = !(projects != null && projects.Any());
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsProjectsViewEmpty)));
                if (IsProjectsViewEmpty)
                {
                    if (Projects != null && Projects.Any())
                    {
                        EmptyProjectsViewMessage = Properties.Resources.NoProjectsMatch;
                    }
                    else
                    {
                        EmptyProjectsViewMessage = Properties.Resources.No_Projects_Message;
                    }

                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(EmptyProjectsViewMessage)));

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

        public bool IsProjectsViewEmpty { get; set; }

        public string EmptyProjectsViewMessage { get; set; }

        // return those projects where the project name or any of the selected apps' name contains the search term
        private IEnumerable<Project> GetFilteredProjects()
        {
            if (string.IsNullOrEmpty(_searchTerm))
            {
                return Projects;
            }

            return Projects.Where(x =>
            {
                if (x.Name.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                if (x.Applications == null)
                {
                    return false;
                }

                return x.Applications.Any(app => app.AppName.Contains(_searchTerm, StringComparison.InvariantCultureIgnoreCase));
            });
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
            editedProject.PreviewIcons = projectToSave.PreviewIcons;
            editedProject.PreviewImage = projectToSave.PreviewImage;
            editedProject.Applications = projectToSave.Applications.Where(x => x.IsIncluded).ToList();

            editedProject.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("AppsCountString"));
            editedProject.Initialize();
            _projectsEditorIO.SerializeProjects(Projects.ToList());
            if (editedProject.IsShortcutNeeded)
            {
                CreateShortcut(editedProject);
            }
        }

        private void CreateShortcut(Project project)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string shortcutAddress = Path.Combine(FolderUtils.Desktop(), project.Name + ".lnk");
            string shortcutIconFilename = Path.Combine(FolderUtils.Temp(), project.Id + ".ico");

            Bitmap icon = ProjectIcon.DrawIcon(ProjectIcon.IconTextFromProjectName(project.Name));
            ProjectIcon.SaveIcon(icon, shortcutIconFilename);

            try
            {
                // Workaround to be able to create a shortcut with unicode filename
                File.WriteAllBytes(shortcutAddress, Array.Empty<byte>());

                // Create a ShellLinkObject that references the .lnk file
                Shell32.Shell shell = new Shell32.Shell();
                Shell32.Folder dir = shell.NameSpace(FolderUtils.Desktop());
                Shell32.FolderItem folderItem = dir.Items().Item($"{project.Name}.lnk");
                Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;

                // Set the .lnk file properties
                link.Description = $"Project Launcher {project.Id}";
                link.Path = Path.Combine(basePath, "PowerToys.ProjectsLauncher.exe");
                link.Arguments = project.Id.ToString();
                link.WorkingDirectory = basePath;
                link.SetIconLocation(shortcutIconFilename, 0);

                link.Save(shortcutAddress);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Shortcut creation error: {ex.Message}");
            }
        }

        public void SaveProjectName(Project project)
        {
            projectNameBeingEdited = project.Name;
        }

        public void CancelProjectName(Project project)
        {
            project.Name = projectNameBeingEdited;
        }

        public async void SnapNewProject()
        {
            CancelSnapshot();

            await Task.Run(() => RunSnapshotTool());

            Project project = new Project();
            if (_projectsEditorIO.ParseTempProject(out project).Result)
            {
                EditProject(project, true);
            }
        }

        public void EditProject(Project selectedProject, bool isNewlyCreated = false)
        {
            var editPage = new ProjectEditor(this);

            SetEditedProject(selectedProject);
            if (!isNewlyCreated)
            {
                selectedProject = new Project(selectedProject);
            }

            if (isNewlyCreated)
            {
                // generate a default name for the new project
                string defaultNamePrefix = Properties.Resources.DefaultProjectNamePrefix;
                int nextProjectIndex = 0;
                foreach (var proj in Projects)
                {
                    if (proj.Name.StartsWith(defaultNamePrefix, StringComparison.CurrentCulture))
                    {
                        try
                        {
                            int index = int.Parse(proj.Name[(defaultNamePrefix.Length + 1)..], CultureInfo.CurrentCulture);
                            if (nextProjectIndex < index)
                            {
                                nextProjectIndex = index;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                selectedProject.Name = defaultNamePrefix + " " + (nextProjectIndex + 1).ToString(CultureInfo.CurrentCulture);
            }

            selectedProject.EditorWindowTitle = isNewlyCreated ? Properties.Resources.CreateProject : Properties.Resources.EditProject;
            selectedProject.Initialize();

            editPage.DataContext = selectedProject;
            _mainWindow.ShowPage(editPage);
            lastUpdatedTimer.Stop();
        }

        public void AddNewProject(Project project)
        {
            Projects.Add(project);
            _projectsEditorIO.SerializeProjects(Projects.ToList());
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
            p.StartInfo = new ProcessStartInfo(@".\PowerToys.ProjectsSnapshotTool.exe");
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
            p.StartInfo = new ProcessStartInfo(@".\PowerToys.ProjectsLauncher.exe", projectId);
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        internal void CloseAllPopups()
        {
            foreach (Project project in Projects)
            {
                project.IsPopupVisible = false;
            }
        }

        internal void EnterSnapshotMode()
        {
            _mainWindow.WindowState = System.Windows.WindowState.Minimized;
            _overlayWindows.Clear();
            foreach (var screen in MonitorHelper.GetDpiUnawareScreens())
            {
                var bounds = screen.Bounds;
                OverlayWindow overlayWindow = new OverlayWindow();
                overlayWindow.Top = bounds.Top;
                overlayWindow.Left = bounds.Left;
                overlayWindow.Width = bounds.Width;
                overlayWindow.Height = bounds.Height;
                overlayWindow.ShowActivated = true;
                overlayWindow.Topmost = true;
                overlayWindow.Show();
                _overlayWindows.Add(overlayWindow);
            }

            _snapshotWindow = new SnapshotWindow(this);
            _snapshotWindow.ShowActivated = true;
            _snapshotWindow.Topmost = true;
            _snapshotWindow.Show();
        }

        internal void CancelSnapshot()
        {
            foreach (OverlayWindow overlayWindow in _overlayWindows)
            {
                overlayWindow.Close();
            }

            _mainWindow.WindowState = System.Windows.WindowState.Normal;
        }
    }
}
