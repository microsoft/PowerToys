// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
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
        private readonly Action<Project, Project> _persistChange;
        private readonly Func<IEnumerable<string>> _storedWorkspaceIds;
        private readonly IWorkspaceShortcutService _shortcuts;
        private readonly Action<EditEvent> _writeEditTelemetry;
        private readonly Action<CreateEvent> _writeCreateTelemetry;
        private Project _draft;
        private Project _launchingDraft;
        private Project _snapshotBase;
        private bool _isNewProject;
        private bool _snapshotRequested;
        private bool _reloadAfterEdit;
        private int _editGeneration;
        private bool _shortcutPresenceUnknown;
        private bool _initialShortcutChoice;
        private bool _showingPendingCleanup;
        private long? _nextShortcutCleanup;
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
            IsWorkspacesViewEmpty = isEmpty && !WorkspaceReadFailed;

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
        [NotifyPropertyChangedFor(nameof(CanLaunchAndEdit))]
        [NotifyPropertyChangedFor(nameof(CanRecaptureCurrentLayout))]
        public partial bool IsLoading { get; set; }

        private bool CanStartEditAction => !_isDisposed && _draft != null && _launchingDraft == null && !_snapshotRequested && !IsLoading;

        public bool CanSaveDraft => !WorkspaceSaveConflict && _draft?.CanBeSaved == true;

        public bool CanDeleteDraft => !WorkspaceSaveConflict && _draft != null;

        public bool CanLaunchAndEdit => CanStartEditAction && _draft.CanBeSaved;

        public bool CanRecaptureCurrentLayout => CanStartEditAction && _draft.HasUnresolvedMonitors;

        public MainViewModel(WorkspacesEditorIO workspacesEditorIO)
            : this(
                workspacesEditorIO,
                Utils.Settings.ReadSettings(),
                settings => settings.Save(SettingsUtils.Default),
                new WorkspaceShortcutService(),
                null,
                telemetryEvent => PowerToysTelemetry.Log.WriteEvent(telemetryEvent),
                telemetryEvent => PowerToysTelemetry.Log.WriteEvent(telemetryEvent))
        {
        }

        internal MainViewModel(
            WorkspacesEditorIO workspacesEditorIO,
            WorkspacesSettings settings,
            Action<WorkspacesSettings> saveSettings,
            IWorkspaceShortcutService shortcuts,
            Action<List<Project>> serializeWorkspaces,
            Action<EditEvent> writeEditTelemetry)
            : this(workspacesEditorIO, settings, saveSettings, shortcuts, serializeWorkspaces, writeEditTelemetry, telemetryEvent => PowerToysTelemetry.Log.WriteEvent(telemetryEvent))
        {
        }

        internal MainViewModel(
            WorkspacesEditorIO workspacesEditorIO,
            WorkspacesSettings settings,
            Action<WorkspacesSettings> saveSettings,
            IWorkspaceShortcutService shortcuts,
            Action<List<Project>> serializeWorkspaces,
            Action<EditEvent> writeEditTelemetry,
            Action<CreateEvent> writeCreateTelemetry)
        {
            ArgumentNullException.ThrowIfNull(workspacesEditorIO);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(saveSettings);
            ArgumentNullException.ThrowIfNull(shortcuts);
            ArgumentNullException.ThrowIfNull(writeEditTelemetry);
            ArgumentNullException.ThrowIfNull(writeCreateTelemetry);

            _workspacesEditorIO = workspacesEditorIO;
            _settings = settings;
            _saveSettings = saveSettings;
            _shortcuts = shortcuts;
            _persistChange = serializeWorkspaces == null
                ? workspacesEditorIO.PersistChange
                : (original, replacement) => serializeWorkspaces(WorkspacesAfterChange(original, replacement));
            _storedWorkspaceIds = serializeWorkspaces == null
                ? workspacesEditorIO.GetStoredWorkspaceIds
                : () => Workspaces.Select(project => project.Id);
            _writeEditTelemetry = writeEditTelemetry;
            _writeCreateTelemetry = writeCreateTelemetry;
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
            ReloadWorkspaces();
            RetryPendingShortcutRemovals();

            // Create DispatcherTimer here (requires UI thread / DispatcherQueue to exist)
            _lastUpdatedTimer = new Microsoft.UI.Xaml.DispatcherTimer();
            _lastUpdatedTimer.Interval = TimeSpan.FromSeconds(1);
            _lastUpdatedTimer.Tick += LastUpdatedTimerTick;
            _lastUpdatedTimer.Start();
        }

        [ObservableProperty]
        public partial bool SaveFailed { get; set; }

        [ObservableProperty]
        public partial bool WorkspaceReadFailed { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSaveDraft))]
        [NotifyPropertyChangedFor(nameof(CanDeleteDraft))]
        public partial bool WorkspaceSaveConflict { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasShortcutNotice))]
        public partial string ShortcutNotice { get; set; }

        public bool HasShortcutNotice => !string.IsNullOrEmpty(ShortcutNotice);

        internal void DismissShortcutNotice() => SetShortcutNotice(null);

        partial void OnSaveFailedChanged(bool value)
        {
            if (value)
            {
                DismissShortcutNotice();
            }
        }

        public void SaveProject(Project projectToSave) => TrySaveProject(projectToSave);

        private bool TrySaveProject(Project projectToSave)
        {
            if (_isDisposed || WorkspaceSaveConflict || projectToSave == null || !ReferenceEquals(_draft, projectToSave) || !projectToSave.CanBeSaved)
            {
                return false;
            }

            Project original = Workspaces.FirstOrDefault(project => SameId(project.Id, projectToSave.Id));
            if (original == null)
            {
                ReportWorkspaceConflict();
                return false;
            }

            string previousName = original.Name;
            EditEvent editEvent = EditEventFactory.Create(original, projectToSave);
            Project saved = PrepareForSave(projectToSave, original);
            bool applyShortcut = ResolveShortcutIntent(original, saved);
            if (!string.Equals(original.Name, saved.Name, StringComparison.Ordinal))
            {
                RetryPendingShortcutRemovals();
                if (!TryPrepareShortcutChange(original, deleting: false))
                {
                    return false;
                }
            }

            if (!TryPersist(original, saved))
            {
                return false;
            }

            original.Name = saved.Name;
            original.IsShortcutNeeded = saved.IsShortcutNeeded;
            original.IsPositionChangedManually = false;
            original.NeedsApplicationIdRepair = false;
            original.UpdateLastLaunchedTime(saved.LastLaunchedTime);
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
            if (applyShortcut)
            {
                var shortcut = _shortcuts.ApplyWithResult(original, previousName);
                ReportShortcutOutcome(shortcut);
                if (shortcut.Status == WorkspaceShortcutStatus.Success)
                {
                    _shortcutPresenceUnknown = false;
                    _initialShortcutChoice = saved.IsShortcutNeeded;
                    projectToSave.IsShortcutNeeded = saved.IsShortcutNeeded;
                }
            }
            else
            {
                SetShortcutNotice("ShortcutUpdateFailedAfterSave");
            }

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

            if (!isNewlyCreated && _reloadAfterEdit)
            {
                if (!ReloadOverview())
                {
                    return;
                }

                selectedProject = Workspaces.FirstOrDefault(project => SameId(project.Id, selectedProject.Id));
                if (selectedProject == null)
                {
                    return;
                }
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

            draft.IsPositionChangedManually = false;
            bool? presence = _shortcuts.GetPresence(draft);
            _shortcutPresenceUnknown = !presence.HasValue;
            draft.IsShortcutNeeded = presence ?? draft.IsShortcutNeeded;
            _initialShortcutChoice = draft.IsShortcutNeeded;
            draft.EditorWindowTitle = isNewlyCreated ? GetString("CreateWorkspace") : GetString("EditWorkspace");
            _lastUpdatedTimer?.Stop();
            ShowDraft(draft);
        }

        private void ShowDraft(Project project)
        {
            if (_draft != null)
            {
                _draft.PropertyChanged -= OnDraftPropertyChanged;
            }

            _draft = project;
            _draft.PropertyChanged += OnDraftPropertyChanged;
            NotifyEditActions();
            _draft.InitializePreview();
            StrongReferenceMessenger.Default.Send(new NavigateToEditorMessage(_draft));
        }

        private void OnDraftPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Project.CanBeSaved) or nameof(Project.HasUnresolvedMonitors))
            {
                NotifyEditActions();
            }
        }

        private void NotifyEditActions()
        {
            OnPropertyChanged(nameof(CanSaveDraft));
            OnPropertyChanged(nameof(CanDeleteDraft));
            OnPropertyChanged(nameof(CanLaunchAndEdit));
            OnPropertyChanged(nameof(CanRecaptureCurrentLayout));
        }

        private static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static Project PrepareForSave(Project draft, Project original)
        {
            var saved = new Project(draft, original);
            saved.Applications.RemoveAll(app => !app.IsIncluded);
            saved.CloseExpanders();
            return saved;
        }

        private List<Project> WorkspacesAfterChange(Project original, Project replacement)
        {
            var result = Workspaces.ToList();
            if (original == null)
            {
                result.Add(replacement);
            }
            else if (replacement == null)
            {
                result.Remove(original);
            }
            else
            {
                result[result.IndexOf(original)] = replacement;
            }

            return result;
        }

        private bool TryPersist(Project original, Project replacement)
        {
            if (WorkspaceSaveConflict)
            {
                return false;
            }

            try
            {
                _persistChange(original, replacement);
                SaveFailed = false;
                return true;
            }
            catch (WorkspaceConflictException ex)
            {
                Logger.LogError("The workspace changed before it could be saved", ex);
                ReportWorkspaceConflict();
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save workspaces", ex);
                SaveFailed = true;
                return false;
            }
        }

        private void ReportWorkspaceConflict()
        {
            SaveFailed = false;
            DismissShortcutNotice();
            WorkspaceSaveConflict = true;
            _reloadAfterEdit = true;
        }

        public void AddNewProject(Project project) => TryAddNewProject(project);

        private bool TryAddNewProject(Project project)
        {
            if (_isDisposed || WorkspaceSaveConflict || project == null || !project.CanBeSaved)
            {
                return false;
            }

            if (Workspaces.Any(existing => SameId(existing.Id, project.Id)))
            {
                ReportWorkspaceConflict();
                return false;
            }

            var saved = PrepareForSave(project, project);
            RetryPendingShortcutRemovals();
            if (!TryPersist(null, saved))
            {
                return false;
            }

            saved.InitializePreview();
            Workspaces.Add(saved);
            RefreshWorkspacesView();
            var shortcut = _shortcuts.ApplyWithResult(saved);
            ReportShortcutOutcome(shortcut);

            _writeCreateTelemetry(new CreateEvent
            {
                Successful = true,
                NumScreens = saved.Monitors.Count,
                AppCount = saved.Applications.Count,
                CliCount = saved.Applications.Count(app => !string.IsNullOrEmpty(app.CommandLineArguments)),
                AdminCount = saved.Applications.Count(app => app.IsElevated),
                ShortcutCreated = shortcut.Created,
            });
            return true;
        }

        public void DeleteProject(Project selectedProject) => TryDeleteProject(selectedProject);

        private bool TryDeleteProject(Project selectedProject)
        {
            if (_isDisposed || WorkspaceSaveConflict || selectedProject == null)
            {
                return false;
            }

            if (!Workspaces.Contains(selectedProject) || !TryPrepareShortcutChange(selectedProject, deleting: true) || !TryPersist(selectedProject, null))
            {
                return false;
            }

            Workspaces.Remove(selectedProject);
            var shortcut = _shortcuts.RemoveWithResult(selectedProject);
            SetShortcutNotice(shortcut.Status == WorkspaceShortcutStatus.Success ? null : "ShortcutCleanupPendingAfterDelete");
            if (shortcut.Status != WorkspaceShortcutStatus.Success)
            {
                _nextShortcutCleanup = Environment.TickCount64 + 30_000;
            }

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
            else if (existing == null)
            {
                ReportWorkspaceConflict();
            }
        }

        public void SwitchToMainView()
        {
            if (_isDisposed)
            {
                return;
            }

            EndEditSession();
            ResumeOverview();
            ReturnToOverview();
        }

        private void ReturnToOverview()
        {
            StrongReferenceMessenger.Default.Send(new GoBackMessage());
            _lastUpdatedTimer?.Start();
        }

        private void EndEditSession()
        {
            ++_editGeneration;
            if (_draft != null)
            {
                _draft.PropertyChanged -= OnDraftPropertyChanged;
            }

            _draft = null;
            _launchingDraft = null;
            _snapshotBase = null;
            _projectBeforeLaunch = null;
            _snapshotRequested = false;
            _isNewProject = false;
            IsLoading = false;
            SaveFailed = false;
            WorkspaceSaveConflict = false;
            NotifyEditActions();
            _workspacesEditorIO.DiscardPublishedCapture();
        }

        private void ResumeOverview()
        {
            if (_draft != null || _snapshotRequested || IsLoading || _isDisposed)
            {
                return;
            }

            if (_reloadAfterEdit || WorkspaceReadFailed)
            {
                ReloadOverview();
            }

            RetryPendingShortcutRemovals();
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

            if (project.HasUnresolvedMonitors)
            {
                EditProject(project);
                return;
            }

            if (project.NeedsApplicationIdRepair)
            {
                if (!TryPersist(project, project))
                {
                    return;
                }

                project.NeedsApplicationIdRepair = false;
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

        private bool ReloadOverview()
        {
            WorkspaceReadFailed = !_workspacesEditorIO.ParseWorkspaces(this).Result;
            if (!WorkspaceReadFailed)
            {
                _reloadAfterEdit = false;
            }

            foreach (Project workspace in Workspaces)
            {
                workspace.InitializePreview();
            }

            RefreshWorkspacesView();
            return !WorkspaceReadFailed;
        }

        [RelayCommand]
        internal void ReloadWorkspaces()
        {
            if (_isDisposed)
            {
                return;
            }

            // A retry for a read failure must never erase an active edit. The conflict
            // action explicitly discards changes only after the replacement is available.
            if (_draft != null)
            {
                WorkspaceReadFailed = !_workspacesEditorIO.CanReadWorkspaces();
                _reloadAfterEdit = true;
                return;
            }

            if (ReloadOverview())
            {
                WorkspaceSaveConflict = false;
                SaveFailed = false;
            }
        }

        [RelayCommand]
        internal void DiscardAndReloadWorkspace()
        {
            if (_isDisposed || !WorkspaceSaveConflict || !ReloadOverview())
            {
                return;
            }

            string editedId = _draft?.Id;
            EndEditSession();
            var current = editedId == null ? null : Workspaces.FirstOrDefault(project => SameId(project.Id, editedId));
            if (current != null)
            {
                OpenEditor(current, false);
            }
            else if (editedId != null)
            {
                ReturnToOverview();
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
            NotifyEditActions();
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
            NotifyEditActions();
            _workspacesEditorIO.DiscardPublishedCapture();

            StrongReferenceMessenger.Default.Send(new RestoreWindowMessage());
            ResumeOverview();
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
            Project beforeCapture = _snapshotBase;
            _snapshotBase = null;
            var requestId = Guid.NewGuid();
            IsLoading = true;
            StrongReferenceMessenger.Default.Send(new RestoreWindowMessage());
            try
            {
                if (!await _workspacesEditorIO.CaptureAsync(requestId, beforeCapture != null) || !IsCurrent(generation))
                {
                    return;
                }

                Project captured = _workspacesEditorIO.AcceptCapture(requestId);
                if (captured == null)
                {
                    return;
                }

                if (beforeCapture == null)
                {
                    OpenEditor(captured, true);
                }
                else
                {
                    _projectBeforeLaunch = new Project(_draft);
                    captured.UpdateAfterLaunchAndEdit(beforeCapture, _draft);
                    captured.EditorWindowTitle = _draft.EditorWindowTitle;
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
                    ResumeOverview();
                }
            }
        }

        [RelayCommand]
        internal async Task LaunchAndEditAsync(Project project)
        {
            if (!ReferenceEquals(project, _draft) || !CanLaunchAndEdit)
            {
                return;
            }

            int generation = ++_editGeneration;
            _launchingDraft = project;
            NotifyEditActions();
            var requestId = Guid.NewGuid();
            string draftPath = _workspacesEditorIO.RequestPath(requestId);
            try
            {
                Project launchDraft;
                try
                {
                    // This is the layout the launcher actually received. Keep subsequent
                    // edits distinguishable from the positions returned by the snapshot.
                    launchDraft = new Project(project);
                    _workspacesEditorIO.WriteDraft(launchDraft, draftPath);
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
                    _snapshotBase = launchDraft;
                    EnterSnapshotMode(true);
                }
            }
            finally
            {
                WorkspacesEditorIO.DeleteTemporaryFile(draftPath);
                if (ReferenceEquals(_launchingDraft, project))
                {
                    _launchingDraft = null;
                    NotifyEditActions();
                }
            }
        }

        internal void RecaptureCurrentLayout(Project project)
        {
            if (ReferenceEquals(project, _draft) && CanRecaptureCurrentLayout)
            {
                ++_editGeneration;
                _snapshotBase = new Project(project);
                EnterSnapshotMode(true);
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

            if (_nextShortcutCleanup.HasValue && Environment.TickCount64 >= _nextShortcutCleanup.Value && _draft == null && !_snapshotRequested && !IsLoading)
            {
                RetryPendingShortcutRemovals();
            }
        }

        private static string GetString(string key)
        {
            return ResourceLoaderInstance.ResourceLoader?.GetString(key) ?? key;
        }

        private bool ResolveShortcutIntent(Project original, Project saved)
        {
            if (!_shortcutPresenceUnknown || saved.IsShortcutNeeded != _initialShortcutChoice)
            {
                return true;
            }

            // An unreadable link is not evidence that the user unchecked the box.
            bool? presence = _shortcuts.GetPresence(original);
            if (presence.HasValue)
            {
                saved.IsShortcutNeeded = presence.Value;
                return true;
            }

            saved.IsShortcutNeeded = original.IsShortcutNeeded;
            return false;
        }

        private bool TryPrepareShortcutChange(Project original, bool deleting)
        {
            if (_shortcuts.TryPrepareChange(original, deleting))
            {
                return true;
            }

            SaveFailed = false;
            SetShortcutNotice("ShortcutTrackingFailedBeforeSave");
            return false;
        }

        private void ReportShortcutOutcome(WorkspaceShortcutResult result)
        {
            SetShortcutNotice(result.Status switch
            {
                WorkspaceShortcutStatus.Success => null,
                WorkspaceShortcutStatus.NameConflict => "ShortcutNameConflictAfterSave",
                _ => "ShortcutUpdateFailedAfterSave",
            });
        }

        private void SetShortcutNotice(string resourceKey)
        {
            _showingPendingCleanup = resourceKey == "ShortcutCleanupPendingAfterDelete";
            ShortcutNotice = resourceKey == null ? null : GetString(resourceKey);
        }

        private void RetryPendingShortcutRemovals()
        {
            var ids = _storedWorkspaceIds();
            if (ids != null)
            {
                bool pending = _shortcuts.RetryPendingRemovals(ids);
                _nextShortcutCleanup = pending ? Environment.TickCount64 + 30_000 : null;
                if (!pending && _showingPendingCleanup)
                {
                    SetShortcutNotice(null);
                }
            }
            else
            {
                _nextShortcutCleanup = Environment.TickCount64 + 30_000;
            }
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
