// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using WinUIEx;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Models;
using WorkspacesEditor.Views;

namespace WorkspacesEditor
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        public const int MinWindowWidth = 750;
        public const int MinWindowHeight = 680;

        private const string WindowSettingsFileName = "editor-window.json";
        private const string WorkspacesModuleName = "Workspaces";
        private readonly CancellationTokenSource _cancellationToken = new();
        private readonly IDictionary<string, object> _windowPlacementStorage;
        private System.ComponentModel.PropertyChangedEventHandler _vmPropertyChangedHandler;
        private Thread _hotkeyEventThread;
        private SnapshotWindow _snapshotWindow;
        private OverlayBorder _overlayBorder;
        private bool _isDisposed;

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);

            // Unpackaged WindowEx windows need durable backing storage for persistence.
            _windowPlacementStorage = LoadWindowPlacement();
            WindowManager.PersistenceStorage = _windowPlacementStorage;
            PersistenceId = "WorkspacesEditor";
            this.CenterOnScreen();

            AppWindow.SetIcon("Assets/Workspaces/Workspaces.ico");

            // Set title from resource or fallback
            try
            {
                this.Title = ResourceLoaderInstance.ResourceLoader?.GetString("MainTitle") ?? "Workspaces";
            }
            catch
            {
                this.Title = "Workspaces";
            }

            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            SetTitleBar(AppTitleBar);
            AppTitleBar.Title = this.Title;

            this.Closed += OnClosed;

            // Listen for hotkey toggle event
            StartHotkeyEventLoop(hwnd);

            // Wire ViewModel navigation via messenger
            // Use StrongReferenceMessenger for MainWindow since Window is not rooted
            // in the visual tree and WeakReferenceMessenger may GC the registration.
            var vm = App.MainViewModel;
            StrongReferenceMessenger.Default.Register<NavigateToEditorMessage>(this, (r, m) =>
            {
                if (ContentFrame.Content is Views.WorkspacesEditorPage editorPage)
                {
                    editorPage.UpdateProject(m.Project);
                }
                else
                {
                    ContentFrame.Navigate(typeof(Views.WorkspacesEditorPage), (vm, m.Project));
                }

                SearchBox.Visibility = Visibility.Collapsed;
                AppTitleBar.IsBackButtonVisible = true;
                AppTitleBar.Title = m.Project.EditorWindowTitle;
            });
            StrongReferenceMessenger.Default.Register<GoBackMessage>(this, (r, m) =>
            {
                if (ContentFrame.CanGoBack)
                {
                    ContentFrame.GoBack();
                }

                if (ContentFrame.Content is not Views.MainPage)
                {
                    ContentFrame.Navigate(typeof(Views.MainPage), vm);
                }

                ContentFrame.BackStack.Clear();
                ContentFrame.ForwardStack.Clear();
                SearchBox.Text = string.Empty;
                SearchBox.Visibility = Visibility.Visible;
                AppTitleBar.IsBackButtonVisible = false;
                AppTitleBar.Title = this.Title;
            });
            StrongReferenceMessenger.Default.Register<MinimizeWindowMessage>(this, (r, m) =>
            {
                ShowWindow(WindowNative.GetWindowHandle(this), 6); // SW_MINIMIZE
            });
            StrongReferenceMessenger.Default.Register<RestoreWindowMessage>(this, (r, m) =>
            {
                ShowWindow(WindowNative.GetWindowHandle(this), 9); // SW_RESTORE
            });

            // Listen for snapshot window requests from ViewModel
            StrongReferenceMessenger.Default.Register<ShowSnapshotWindowMessage>(this, (r, m) =>
            {
                ShowSnapshotWindow();
            });

            // Bind loading ring to ViewModel.IsLoading
            _vmPropertyChangedHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsLoading))
                {
                    LoadingRing.IsActive = vm.IsLoading;
                    LoadingRing.Visibility = vm.IsLoading
                        ? Microsoft.UI.Xaml.Visibility.Visible
                        : Microsoft.UI.Xaml.Visibility.Collapsed;
                }
            };
            vm.PropertyChanged += _vmPropertyChangedHandler;

            // Navigate to main page
            ContentFrame.Navigate(typeof(Views.MainPage), vm);

            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Telemetry.WorkspacesEditorStartFinishEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            sender.ItemsSource = App.MainViewModel.SearchWorkspaces(sender.Text).ToList();
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is Project project)
            {
                sender.Text = project.Name;
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var vm = App.MainViewModel;
            var project = args.ChosenSuggestion as Project
                ?? vm.SearchWorkspaces(args.QueryText).FirstOrDefault();

            if (project == null)
            {
                return;
            }

            sender.Text = string.Empty;
            vm.CloseAllPopups();
            vm.EditProject(project);
        }

        private void AppTitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            // Discard any in-progress edits (same behavior as the editor's Cancel), then return to the overview.
            WorkspacesCsharpLibrary.Data.TempProjectData.DeleteTempFile();
            App.MainViewModel.SwitchToMainView();
        }

        private void StartHotkeyEventLoop(IntPtr hwnd)
        {
            var token = _cancellationToken.Token;
            var cancellationHandle = token.WaitHandle;
            _hotkeyEventThread = new Thread(() =>
            {
                using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToys.Interop.Constants.WorkspacesHotkeyEvent());
                var waitHandles = new WaitHandle[] { cancellationHandle, eventHandle };
                while (WaitHandle.WaitAny(waitHandles) == 1 && !token.IsCancellationRequested)
                {
                    App.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_isDisposed)
                        {
                            return;
                        }

                        if (ApplicationIsInFocus())
                        {
                            StrongReferenceMessenger.Default.Send(new CloseApplicationMessage());
                        }
                        else
                        {
                            if (IsIconic(hwnd))
                            {
                                ShowWindow(hwnd, 9); // SW_RESTORE
                            }

                            WindowHelpers.BringToForeground(hwnd);
                        }
                    });
                }
            }) { IsBackground = true };
            _hotkeyEventThread.Start();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            try
            {
                // WindowEx has already written the native placement to its backing dictionary.
                SettingsUtils.Default.SaveSettings(WindowPlacementStorage.Serialize(_windowPlacementStorage), WorkspacesModuleName, WindowSettingsFileName);
            }
            finally
            {
                try
                {
                    Dispose();
                }
                finally
                {
                    (Microsoft.UI.Xaml.Application.Current as App)?.OnMainWindowClosed();
                }
            }
        }

        private static IDictionary<string, object> LoadWindowPlacement()
        {
            try
            {
                var path = SettingsUtils.Default.GetSettingsFilePath(WorkspacesModuleName, WindowSettingsFileName);
                if (File.Exists(path))
                {
                    return WindowPlacementStorage.Deserialize(File.ReadAllText(path));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Logger.LogError("Failed to load Workspaces Editor window placement.", ex);
            }

            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private void ShowSnapshotWindow()
        {
            if (_snapshotWindow != null)
            {
                WindowHelpers.BringToForeground(WindowNative.GetWindowHandle(_snapshotWindow));
                return;
            }

            _overlayBorder = OverlayBorder.CreateForAllMonitors(OverlayBorder.GetAllMonitorBounds());
            _snapshotWindow = new SnapshotWindow();
            _snapshotWindow.Closed += OnSnapshotWindowClosed;
            _snapshotWindow.Activate();
        }

        private void OnSnapshotWindowClosed(object sender, WindowEventArgs args)
        {
            if (sender is not SnapshotWindow window)
            {
                return;
            }

            window.Closed -= OnSnapshotWindowClosed;
            if (!ReferenceEquals(_snapshotWindow, window))
            {
                return;
            }

            _snapshotWindow = null;
            var overlay = _overlayBorder;
            _overlayBorder = null;
            overlay?.Dispose();
        }

        private void CloseSnapshotForShutdown()
        {
            var window = _snapshotWindow;
            var overlay = _overlayBorder;
            _snapshotWindow = null;
            _overlayBorder = null;
            if (window != null)
            {
                window.Closed -= OnSnapshotWindowClosed;
            }

            try
            {
                window?.CloseForShutdown();
            }
            finally
            {
                overlay?.Dispose();
            }
        }

        internal void CloseForShutdown()
        {
            CloseSnapshotForShutdown();
            this.Close();
        }

        private static bool ApplicationIsInFocus()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;
            }

            var procId = Environment.ProcessId;
            _ = GetWindowThreadProcessId(activatedHandle, out int activeProcId);

            return activeProcId == procId;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                StrongReferenceMessenger.Default.UnregisterAll(this);
                this.Closed -= OnClosed;

                if (_vmPropertyChangedHandler != null && App.MainViewModel != null)
                {
                    App.MainViewModel.PropertyChanged -= _vmPropertyChangedHandler;
                }

                CloseSnapshotForShutdown();
                _cancellationToken.Cancel();
                _hotkeyEventThread?.Join();
                _cancellationToken.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        // Win32 interop
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    }
}
