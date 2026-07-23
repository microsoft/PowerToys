// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
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
        private readonly CancellationTokenSource _cancellationToken = new();

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);

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
                ContentFrame.Navigate(typeof(Views.WorkspacesEditorPage), (vm, m.Project));
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
            OverlayBorder overlayBorder = null;
            StrongReferenceMessenger.Default.Register<ShowSnapshotWindowMessage>(this, (r, m) =>
            {
                // Show red border overlay around all displays
                var displays = OverlayBorder.GetAllMonitorBounds();
                overlayBorder = OverlayBorder.CreateForAllMonitors(displays);

                var snapshotWindow = new Views.SnapshotWindow();
                snapshotWindow.Closed += (s, args) =>
                {
                    overlayBorder?.Dispose();
                    overlayBorder = null;
                };
                snapshotWindow.Activate();
            });

            // Bind loading ring to ViewModel.IsLoading
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsLoading))
                {
                    LoadingRing.IsActive = vm.IsLoading;
                    LoadingRing.Visibility = vm.IsLoading
                        ? Microsoft.UI.Xaml.Visibility.Visible
                        : Microsoft.UI.Xaml.Visibility.Collapsed;
                }
            };

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
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToys.Interop.Constants.WorkspacesHotkeyEvent());
                while (true)
                {
                    if (WaitHandle.WaitAny(new WaitHandle[] { token.WaitHandle, eventHandle }) == 1)
                    {
                        App.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (ApplicationIsInFocus())
                            {
                                StrongReferenceMessenger.Default.Send(new CloseApplicationMessage());
                            }
                            else
                            {
                                WindowHelpers.BringToForeground(hwnd);
                            }
                        });
                    }
                    else
                    {
                        return;
                    }
                }
            }) { IsBackground = true }.Start();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _cancellationToken.Dispose();
            (Microsoft.UI.Xaml.Application.Current as IDisposable)?.Dispose();
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
            _cancellationToken?.Dispose();
            GC.SuppressFinalize(this);
        }

        // Win32 interop
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    }
}
