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
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private WorkspacesEditorIO _workspacesEditorIO;
        private Project _editedProject;
        private Project _projectBeforeLaunch;
        private string _projectNameBeingEdited;
        private Timer _lastUpdatedTimer;
        private WorkspacesSettings _settings;
        private bool _isDisposed;
        private bool _isExistingProjectLaunched;

        public ObservableCollection<Project> Workspaces { get; set; } = new ObservableCollection<Project>();

        public List<Project> WorkspacesView
        {
            get
            {
                IEnumerable<Project> workspaces = GetFilteredWorkspaces();
                IsWorkspacesViewEmpty = !(workspaces != null && workspaces.Any());
                OnPropertyChanged(nameof(IsWorkspacesViewEmpty));
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

                    OnPropertyChanged(nameof(EmptyWorkspacesViewMessage));

                    return new List<Project>();
                }

                WorkspacesData.OrderBy orderBy = (WorkspacesData.OrderBy)OrderByIndex;
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
            if (string.IsNullOrEmpty(SearchTerm))
            {
                return Workspaces;
            }

            return Workspaces.Where(x =>
            {
                if (x.Name.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                if (x.Applications == null)
                {
                    return false;
                }

                return x.Applications.Any(app => app.AppName.Contains(SearchTerm, StringComparison.InvariantCultureIgnoreCase));
            });
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WorkspacesView))]
        private string _searchTerm;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WorkspacesView))]
        private int _orderByIndex;

        partial void OnOrderByIndexChanged(int value)
        {
            _settings.Properties.SortBy = (WorkspacesProperties.SortByProperty)value;
            _settings.Save(SettingsUtils.Default);
        }

        public Action GoBackAction { get; set; }

        public Action MinimizeMainWindowAction { get; set; }

        public Action RestoreMainWindowAction { get; set; }

        public Action ShowLoadingAction { get; set; }

        public Action HideLoadingAction { get; set; }

        public MainViewModel(WorkspacesEditorIO workspacesEditorIO)
        {
            _settings = Utils.Settings.ReadSettings();
            OrderByIndex = (int)_settings.Properties.SortBy;
            _workspacesEditorIO = workspacesEditorIO;
            _lastUpdatedTimer = new Timer();
            _lastUpdatedTimer.Interval = 1000;
            _lastUpdatedTimer.Elapsed += LastUpdatedTimerElapsed;
            _lastUpdatedTimer.Start();

            WeakReferenceMessenger.Default.Register<SnapshotCapturedMessage>(this, (r, m) => ((MainViewModel)r).OnSnapshotCaptured());
            WeakReferenceMessenger.Default.Register<SnapshotCancelledMessage>(this, (r, m) => ((MainViewModel)r).CancelSnapshot());
        }

        private void OnSnapshotCaptured()
        {
            _ = SnapWorkspaceAsync();
        }

        public void Initialize()
        {
            foreach (Project project in Workspaces)
            {
                project.InitializePreview();
            }

            OnPropertyChanged(nameof(WorkspacesView));
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
            ApplyShortcut(_editedProject);

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.EditEvent { Successful = true, PixelAdjustmentsUsed = projectToSave.IsPositionChangedManually });
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
            WeakReferenceMessenger.Default.Send(new NavigateToEditorMessage(selectedProject));
        }

        public void AddNewProject(Project project)
        {
            project.Applications.RemoveAll(app => !app.IsIncluded);
            project.InitializePreview();
            Workspaces.Add(project);
            _workspacesEditorIO.SerializeWorkspaces(Workspaces.ToList());
            TempProjectData.DeleteTempFile();
            OnPropertyChanged(nameof(WorkspacesView));
            ApplyShortcut(project);

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
            _workspacesEditorIO.SerializeWorkspaces(Workspaces.ToList());
            RemoveShortcut(selectedProject);
            OnPropertyChanged(nameof(WorkspacesView));

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.DeleteEvent { Successful = true });
        }

        public void SwitchToMainView()
        {
            GoBackAction?.Invoke();
            SearchTerm = string.Empty;
            OnPropertyChanged(nameof(SearchTerm));
            _lastUpdatedTimer.Start();
            _editedProject = null;
        }

        [RelayCommand]
        public async Task LaunchProjectAsync(Project project)
        {
            if (project == null)
            {
                return;
            }

            await Task.Run(() => RunLauncher(project.Id, InvokePoint.EditorButton));
            if (_workspacesEditorIO.ParseWorkspaces(this).Result == true)
            {
                OnPropertyChanged(nameof(WorkspacesView));
            }
        }

        public async Task LaunchProjectAndExitAsync(Project project)
        {
            if (project == null)
            {
                return;
            }

            await Task.Run(() => RunLauncher(project.Id, InvokePoint.EditorButton));
            if (_workspacesEditorIO.ParseWorkspaces(this).Result == true)
            {
                OnPropertyChanged(nameof(WorkspacesView));
            }

            Logger.LogInfo($"Launched the Workspace {project.Name}. Exiting.");
            WeakReferenceMessenger.Default.Send(new CloseApplicationMessage());
        }

        public void EnterSnapshotMode(bool isExistingProjectLaunched)
        {
            _isExistingProjectLaunched = isExistingProjectLaunched;

            // Minimize the main window
            MinimizeMainWindowAction?.Invoke();

            // Request the View layer to show the snapshot window
            WeakReferenceMessenger.Default.Send(new ShowSnapshotWindowMessage());
        }

        internal void CancelSnapshot()
        {
            RestoreMainWindowAction?.Invoke();
        }

        [RelayCommand]
        internal async Task SnapWorkspaceAsync()
        {
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
                    WeakReferenceMessenger.Default.Send(new NavigateToEditorMessage(project));
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
            await Task.Run(() => RunLauncher(project.Id, InvokePoint.LaunchAndEdit));
            _projectBeforeLaunch = new Project(project);
            EnterSnapshotMode(true);
        }

        internal void RevertLaunch()
        {
            if (_projectBeforeLaunch != null)
            {
                _projectBeforeLaunch.InitializePreview();
                WeakReferenceMessenger.Default.Send(new NavigateToEditorMessage(_projectBeforeLaunch));
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

            // Snapshot tool is in the parent directory
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

        private static string GetDesktopShortcutAddress(Project project) => Path.Combine(WorkspacesCsharpLibrary.Utils.FolderUtils.Desktop(), project.Name + ".lnk");

        private static string GetShortcutStoreAddress(Project project)
        {
            var dataFolder = WorkspacesCsharpLibrary.Utils.FolderUtils.DataFolder();
            Directory.CreateDirectory(dataFolder);
            var shortcutStoreFolder = Path.Combine(dataFolder, "WorkspacesIcons");
            Directory.CreateDirectory(shortcutStoreFolder);
            return Path.Combine(shortcutStoreFolder, project.Id + ".ico");
        }

        private static void ApplyShortcut(Project project)
        {
            if (!project.IsShortcutNeeded)
            {
                RemoveShortcut(project);
                return;
            }

            try
            {
                var basePath = Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath));
                var shortcutAddress = GetDesktopShortcutAddress(project);
                var shortcutIconFilename = GetShortcutStoreAddress(project);

                bool isDarkTheme = true;
                try
                {
                    var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (key?.GetValue("AppsUseLightTheme") is int val && val == 0)
                    {
                        isDarkTheme = true;
                    }
                    else
                    {
                        isDarkTheme = false;
                    }
                }
                catch
                {
                }

                var icon = Utils.WorkspacesIcon.DrawIcon(Utils.WorkspacesIcon.IconTextFromProjectName(project.Name), isDarkTheme);
                Utils.WorkspacesIcon.SaveIcon(icon, shortcutIconFilename);

                File.WriteAllBytes(shortcutAddress, Array.Empty<byte>());

                Shell32.Shell shell = new Shell32.Shell();
                Shell32.Folder dir = shell.NameSpace(WorkspacesCsharpLibrary.Utils.FolderUtils.Desktop());
                Shell32.FolderItem folderItem = dir.Items().Item($"{project.Name}.lnk");
                Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;

                link.Description = $"Project Launcher {project.Id}";
                link.Path = Path.Combine(basePath, "PowerToys.WorkspacesLauncher.exe");
                link.Arguments = $"{project.Id} {(int)InvokePoint.Shortcut}";
                link.WorkingDirectory = basePath;
                link.SetIconLocation(shortcutIconFilename, 0);
                link.Save(shortcutAddress);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Shortcut creation error: {ex.Message}");
            }
        }

        private static void RemoveShortcut(Project project)
        {
            string shortcutAddress = GetDesktopShortcutAddress(project);
            string shortcutIconFilename = GetShortcutStoreAddress(project);

            Logger.LogInfo($"RemoveShortcut: trying to delete '{shortcutAddress}' (exists: {File.Exists(shortcutAddress)})");

            if (File.Exists(shortcutIconFilename))
            {
                File.Delete(shortcutIconFilename);
            }

            if (File.Exists(shortcutAddress))
            {
                File.Delete(shortcutAddress);
                Logger.LogInfo("RemoveShortcut: deleted successfully");
            }
            else
            {
                Logger.LogInfo("RemoveShortcut: file not found at expected path");
            }
        }

        private static void CheckShortcutPresence(Project project)
        {
            string shortcutAddress = Path.Combine(WorkspacesCsharpLibrary.Utils.FolderUtils.Desktop(), project.Name + ".lnk");
            project.IsShortcutNeeded = File.Exists(shortcutAddress);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _lastUpdatedTimer?.Stop();
                _lastUpdatedTimer?.Dispose();
                WeakReferenceMessenger.Default.UnregisterAll(this);
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
