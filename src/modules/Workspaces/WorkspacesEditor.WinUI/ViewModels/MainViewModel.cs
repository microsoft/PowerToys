// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using WorkspacesCsharpLibrary;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Services;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly WorkspacesEditorIO _workspacesEditorIO;
        private readonly Action<WorkspacesSettings> _saveSettings;
        private readonly Action<List<Project>> _serializeWorkspaces;
        private readonly IWorkspaceShortcutService _shortcuts;
        private readonly Action<EditEvent> _writeEditTelemetry;
        private Project _editedProject;
        private Project _projectBeforeLaunch;
        private string _projectNameBeingEdited;
        private Microsoft.UI.Xaml.DispatcherTimer _lastUpdatedTimer;
        private WorkspacesSettings _settings;
        private bool _isDisposed;
        private bool _isExistingProjectLaunched;

        public ObservableCollection<Project> Workspaces { get; set; } = new ObservableCollection<Project>();

        private List<Project> _workspacesView = new();

        public List<Project> WorkspacesView
        {
            get => _workspacesView;
            private set => SetProperty(ref _workspacesView, value);
        }

        [ObservableProperty]
        public partial bool IsWorkspacesViewEmpty { get; set; }

        [ObservableProperty]
        public partial string EmptyWorkspacesViewMessage { get; set; }

        public void RefreshWorkspacesView()
        {
            IEnumerable<Project> workspaces = GetFilteredWorkspaces();
            bool isEmpty = !(workspaces != null && workspaces.Any());
            IsWorkspacesViewEmpty = isEmpty;

            if (isEmpty)
            {
                if (Workspaces != null && Workspaces.Count > 0)
                {
                    EmptyWorkspacesViewMessage = GetString("NoWorkspacesMatch");
                }
                else
                {
                    EmptyWorkspacesViewMessage = GetString("No_Workspaces_Message");
                }

                WorkspacesView = new List<Project>();
                return;
            }

            WorkspacesData.OrderBy orderBy = (WorkspacesData.OrderBy)OrderByIndex;
            if (orderBy == WorkspacesData.OrderBy.LastViewed)
            {
                WorkspacesView = workspaces.OrderByDescending(x => x.LastLaunchedTime).ToList();
            }
            else if (orderBy == WorkspacesData.OrderBy.Created)
            {
                WorkspacesView = workspaces.OrderByDescending(x => x.CreationTime).ToList();
            }
            else
            {
                WorkspacesView = workspaces.OrderBy(x => x.Name).ToList();
            }
        }

        private IEnumerable<Project> GetFilteredWorkspaces()
        {
            if (Workspaces == null)
            {
                return Enumerable.Empty<Project>();
            }

            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                return Workspaces;
            }

            return Workspaces.Where(x =>
            {
                if (x.Name != null && x.Name.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                if (x.Applications == null)
                {
                    return false;
                }

                return x.Applications.Any(app => app.AppName != null && app.AppName.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase));
            });
        }

        /// <summary>
        /// Returns the workspaces matching the given query (by workspace name or contained app name),
        /// used to populate the title bar search suggestions. Returns an empty list for an empty query.
        /// </summary>
        public IEnumerable<Project> SearchWorkspaces(string query)
        {
            if (Workspaces == null || string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<Project>();
            }

            return Workspaces
                .Where(x =>
                {
                    if (x.Name != null && x.Name.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }

                    if (x.Applications == null)
                    {
                        return false;
                    }

                    return x.Applications.Any(app => app.AppName.Contains(query, StringComparison.InvariantCultureIgnoreCase));
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        [ObservableProperty]
        public partial int OrderByIndex { get; set; }

        [ObservableProperty]
        public partial string SearchTerm { get; set; }

        partial void OnSearchTermChanged(string value)
        {
            RefreshWorkspacesView();
        }

        partial void OnOrderByIndexChanged(int value)
        {
            _settings.Properties.SortBy = (WorkspacesProperties.SortByProperty)value;
            _saveSettings(_settings);
            RefreshWorkspacesView();
        }

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        public MainViewModel(WorkspacesEditorIO workspacesEditorIO)
            : this(
                workspacesEditorIO,
                Utils.Settings.ReadSettings(),
                settings => settings.Save(SettingsUtils.Default),
                new WorkspaceShortcutService(),
                workspaces => workspacesEditorIO.SerializeWorkspaces(workspaces),
                telemetryEvent => PowerToysTelemetry.Log.WriteEvent(telemetryEvent))
        {
            // Populate the shared resolver before either previews or the editor load PWA icons.
            try
            {
                _ = new PwaHelper();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("Failed to initialize PWA icons", ex);
            }
        }

        internal MainViewModel(
            WorkspacesEditorIO workspacesEditorIO,
            WorkspacesSettings settings,
            Action<WorkspacesSettings> saveSettings,
            IWorkspaceShortcutService shortcuts,
            Action<List<Project>> serializeWorkspaces,
            Action<EditEvent> writeEditTelemetry)
        {
            ArgumentNullException.ThrowIfNull(workspacesEditorIO);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(saveSettings);
            ArgumentNullException.ThrowIfNull(shortcuts);
            ArgumentNullException.ThrowIfNull(serializeWorkspaces);
            ArgumentNullException.ThrowIfNull(writeEditTelemetry);

            _workspacesEditorIO = workspacesEditorIO;
            _settings = settings;
            _saveSettings = saveSettings;
            _shortcuts = shortcuts;
            _serializeWorkspaces = serializeWorkspaces;
            _writeEditTelemetry = writeEditTelemetry;
            OrderByIndex = (int)_settings.Properties.SortBy;

            StrongReferenceMessenger.Default.Register<SnapshotCapturedMessage>(this, (r, m) => ((MainViewModel)r).OnSnapshotCaptured());
            StrongReferenceMessenger.Default.Register<SnapshotCancelledMessage>(this, (r, m) => ((MainViewModel)r).CancelSnapshot());
        }

        private void OnSnapshotCaptured()
        {
            if (!_isDisposed)
            {
                _ = SnapWorkspaceAsync();
            }
        }

        public void Initialize()
        {
            foreach (Project project in Workspaces)
            {
                project.InitializePreview();
            }

            // Create DispatcherTimer here (requires UI thread / DispatcherQueue to exist)
            _lastUpdatedTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _lastUpdatedTimer.Interval = TimeSpan.FromSeconds(1);
            _lastUpdatedTimer.Tick += LastUpdatedTimerTick;
            _lastUpdatedTimer.Start();

            RefreshWorkspacesView();
        }

        public void SaveProject(Project projectToSave)
        {
            if (_isDisposed || _editedProject == null || projectToSave == null)
            {
                return;
            }

            EditEvent editEvent = EditEventFactory.Create(_editedProject, projectToSave);
            if (!string.Equals(_editedProject.Name, projectToSave.Name, StringComparison.Ordinal))
            {
                _shortcuts.Remove(_editedProject);
            }

            _editedProject.Name = projectToSave.Name;
            _editedProject.IsShortcutNeeded = projectToSave.IsShortcutNeeded;
            _editedProject.MoveExistingWindows = projectToSave.MoveExistingWindows;
            _editedProject.PreviewIcons = projectToSave.PreviewIcons;
            _editedProject.PreviewImage = projectToSave.PreviewImage;
            _editedProject.Applications = projectToSave.Applications.Where(x => x.IsIncluded).ToList();

            _editedProject.NotifyApplicationsChanged();
            _editedProject.InitializePreview();
            _serializeWorkspaces(Workspaces.ToList());
            _shortcuts.Apply(_editedProject);
            RefreshWorkspacesView();

            _writeEditTelemetry(editEvent);
        }

        public void EditProject(Project selectedProject, bool isNewlyCreated = false)
        {
            if (_isDisposed || selectedProject == null)
            {
                return;
            }

            _editedProject = selectedProject;

            if (!isNewlyCreated)
            {
                selectedProject = new Project(selectedProject);
            }

            if (isNewlyCreated)
            {
                string defaultNamePrefix = GetString("DefaultWorkspaceNamePrefix");
                selectedProject.Name = WorkspaceNameGenerator.Create(defaultNamePrefix, Workspaces.Select(project => project.Name));
            }

            CheckShortcutPresence(selectedProject);
            selectedProject.EditorWindowTitle = isNewlyCreated ? GetString("CreateWorkspace") : GetString("EditWorkspace");
            selectedProject.InitializePreview();

            _lastUpdatedTimer?.Stop();

            // Navigate to editor page, passing the project as parameter
            StrongReferenceMessenger.Default.Send(new NavigateToEditorMessage(selectedProject));
        }

        public void AddNewProject(Project project)
        {
            project.Applications.RemoveAll(app => !app.IsIncluded);
            project.InitializePreview();
            Workspaces.Add(project);
            _serializeWorkspaces(Workspaces.ToList());
            TempProjectData.DeleteTempFile();
            RefreshWorkspacesView();
            _shortcuts.Apply(project);

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.CreateEvent
            {
                Successful = true,
                NumScreens = project.Monitors.Count,
                AppCount = project.Applications.Count,
                CliCount = project.Applications.FindAll(app => !string.IsNullOrEmpty(app.CommandLineArguments)).Count,
                AdminCount = project.Applications.FindAll(app => app.IsElevated).Count,
                ShortcutCreated = project.IsShortcutNeeded,
            });
        }

        public void DeleteProject(Project selectedProject)
        {
            Workspaces.Remove(selectedProject);
            _serializeWorkspaces(Workspaces.ToList());
            _shortcuts.Remove(selectedProject);
            RefreshWorkspacesView();

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.DeleteEvent { Successful = true });
        }

        public void SaveOrAddProject(Project projectToSave)
        {
            if (projectToSave == null)
            {
                return;
            }

            projectToSave.CloseExpanders();

            if (Workspaces.Any(x => x.Id == projectToSave.Id))
            {
                SaveProject(projectToSave);
            }
            else
            {
                AddNewProject(projectToSave);
            }

            SwitchToMainView();
        }

        public void DeleteProjectById(string projectId)
        {
            var existing = Workspaces.FirstOrDefault(x => x.Id == projectId);
            if (existing != null)
            {
                DeleteProject(existing);
            }

            TempProjectData.DeleteTempFile();
            SwitchToMainView();
        }

        public void SwitchToMainView()
        {
            if (_isDisposed)
            {
                return;
            }

            StrongReferenceMessenger.Default.Send(new GoBackMessage());
            RefreshWorkspacesView();
            _lastUpdatedTimer?.Start();
            _editedProject = null;
        }

        [RelayCommand]
        public async Task LaunchProjectAsync(Project project)
        {
            if (_isDisposed || project == null)
            {
                return;
            }

            await Task.Run(() => RunLauncher(project.Id, InvokePoint.EditorButton));
            if (_isDisposed)
            {
                return;
            }

            if (_workspacesEditorIO.ParseWorkspaces(this).Result == true)
            {
                foreach (Project p in Workspaces)
                {
                    p.InitializePreview();
                }

                RefreshWorkspacesView();
            }
        }

        public async Task LaunchProjectAndExitAsync(Project project)
        {
            if (_isDisposed || project == null)
            {
                return;
            }

            await Task.Run(() => RunLauncher(project.Id, InvokePoint.EditorButton));
            if (_isDisposed)
            {
                return;
            }

            if (_workspacesEditorIO.ParseWorkspaces(this).Result == true)
            {
                foreach (Project p in Workspaces)
                {
                    p.InitializePreview();
                }

                RefreshWorkspacesView();
            }

            Logger.LogInfo($"Launched the Workspace {project.Name}. Exiting.");
            StrongReferenceMessenger.Default.Send(new CloseApplicationMessage());
        }

        public void EnterSnapshotMode(bool isExistingProjectLaunched)
        {
            if (_isDisposed)
            {
                return;
            }

            _isExistingProjectLaunched = isExistingProjectLaunched;

            // Minimize the main window
            StrongReferenceMessenger.Default.Send(new MinimizeWindowMessage());

            // Request the View layer to show the snapshot window
            StrongReferenceMessenger.Default.Send(new ShowSnapshotWindowMessage());
        }

        internal void CancelSnapshot()
        {
            if (!_isDisposed)
            {
                StrongReferenceMessenger.Default.Send(new RestoreWindowMessage());
            }
        }

        [RelayCommand]
        internal async Task SnapWorkspaceAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            // Restore window immediately so user sees feedback
            StrongReferenceMessenger.Default.Send(new RestoreWindowMessage());
            IsLoading = true;

            await Task.Run(() => RunSnapshotTool(_isExistingProjectLaunched));

            if (_isDisposed)
            {
                return;
            }

            IsLoading = false;

            Project project = _workspacesEditorIO.ParseTempProject();
            if (project != null)
            {
                if (_isExistingProjectLaunched)
                {
                    project.UpdateAfterLaunchAndEdit(_projectBeforeLaunch);
                    CheckShortcutPresence(project);
                    project.EditorWindowTitle = GetString("EditWorkspace");
                    project.InitializePreview();

                    // Navigate to editor page with the updated project
                    StrongReferenceMessenger.Default.Send(new NavigateToEditorMessage(project));
                }
                else
                {
                    EditProject(project, true);
                }
            }
        }

        [RelayCommand]
        internal async Task LaunchAndEditAsync(Project project)
        {
            if (_isDisposed || project == null)
            {
                return;
            }

            await Task.Run(() => RunLauncher(project.Id, InvokePoint.LaunchAndEdit));
            if (_isDisposed)
            {
                return;
            }

            _projectBeforeLaunch = new Project(project);
            EnterSnapshotMode(true);
        }

        internal void RevertLaunch()
        {
            if (!_isDisposed && _projectBeforeLaunch != null)
            {
                CheckShortcutPresence(_projectBeforeLaunch);
                _projectBeforeLaunch.InitializePreview();
                StrongReferenceMessenger.Default.Send(new NavigateToEditorMessage(_projectBeforeLaunch));
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

        private void LastUpdatedTimerTick(object sender, object e)
        {
            if (Workspaces == null)
            {
                return;
            }

            foreach (Project project in Workspaces)
            {
                project.NotifyLastLaunchedChanged();
            }
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

            using Process process = new();
            process.StartInfo = ModuleProcess.CreateStartInfo(launcherPath, $"{projectId} {(int)invokePoint}");
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(launcherPath);

            try
            {
                process.Start();

                // A launch can legitimately wait for the user's trust/elevation decision.
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

            // Snapshot tool is in the parent directory
            var parentDir = Path.GetDirectoryName(exeDir);
            var snapshotUtilsPath = Path.Combine(parentDir, "PowerToys.WorkspacesSnapshotTool.exe");

            if (!File.Exists(snapshotUtilsPath))
            {
                // Fallback: try same directory
                snapshotUtilsPath = Path.Combine(exeDir, "PowerToys.WorkspacesSnapshotTool.exe");
            }

            Process process = new Process();
            process.StartInfo = ModuleProcess.CreateStartInfo(snapshotUtilsPath, isExistingProjectLaunched ? $"{(int)InvokePoint.LaunchAndEdit}" : string.Empty);
            process.StartInfo.WorkingDirectory = Path.GetDirectoryName(snapshotUtilsPath);

            try
            {
                process.Start();
                if (!process.WaitForExit(120_000))
                {
                    Logger.LogWarning("Snapshot tool did not exit within 120 seconds.");
                    process.Kill();
                }
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

        private void CheckShortcutPresence(Project project)
        {
            project.IsShortcutNeeded = _shortcuts.Exists(project);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _lastUpdatedTimer?.Stop();
                StrongReferenceMessenger.Default.UnregisterAll(this);
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
