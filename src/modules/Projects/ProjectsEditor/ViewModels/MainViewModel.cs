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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ProjectsEditor.Models;
using ProjectsEditor.Utils;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;
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
                return;
            }

            LaunchProject(Projects.Where(x => x.Id == projectId).First(), true);
        }

        public async void LaunchProject(Project project, bool exitAfterLaunch = false)
        {
            Project actualSetup = await GetActualSetup();

            // Launch apps
            // TODO: move launching to ProjectsLauncher
            List<Application> newlyStartedApps = new List<Application>();
            foreach (Application app in project.Applications.Where(x => x.IsSelected))
            {
                string launchParam = app.AppPath;
                if (string.IsNullOrEmpty(launchParam))
                {
                    continue;
                }

                if (actualSetup.Applications.Exists(x => x.AppPath.Equals(app.AppPath, StringComparison.Ordinal)))
                {
                    // just move the existing window to the desired position
                    Application existingApp = actualSetup.Applications.Where(x => x.AppPath.Equals(app.AppPath, StringComparison.Ordinal)).First();
                    NativeMethods.ShowWindow(existingApp.Hwnd, app.Minimized ? NativeMethods.SW_MINIMIZE : NativeMethods.SW_NORMAL);
                    if (!app.Minimized)
                    {
                        MoveWindowWithScaleAdjustment(project, existingApp.Hwnd, app, true);
                        NativeMethods.SetForegroundWindow(existingApp.Hwnd);
                    }

                    continue;
                }

                if (launchParam.EndsWith("systemsettings.exe", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        string args = string.IsNullOrWhiteSpace(app.CommandLineArguments) ? "home" : app.CommandLineArguments;
                        bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri($"ms-settings:{args}"));
                        newlyStartedApps.Add(app);
                    }
                    catch (Exception)
                    {
                        // todo exception handling
                    }
                }

                // todo: check the user's packaged apps folder
                else if (app.IsPackagedApp)
                {
                    bool started = false;
                    int retryCountLaunch = 50;

                    while (!started && retryCountLaunch > 0)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(app.CommandLineArguments))
                            {
                                // the official app launching method.No parameters are expected
                                var packApp = await GetAppByPackageFamilyNameAsync(app.PackagedId);
                                if (packApp != null)
                                {
                                    await packApp.LaunchAsync();
                                }

                                newlyStartedApps.Add(app);
                                started = true;
                            }
                            else
                            {
                                await Task.Run(() =>
                                {
                                    Process p = new Process();
                                    p.StartInfo = new ProcessStartInfo("cmd.exe");
                                    p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    p.StartInfo.Arguments = $"cmd /c start shell:appsfolder\\{app.Aumid} {app.CommandLineArguments}";
                                    p.EnableRaisingEvents = true;
                                    p.ErrorDataReceived += (sender, e) =>
                                    {
                                        started = false;
                                    };
                                    p.Start();
                                    p.WaitForExit();
                                });
                                newlyStartedApps.Add(app);
                                started = true;
                            }
                        }
                        catch (Exception)
                        {
                            // todo exception handling
                            Thread.Sleep(100);
                        }

                        retryCountLaunch--;
                    }
                }
                else
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            Process p = new Process();
                            p.StartInfo = new ProcessStartInfo(launchParam);
                            p.StartInfo.Arguments = app.CommandLineArguments;
                            p.Start();
                        });
                        newlyStartedApps.Add(app);
                    }
                    catch (Exception)
                    {
                        // try again as admin
                        try
                        {
                            await Task.Run(() =>
                            {
                                Process p = new Process();
                                p.StartInfo = new ProcessStartInfo(launchParam);
                                p.StartInfo.Arguments = app.CommandLineArguments;
                                p.StartInfo.UseShellExecute = true;
                                p.StartInfo.Verb = "runas"; // administrator privilages, some apps start only that way
                                p.Start();
                            });
                            newlyStartedApps.Add(app);
                        }
                        catch (Exception)
                        {
                            // todo exception handling
                        }
                    }
                }
            }

            // the official launching method needs this task.
            static async Task<AppListEntry> GetAppByPackageFamilyNameAsync(string packageFamilyName)
            {
                var pkgManager = new PackageManager();
                var pkg = pkgManager.FindPackagesForUser(string.Empty, packageFamilyName).FirstOrDefault();

                if (pkg == null)
                {
                    return null;
                }

                var apps = await pkg.GetAppListEntriesAsync();
                var firstApp = apps.Any() ? apps[0] : null;
                return firstApp;
            }

            // next step: move newly created apps to their desired position
            // the OS needs some time to show the apps, so do it in multiple steps until all windows all in place
            // retry every 100ms for 5 sec totally
            int retryCount = 50;
            while (newlyStartedApps.Count > 0 && retryCount > 0)
            {
                List<Application> finishedApps = new List<Application>();
                actualSetup = await GetActualSetup();
                foreach (Application app in newlyStartedApps)
                {
                    IEnumerable<Application> candidates = actualSetup.Applications.Where(x => app.IsMyAppPath(x.AppPath));
                    if (candidates.Any())
                    {
                        if (app.AppPath.EndsWith("PowerToys.Settings.exe", StringComparison.Ordinal))
                        {
                            // give it time to not get confused (the app tries to position itself)
                            Thread.Sleep(1000);
                        }
                        else
                        {
                            // the other apps worked fine and reacted correctly to the MoveWindow event, but to be safe, give them also a little time
                            Thread.Sleep(100);
                        }

                        // just move the existing window to the desired position
                        Application existingApp = candidates.First();
                        NativeMethods.ShowWindow(existingApp.Hwnd, app.Minimized ? NativeMethods.SW_MINIMIZE : NativeMethods.SW_NORMAL);
                        if (!app.Minimized)
                        {
                            MoveWindowWithScaleAdjustment(project, existingApp.Hwnd, app, true);
                            NativeMethods.SetForegroundWindow(existingApp.Hwnd);
                        }

                        finishedApps.Add(app);
                    }
                }

                foreach (Application app in finishedApps)
                {
                    newlyStartedApps.Remove(app);
                }

                Thread.Sleep(100);
                retryCount--;
            }

            // update last launched time
            var ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            project.LastLaunchedTime = (long)ts.TotalSeconds;
            _projectsEditorIO.SerializeProjects(Projects.ToList());
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ProjectsView)));

            /*await Task.Run(() => RunLauncher(project.Id));
            if (_projectsEditorIO.ParseProjects(this).Result == true)
            {
                OnPropertyChanged(new PropertyChangedEventArgs("ProjectsView"));
            }*/

            if (exitAfterLaunch)
            {
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

        private void MoveWindowWithScaleAdjustment(Project project, nint hwnd, Application app, bool repaint)
        {
            NativeMethods.SetForegroundWindow(hwnd);

            NativeMethods.MoveWindow(hwnd, app.ScaledPosition.X, app.ScaledPosition.Y, app.ScaledPosition.Width, app.ScaledPosition.Height, repaint);
        }

        private async Task<Project> GetActualSetup()
        {
            string filename = Path.GetTempFileName();
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            await Task.Run(() => RunSnapshotTool(filename));
            Project actualProject;
            _projectsEditorIO.ParseProject(filename, out actualProject);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            return actualProject;
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
