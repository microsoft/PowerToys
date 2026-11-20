// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private Project _draft;
        private Project _launchingDraft;
        private Project _snapshotBase;
        private bool _isNewProject;
        private bool _snapshotRequested;
        private bool _reloadAfterEdit;
        private int _editGeneration;
        private bool _ownsPublishedSnapshot;
        private Project _projectBeforeLaunch;
        private string _projectNameBeingEdited;
        private Microsoft.UI.Xaml.DispatcherTimer _lastUpdatedTimer;
        private WorkspacesSettings _settings;
        private bool _isDisposed;

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
            _workspacesEditorIO.CleanupOrphanedRequests();
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

        [ObservableProperty]
        public partial bool SaveFailed { get; set; }

        public void SaveProject(Project projectToSave) => TrySaveProject(projectToSave);

        private bool TrySaveProject(Project projectToSave)
        {
            if (_isDisposed || projectToSave == null || !ReferenceEquals(_draft, projectToSave) || !projectToSave.CanBeSaved)
            {
                return false;
            }

            var projects = Workspaces.ToList();
            int index = projects.FindIndex(project => SameId(project.Id, projectToSave.Id));
            if (index < 0)
            {
                return false;
            }

            Project original = projects[index];
            string previousName = original.Name;
            EditEvent editEvent = EditEventFactory.Create(original, projectToSave);
            Project saved = PrepareForSave(projectToSave, original);
            projects[index] = saved;
            if (!TryPersist(projects))
            {
                return false;
            }

            original.Name = saved.Name;
            original.IsShortcutNeeded = saved.IsShortcutNeeded;
            original.MoveExistingWindows = saved.MoveExistingWindows;
            original.Monitors.Clear();
            original.Monitors.AddRange(saved.Monitors);
            original.Applications = saved.Applications;
            foreach (var app in original.Applications)
            {
                app.Parent = original;
            }

            original.NotifyApplicationsChanged();
            original.InitializePreview();
            _shortcuts.Apply(original, previousName);
            RefreshWorkspacesView();
            _writeEditTelemetry(editEvent);
            return true;
        }

        public void EditProject(Project selectedProject, bool isNewlyCreated = false)
        {
            if (_isDisposed || selectedProject == null)
            {
                return;
            }

            EndEditSession();
            OpenEditor(selectedProject, isNewlyCreated);
        }

        private void OpenEditor(Project selectedProject, bool isNewlyCreated)
        {
            _isNewProject = isNewlyCreated;
            var draft = isNewlyCreated ? selectedProject : new Project(selectedProject);
            if (isNewlyCreated)
            {
                string defaultNamePrefix = GetString("DefaultWorkspaceNamePrefix");
                draft.Name = WorkspaceNameGenerator.Create(defaultNamePrefix, Workspaces.Select(project => project.Name));
            }

            CheckShortcutPresence(draft);
            draft.EditorWindowTitle = isNewlyCreated ? GetString("CreateWorkspace") : GetString("EditWorkspace");
            _lastUpdatedTimer?.Stop();
            ShowDraft(draft);
        }

        private void ShowDraft(Project project)
        {
            _draft = project;
            _draft.InitializePreview();
            StrongReferenceMessenger.Default.Send(new NavigateToEditorMessage(_draft));
        }

        private static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static Project PrepareForSave(Project draft, Project original)
        {
            var saved = new Project(draft, original);
            saved.Applications.RemoveAll(app => !app.IsIncluded);
            saved.CloseExpanders();
            return saved;
        }

        private bool TryPersist(List<Project> projects)
        {
            try
            {
                _serializeWorkspaces(projects);
                SaveFailed = false;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save workspaces", ex);
                SaveFailed = true;
                return false;
            }
        }

        public void AddNewProject(Project project) => TryAddNewProject(project);

        private bool TryAddNewProject(Project project)
        {
            if (_isDisposed || project == null || !project.CanBeSaved || Workspaces.Any(existing => SameId(existing.Id, project.Id)))
            {
                return false;
            }

            var saved = PrepareForSave(project, project);
            if (!TryPersist([.. Workspaces, saved]))
            {
                return false;
            }

            saved.InitializePreview();
            Workspaces.Add(saved);
            RefreshWorkspacesView();
            _shortcuts.Apply(saved);

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.CreateEvent
            {
                Successful = true,
                NumScreens = saved.Monitors.Count,
                AppCount = saved.Applications.Count,
                CliCount = saved.Applications.Count(app => !string.IsNullOrEmpty(app.CommandLineArguments)),
                AdminCount = saved.Applications.Count(app => app.IsElevated),
                ShortcutCreated = saved.IsShortcutNeeded,
            });
            return true;
        }

        public void DeleteProject(Project selectedProject) => TryDeleteProject(selectedProject);

        private bool TryDeleteProject(Project selectedProject)
        {
            if (_isDisposed || selectedProject == null)
            {
                return false;
            }

            var projects = Workspaces.ToList();
            if (!projects.Remove(selectedProject) || !TryPersist(projects))
            {
                return false;
            }

            Workspaces.Remove(selectedProject);
            _shortcuts.Remove(selectedProject);
            RefreshWorkspacesView();
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.DeleteEvent { Successful = true });
            return true;
        }

        public void SaveOrAddProject(Project projectToSave)
        {
            if (_draft == null || !ReferenceEquals(_draft, projectToSave))
            {
                return;
            }

            if (_isNewProject ? TryAddNewProject(projectToSave) : TrySaveProject(projectToSave))
            {
                SwitchToMainView();
            }
        }

        public void DeleteProjectById(string projectId)
        {
            if (_draft == null || !SameId(_draft.Id, projectId))
            {
                return;
            }

            var existing = Workspaces.FirstOrDefault(project => SameId(project.Id, projectId));
            if ((_isNewProject && existing == null) || (existing != null && TryDeleteProject(existing)))
            {
                SwitchToMainView();
            }
        }

        public void SwitchToMainView()
        {
            if (_isDisposed)
            {
                return;
            }

            EndEditSession();
            if (_reloadAfterEdit)
            {
                ReloadOverview();
            }

            StrongReferenceMessenger.Default.Send(new GoBackMessage());
            _lastUpdatedTimer?.Start();
        }

        private void EndEditSession()
        {
            ++_editGeneration;
            _draft = null;
            _launchingDraft = null;
            _snapshotBase = null;
            _projectBeforeLaunch = null;
            _snapshotRequested = false;
            _isNewProject = false;
            IsLoading = false;
            SaveFailed = false;
            DiscardPublishedSnapshot();
        }

        private void DiscardPublishedSnapshot()
        {
            if (_ownsPublishedSnapshot)
            {
                WorkspacesEditorIO.DeleteTemporaryFile(_workspacesEditorIO.TempProjectPath);
                _ownsPublishedSnapshot = false;
            }
        }

        [RelayCommand]
        public Task LaunchProjectAsync(Project project) => LaunchFromOverviewAsync(project, false);

        public Task LaunchProjectAndExitAsync(Project project) => LaunchFromOverviewAsync(project, true);

        private async Task LaunchFromOverviewAsync(Project project, bool exitAfterLaunch)
        {
            int generation = _editGeneration;
            if (_isDisposed || project == null)
            {
                return;
            }

            if (!await _workspacesEditorIO.LaunchAsync(project.Id, InvokePoint.EditorButton) || _isDisposed)
            {
                return;
            }

            if (_draft != null || _snapshotRequested || IsLoading)
            {
                // Reloading replaces the canonical models. Keep the active editor's
                // object references until the user finishes with that draft.
                _reloadAfterEdit = true;
                return;
            }

            ReloadOverview();
            if (exitAfterLaunch && IsCurrent(generation))
            {
                Logger.LogInfo($"Launched the Workspace {project.Name}. Exiting.");
                StrongReferenceMessenger.Default.Send(new CloseApplicationMessage());
            }
        }

        private void ReloadOverview()
        {
            _reloadAfterEdit = false;
            if (_workspacesEditorIO.ParseWorkspaces(this).Result)
            {
                foreach (Project workspace in Workspaces)
                {
                    workspace.InitializePreview();
                }

                RefreshWorkspacesView();
            }
        }

        public void EnterSnapshotMode(bool isExistingProjectLaunched)
        {
            if (_isDisposed || _snapshotRequested || (isExistingProjectLaunched && _snapshotBase == null))
            {
                return;
            }

            if (!isExistingProjectLaunched)
            {
                EndEditSession();
            }

            _snapshotRequested = true;
            StrongReferenceMessenger.Default.Send(new MinimizeWindowMessage());
            StrongReferenceMessenger.Default.Send(new ShowSnapshotWindowMessage());
        }

        internal void CancelSnapshot()
        {
            if (_isDisposed || !_snapshotRequested)
            {
                return;
            }

            _snapshotRequested = false;
            _snapshotBase = null;
            ++_editGeneration;
            DiscardPublishedSnapshot();

            StrongReferenceMessenger.Default.Send(new RestoreWindowMessage());
        }

        private bool IsCurrent(int generation) => !_isDisposed && generation == _editGeneration;

        [RelayCommand]
        internal async Task SnapWorkspaceAsync()
        {
            if (_isDisposed || !_snapshotRequested)
            {
                return;
            }

            _snapshotRequested = false;
            int generation = _editGeneration;
            Project beforeLaunch = _snapshotBase == null ? null : new Project(_snapshotBase);
            _snapshotBase = null;
            var requestId = Guid.NewGuid();
            StrongReferenceMessenger.Default.Send(new RestoreWindowMessage());
            IsLoading = true;
            try
            {
                if (!await _workspacesEditorIO.CaptureAsync(requestId, beforeLaunch != null) || !IsCurrent(generation))
                {
                    return;
                }

                Project captured = _workspacesEditorIO.AcceptCapture(requestId);
                if (captured == null)
                {
                    return;
                }

                _ownsPublishedSnapshot = true;
                if (beforeLaunch == null)
                {
                    OpenEditor(captured, true);
                }
                else
                {
                    _projectBeforeLaunch = beforeLaunch;
                    captured.UpdateAfterLaunchAndEdit(beforeLaunch);
                    captured.EditorWindowTitle = beforeLaunch.EditorWindowTitle;
                    ShowDraft(captured);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogError("Failed to publish the workspace capture", ex);
            }
            finally
            {
                WorkspacesEditorIO.DeleteTemporaryFile(_workspacesEditorIO.RequestPath(requestId));
                if (IsCurrent(generation))
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        internal async Task LaunchAndEditAsync(Project project)
        {
            if (_isDisposed || project == null || !ReferenceEquals(project, _draft) || !project.CanBeSaved ||
                ReferenceEquals(project, _launchingDraft) || _snapshotRequested || IsLoading)
            {
                return;
            }

            int generation = ++_editGeneration;
            _launchingDraft = project;
            var requestId = Guid.NewGuid();
            string draftPath = _workspacesEditorIO.RequestPath(requestId);
            try
            {
                try
                {
                    _workspacesEditorIO.WriteDraft(project, draftPath);
                    SaveFailed = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to write the workspace draft for launch", ex);
                    SaveFailed = true;
                    return;
                }

                if (await _workspacesEditorIO.LaunchAsync(project.Id, InvokePoint.LaunchAndEdit, requestId) && IsCurrent(generation))
                {
                    _snapshotBase = project;
                    EnterSnapshotMode(true);
                }
            }
            finally
            {
                WorkspacesEditorIO.DeleteTemporaryFile(draftPath);
                if (ReferenceEquals(_launchingDraft, project))
                {
                    _launchingDraft = null;
                }
            }
        }

        internal void RevertLaunch()
        {
            if (!_isDisposed && _projectBeforeLaunch != null)
            {
                ++_editGeneration;
                IsLoading = false;
                var beforeLaunch = _projectBeforeLaunch;
                _projectBeforeLaunch = null;
                ShowDraft(beforeLaunch);
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
                EndEditSession();
                _lastUpdatedTimer?.Stop();
                StrongReferenceMessenger.Default.UnregisterAll(this);
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
