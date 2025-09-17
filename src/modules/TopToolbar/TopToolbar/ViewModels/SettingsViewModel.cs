// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.UI.Dispatching;
using TopToolbar.Models;
using TopToolbar.Services;

namespace TopToolbar.ViewModels
{
    public class SettingsViewModel : ObservableObject, System.IDisposable
    {
        private readonly ToolbarConfigService _service;
        private readonly Timer _saveDebounce = new(300) { AutoReset = false };
        private Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

        public ObservableCollection<ButtonGroup> Groups { get; } = new();

        private ButtonGroup _selectedGroup;

        public ButtonGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                SetProperty(ref _selectedGroup, value);
                OnPropertyChanged(nameof(HasSelectedGroup));
                OnPropertyChanged(nameof(HasNoSelectedGroup));
            }
        }

        private ToolbarButton _selectedButton;

        public ToolbarButton SelectedButton
        {
            get => _selectedButton;
            set
            {
                SetProperty(ref _selectedButton, value);
                OnPropertyChanged(nameof(HasSelectedButton));
            }
        }

        public bool HasSelectedGroup => SelectedGroup != null;

        public bool HasNoSelectedGroup => SelectedGroup == null;

        public bool HasSelectedButton => SelectedButton != null;

        public SettingsViewModel(ToolbarConfigService service)
        {
            _service = service;
            _saveDebounce.Elapsed += async (s, e) =>
            {
                await SaveAsync();
            };

            Groups.CollectionChanged += Groups_CollectionChanged;
        }

        public async Task LoadAsync(DispatcherQueue dispatcher)
        {
            _dispatcher = dispatcher;
            var cfg = await _service.LoadAsync();

            void Apply()
            {
                Groups.Clear();
                foreach (var g in cfg.Groups)
                {
                    Groups.Add(g);
                    HookGroup(g);
                }

                if (SelectedGroup == null && Groups.Count > 0)
                {
                    SelectedGroup = Groups[0];
                    SelectedButton = SelectedGroup.Buttons.FirstOrDefault();
                }
            }

            if (dispatcher.HasThreadAccess)
            {
                Apply();
            }
            else
            {
                var tcs = new TaskCompletionSource();
                dispatcher.TryEnqueue(() =>
                {
                    Apply();
                    tcs.SetResult();
                });
                await tcs.Task;
            }
        }

        public async Task SaveAsync()
        {
            // Ensure we mutate bound properties on UI thread
            if (_dispatcher != null && !_dispatcher.HasThreadAccess)
            {
                var tcs = new TaskCompletionSource();
                _dispatcher.TryEnqueue(async () =>
                {
                    await SaveCoreAsync();
                    tcs.SetResult();
                });
                await tcs.Task;
                return;
            }

            await SaveCoreAsync();
        }

        private async Task SaveCoreAsync()
        {
            // Ensure exe icons extracted before save (robustness if user hit Save quickly)
            ManagedCommon.Logger.LogInfo("SaveCoreAsync: begin icon extraction sweep");
            foreach (var g in Groups)
            {
                foreach (var b in g.Buttons)
                {
                    TryUpdateIconFromCommand(b);
                }
            }

            var cfg = new ToolbarConfig { Groups = Groups.ToList() };
            await _service.SaveAsync(cfg);
            ManagedCommon.Logger.LogInfo("SaveCoreAsync: config saved");
        }

        private void ScheduleSave()
        {
            _saveDebounce.Stop();
            _saveDebounce.Start();
        }

        private void Groups_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.Cast<ButtonGroup>())
                {
                    HookGroup(item);
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.Cast<ButtonGroup>())
                {
                    UnhookGroup(item);
                }
            }

            ScheduleSave();
        }

        private void HookGroup(ButtonGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.PropertyChanged += Group_PropertyChanged;
            group.Buttons.CollectionChanged += Buttons_CollectionChanged;
            foreach (var b in group.Buttons)
            {
                HookButton(b);
            }
        }

        private void UnhookGroup(ButtonGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.PropertyChanged -= Group_PropertyChanged;
            group.Buttons.CollectionChanged -= Buttons_CollectionChanged;
            foreach (var b in group.Buttons)
            {
                UnhookButton(b);
            }
        }

        private void Buttons_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.Cast<ToolbarButton>())
                {
                    HookButton(item);
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.Cast<ToolbarButton>())
                {
                    UnhookButton(item);
                }
            }

            ScheduleSave();
        }

        private void Group_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ScheduleSave();
        }

        private void HookButton(ToolbarButton b)
        {
            if (b == null)
            {
                return;
            }

            b.PropertyChanged += Button_PropertyChanged;
            if (b.Action != null)
            {
                b.Action.PropertyChanged += (s, e) => OnActionPropertyChanged(b, e);
            }
        }

        private void UnhookButton(ToolbarButton b)
        {
            if (b == null)
            {
                return;
            }

            b.PropertyChanged -= Button_PropertyChanged;
        }

        private void Button_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ScheduleSave();
        }

        private void OnActionPropertyChanged(ToolbarButton button, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ToolbarAction.Command))
            {
                // Ensure property changes occur on UI thread
                if (_dispatcher != null && !_dispatcher.HasThreadAccess)
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        TryUpdateIconFromCommand(button);
                        ScheduleSave();
                    });
                }
                else
                {
                    TryUpdateIconFromCommand(button);
                    ScheduleSave();
                }
            }
        }

        private void TryUpdateIconFromCommand(ToolbarButton button)
        {
            var cmd = button?.Action?.Command;
            if (string.IsNullOrWhiteSpace(cmd))
            {
                return;
            }

            string path = cmd.Trim();
            path = Environment.ExpandEnvironmentVariables(path);
            if (path.StartsWith('"'))
            {
                int end = path.IndexOf('"', 1);
                if (end > 1)
                {
                    path = path.Substring(1, end - 1);
                }
            }
            else
            {
                // If not quoted, try to cut at the end of .exe to ignore arguments
                int exeIdx = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIdx >= 0)
                {
                    path = path.Substring(0, exeIdx + 4);
                }
            }

            // Resolve relative/name-only commands (e.g., code, code.exe) via WorkingDirectory and PATH
            var workingDirectory = string.Empty;
            if (button != null && button.Action != null && !string.IsNullOrWhiteSpace(button.Action.WorkingDirectory))
            {
                workingDirectory = button.Action.WorkingDirectory;
            }

            var resolved = ResolveCommandToFilePath(path, workingDirectory);
            if (!string.IsNullOrEmpty(resolved))
            {
                path = resolved;
            }

            ManagedCommon.Logger.LogInfo($"TryUpdateIconFromCommand: cmd='{cmd}', resolvedPath='{path}'");

            var iconsDir = Path.Combine(Path.GetDirectoryName(_service.ConfigPath)!, "icons");
            var target = Path.Combine(iconsDir, button.Id + ".png");

            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                if (IconExtractionService.TryExtractExeIconToPng(path, target))
                {
                    button.IconType = ToolbarIconType.Image;
                    button.IconPath = target;
                    ManagedCommon.Logger.LogInfo($"Extracted exe icon -> '{target}'");
                }

                return;
            }

            // For scripts or other files, try associated icon
            if (File.Exists(path))
            {
                if (IconExtractionService.TryExtractFileIconToPng(path, target))
                {
                    button.IconType = ToolbarIconType.Image;
                    button.IconPath = target;
                    ManagedCommon.Logger.LogInfo($"Extracted file icon -> '{target}'");
                }
            }
        }

        private static string ResolveCommandToFilePath(string file, string workingDir)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return string.Empty;
            }

            try
            {
                var candidate = file.Trim();
                candidate = Environment.ExpandEnvironmentVariables(candidate);

                bool hasRoot = System.IO.Path.IsPathRooted(candidate);
                bool hasExt = System.IO.Path.HasExtension(candidate);

                // If absolute or contains directory, try directly and with PATHEXT if needed
                if (hasRoot || candidate.Contains('\\') || candidate.Contains('/'))
                {
                    if (System.IO.File.Exists(candidate))
                    {
                        return candidate;
                    }

                    if (!hasExt)
                    {
                        foreach (var ext in GetPathExtensions())
                        {
                            var p = candidate + ext;
                            if (System.IO.File.Exists(p))
                            {
                                return p;
                            }
                        }
                    }

                    // If a specific extension was provided but file not found, try alternate PATHEXT extensions
                    if (hasExt)
                    {
                        var dirName = System.IO.Path.GetDirectoryName(candidate) ?? string.Empty;
                        var nameNoExtOnly = System.IO.Path.GetFileNameWithoutExtension(candidate);
                        var nameNoExt = string.IsNullOrEmpty(dirName) ? nameNoExtOnly : System.IO.Path.Combine(dirName, nameNoExtOnly);
                        foreach (var ext in GetPathExtensions())
                        {
                            var p = nameNoExt + ext;
                            if (System.IO.File.Exists(p))
                            {
                                return p;
                            }
                        }
                    }

                    return string.Empty;
                }

                // Build search dirs: workingDir, current dir, PATH
                var dirs = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(workingDir) && System.IO.Directory.Exists(workingDir))
                {
                    dirs.Add(workingDir);
                }

                dirs.Add(Environment.CurrentDirectory);
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var d in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    dirs.Add(d);
                }

                foreach (var dir in dirs)
                {
                    var basePath = System.IO.Path.Combine(dir, candidate);
                    if (hasExt)
                    {
                        if (System.IO.File.Exists(basePath))
                        {
                            return basePath;
                        }

                        // Also try alternate extensions if the given one is not found in this dir
                        var nameNoExtOnly = System.IO.Path.GetFileNameWithoutExtension(candidate);
                        var nameNoExt = System.IO.Path.Combine(dir, nameNoExtOnly);
                        foreach (var ext in GetPathExtensions())
                        {
                            var p = nameNoExt + ext;
                            if (System.IO.File.Exists(p))
                            {
                                return p;
                            }
                        }
                    }
                    else
                    {
                        foreach (var ext in GetPathExtensions())
                        {
                            var p = basePath + ext;
                            if (System.IO.File.Exists(p))
                            {
                                return p;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static System.Collections.Generic.IEnumerable<string> GetPathExtensions()
        {
            var pathext = Environment.GetEnvironmentVariable("PATHEXT");
            if (string.IsNullOrWhiteSpace(pathext))
            {
                return new[] { ".COM", ".EXE", ".BAT", ".CMD", ".VBS", ".JS", ".WS", ".MSC", ".PS1" };
            }

            return pathext.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        }

        public void AddGroup()
        {
            Groups.Add(new ButtonGroup { Name = "New Group" });
            SelectedGroup = Groups.LastOrDefault();
            SelectedButton = SelectedGroup?.Buttons.FirstOrDefault();
            ScheduleSave();
        }

        public void RemoveGroup(ButtonGroup group)
        {
            Groups.Remove(group);
            if (SelectedGroup == group)
            {
                SelectedGroup = Groups.FirstOrDefault();
                SelectedButton = SelectedGroup?.Buttons.FirstOrDefault();
            }

            ScheduleSave();
        }

        public void AddButton(ButtonGroup group)
        {
            group.Buttons.Add(new ToolbarButton { Name = "New Button", IconGlyph = "\uE10F", Action = new ToolbarAction { Command = "notepad.exe" } });

            SelectedGroup = group;
            SelectedButton = group.Buttons.LastOrDefault();
            ScheduleSave();
        }

        public void RemoveButton(ButtonGroup group, ToolbarButton button)
        {
            var removedIndex = group.Buttons.IndexOf(button);
            if (removedIndex < 0)
            {
                return;
            }

            group.Buttons.RemoveAt(removedIndex);

            if (SelectedButton == button)
            {
                if (group.Buttons.Count > 0)
                {
                    var newIndex = Math.Min(removedIndex, group.Buttons.Count - 1);
                    SelectedButton = group.Buttons[newIndex];
                }
                else
                {
                    SelectedButton = null;
                }
            }

            ScheduleSave();
        }

        public void Dispose()
        {
            _saveDebounce?.Stop();
            _saveDebounce?.Dispose();

            Groups.CollectionChanged -= Groups_CollectionChanged;

            foreach (var g in Groups)
            {
                UnhookGroup(g);
            }

            System.GC.SuppressFinalize(this);
        }
    }
}
