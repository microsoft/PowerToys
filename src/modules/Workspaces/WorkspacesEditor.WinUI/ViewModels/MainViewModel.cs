// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Models;
using WorkspacesEditor.Utils;
using WorkspacesEditor.Views;

namespace WorkspacesEditor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private WorkspacesEditorIO _workspacesEditorIO;
        private Project _editedProject;
        private Project _projectBeforeLaunch;
        private string _projectNameBeingEdited;
        private Timer _lastUpdatedTimer;
        private WorkspacesSettings _settings;
        private bool _isDisposed;
        private bool _isExistingProjectLaunched;
        private SnapshotWindow _snapshotWindow;
        private List<OverlayWindow> _overlayWindows = new();

        public ObservableCollection<Project> Workspaces { get; set; } = new ObservableCollection<Project>();

        public List<Project> WorkspacesView
        {
            get
            {
                IEnumerable<Project> workspaces = GetFilteredWorkspaces();
                IsWorkspacesViewEmpty = !(workspaces != null && workspaces.Any());
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsWorkspacesViewEmpty)));
                if (IsWorkspacesViewEmpty)
                {
                    if (Workspaces != null && Workspaces.Any())
                    {
                        EmptyWorkspacesViewMessage = GetString("NoWorkspacesMatch");
                    }
                    else
                    {
                        EmptyWorkspacesViewMessage = GetString("No_Workspaces_Message");
                    }

                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(EmptyWorkspacesViewMessage)));

                    return new List<Project>();
                }

                WorkspacesData.OrderBy orderBy = (WorkspacesData.OrderBy)_orderByIndex;
                if (orderBy == WorkspacesData.OrderBy.LastViewed)
                {
                    return workspaces.OrderByDescending(x => x.LastLaunchedTime).ToList();
                }
                else if (orderBy == WorkspacesData.OrderBy.Created)
                {
                    return workspaces.OrderByDescending(x => x.CreationTime).ToList();
                }
                else
                {
                    return workspaces.OrderBy(x => x.Name).ToList();
                }
            }
        }

        public bool IsWorkspacesViewEmpty { get; set; }

        public string EmptyWorkspacesViewMessage { get; set; }

        private IEnumerable<Project> GetFilteredWorkspaces()
        {
            if (string.IsNullOrEmpty(_searchTerm))
            {
                return Workspaces;
            }

            return Workspaces.Where(x =>
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
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(WorkspacesView)));
            }
        }

        private int _orderByIndex;

        public int OrderByIndex
        {
            get => _orderByIndex;
            set
            {
                _orderByIndex = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(WorkspacesView)));
                _settings.Properties.SortBy = (WorkspacesProperties.SortByProperty)value;
                _settings.Save(SettingsUtils.Default);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        // Navigation action — set by MainWindow
        public Action<Type, object> NavigateAction { get; set; }

        public Action GoBackAction { get; set; }

        public Action MinimizeMainWindowAction { get; set; }

        public Action RestoreMainWindowAction { get; set; }

        public Action ShowLoadingAction { get; set; }

        public Action HideLoadingAction { get; set; }

        public MainViewModel(WorkspacesEditorIO workspacesEditorIO)
        {
            _settings = Utils.Settings.ReadSettings();
            _orderByIndex = (int)_settings.Properties.SortBy;
            _workspacesEditorIO = workspacesEditorIO;
            _lastUpdatedTimer = new Timer();
            _lastUpdatedTimer.Interval = 1000;
            _lastUpdatedTimer.Elapsed += LastUpdatedTimerElapsed;
            _lastUpdatedTimer.Start();
        }

        public void Initialize()
        {
            foreach (Project project in Workspaces)
            {
                project.InitializePreview();
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(WorkspacesView)));
        }

        public void SaveProject(Project projectToSave)
        {
            if (_editedProject == null)
            {
                return;
            }

            _editedProject.Name = projectToSave.Name;
            _editedProject.IsShortcutNeeded = projectToSave.IsShortcutNeeded;
            _editedProject.MoveExistingWindows = projectToSave.MoveExistingWindows;
            _editedProject.PreviewIcons = projectToSave.PreviewIcons;
            _editedProject.PreviewImage = projectToSave.PreviewImage;
            _editedProject.Applications = projectToSave.Applications.Where(x => x.IsIncluded).ToList();

            _editedProject.OnPropertyChanged(new PropertyChangedEventArgs("AppsCountString"));
            _editedProject.InitializePreview();
            _workspacesEditorIO.SerializeWorkspaces(Workspaces.ToList());
        }

        public void EditProject(Project selectedProject, bool isNewlyCreated = false)
        {
            _editedProject = selectedProject;

            if (!isNewlyCreated)
            {
                selectedProject = new Project(selectedProject);
            }

            if (isNewlyCreated)
            {
                string defaultNamePrefix = GetString("DefaultWorkspaceNamePrefix");
                int nextProjectIndex = 0;
                foreach (var proj in Workspaces)
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

            selectedProject.EditorWindowTitle = isNewlyCreated ? GetString("CreateWorkspace") : GetString("EditWorkspace");
            selectedProject.InitializePreview();

            _lastUpdatedTimer.Stop();

            // Navigate to editor page, passing the project as parameter
            NavigateAction?.Invoke(typeof(Views.WorkspacesEditorPage), selectedProject);
        }

        public void AddNewProject(Project project)
        {
            project.Applications.RemoveAll(app => !app.IsIncluded);
            project.InitializePreview();
            Workspaces.Add(project);
            _workspacesEditorIO.SerializeWorkspaces(Workspaces.ToList());
            TempProjectData.DeleteTempFile();
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(WorkspacesView)));
        }

        public void DeleteProject(Project selectedProject)
        {
            Workspaces.Remove(selectedProject);
            _workspacesEditorIO.SerializeWorkspaces(Workspaces.ToList());
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(WorkspacesView)));
        }

        public void SwitchToMainView()
        {
            GoBackAction?.Invoke();
            SearchTerm = string.Empty;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(SearchTerm)));
            _lastUpdatedTimer.Start();
            _editedProject = null;
        }

        public async void LaunchProject(Project project, bool exitAfterLaunch = false)
        {
            if (project == null)
            {
                return;
            }

            await Task.Run(() => RunLauncher(project.Id, InvokePoint.EditorButton));
            if (_workspacesEditorIO.ParseWorkspaces(this).Result == true)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(WorkspacesView)));
            }

            if (exitAfterLaunch)
            {
                Logger.LogInfo($"Launched the Workspace {project.Name}. Exiting.");
                Environment.Exit(0);
            }
        }

        public void EnterSnapshotMode(bool isExistingProjectLaunched)
        {
            _isExistingProjectLaunched = isExistingProjectLaunched;

            // Minimize the main window
            MinimizeMainWindowAction?.Invoke();

            // Show snapshot dialog (overlays are cosmetic — skip for now if they cause issues)
            try
            {
                _overlayWindows.Clear();
                foreach (var displayArea in DisplayArea.FindAll())
                {
                    var bounds = displayArea.OuterBounds;
                    var overlay = new OverlayWindow();
                    overlay.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    overlay.Activate();
                    _overlayWindows.Add(overlay);
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to create overlay windows: {ex.Message}");
            }

            // Show snapshot dialog
            _snapshotWindow = new SnapshotWindow(this);
            _snapshotWindow.Activate();
        }

        internal void CancelSnapshot()
        {
            foreach (var overlay in _overlayWindows)
            {
                overlay.Close();
            }

            _overlayWindows.Clear();
            RestoreMainWindowAction?.Invoke();
        }

        internal async void SnapWorkspace()
        {
            foreach (var overlay in _overlayWindows)
            {
                overlay.Close();
            }

            _overlayWindows.Clear();

            // Restore window immediately so user sees feedback
            RestoreMainWindowAction?.Invoke();
            ShowLoadingAction?.Invoke();

            await Task.Run(() => RunSnapshotTool(_isExistingProjectLaunched));

            HideLoadingAction?.Invoke();

            Project project = _workspacesEditorIO.ParseTempProject();
            if (project != null)
            {
                if (_isExistingProjectLaunched)
                {
                    project.UpdateAfterLaunchAndEdit(_projectBeforeLaunch);
                    project.EditorWindowTitle = GetString("EditWorkspace");

                    // Navigate to editor page with the updated project
                    NavigateAction?.Invoke(typeof(WorkspacesEditorPage), project);
                }
                else
                {
                    EditProject(project, true);
                }
            }
        }

        internal async void LaunchAndEdit(Project project)
        {
            await Task.Run(() => RunLauncher(project.Id, InvokePoint.LaunchAndEdit));
            _projectBeforeLaunch = new Project(project);
            EnterSnapshotMode(true);
        }

        internal void RevertLaunch()
        {
            if (_projectBeforeLaunch != null)
            {
                _projectBeforeLaunch.InitializePreview();
                NavigateAction?.Invoke(typeof(WorkspacesEditorPage), _projectBeforeLaunch);
            }
        }

        public void SaveProjectName(Project project)
        {
            _projectNameBeingEdited = project.Name;
        }

        public void CancelProjectName(Project project)
        {
            project.Name = _projectNameBeingEdited;
        }

        internal void CloseAllPopups()
        {
            foreach (Project project in Workspaces)
            {
                project.IsPopupVisible = false;
            }
        }

        private void LastUpdatedTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (Workspaces == null)
            {
                return;
            }

            App.DispatcherQueue?.TryEnqueue(() =>
            {
                foreach (Project project in Workspaces)
                {
                    project.OnPropertyChanged(new PropertyChangedEventArgs("LastLaunched"));
                }
            });
        }

        private void RunLauncher(string projectId, InvokePoint invokePoint)
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            var parentDir = Path.GetDirectoryName(exeDir);
            var launcherPath = Path.Combine(parentDir, "PowerToys.WorkspacesLauncher.exe");

            if (!File.Exists(launcherPath))
            {
                launcherPath = Path.Combine(exeDir, "PowerToys.WorkspacesLauncher.exe");
            }

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(launcherPath, $"{projectId} {(int)invokePoint}")
            {
                CreateNoWindow = true,
            };

            try
            {
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to launch workspace: {ex.Message}");
            }
        }

        private void RunSnapshotTool(bool isExistingProjectLaunched)
        {
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);

            // Snapshot tool is in the parent directory (WinUI apps are in WinUI3Apps subfolder)
            var parentDir = Path.GetDirectoryName(exeDir);
            var snapshotUtilsPath = Path.Combine(parentDir, "PowerToys.WorkspacesSnapshotTool.exe");

            if (!File.Exists(snapshotUtilsPath))
            {
                // Fallback: try same directory
                snapshotUtilsPath = Path.Combine(exeDir, "PowerToys.WorkspacesSnapshotTool.exe");
            }

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(snapshotUtilsPath)
            {
                CreateNoWindow = true,
                Arguments = isExistingProjectLaunched ? $"{(int)InvokePoint.LaunchAndEdit}" : string.Empty,
            };

            try
            {
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to run snapshot tool: {ex.Message}");
            }
        }

        private static string GetString(string key)
        {
            return ResourceLoaderInstance.ResourceLoader?.GetString(key) ?? key;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _lastUpdatedTimer?.Stop();
                _lastUpdatedTimer?.Dispose();
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
