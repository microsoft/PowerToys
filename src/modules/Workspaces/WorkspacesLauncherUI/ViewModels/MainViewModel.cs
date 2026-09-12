// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WorkspacesCsharpLibrary;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.IPC;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly LauncherConnection _connection;
        private readonly HashSet<Guid> _seenRequests = new HashSet<Guid>();
        private StatusWindow _snapshotWindow;
        private ActiveWarning _warning;
        private bool _stopped;

        internal MainViewModel(LauncherConnection connection)
        {
            _connection = connection;

            // Populate the shared PWA icon cache used by the launch-status list.
            _ = new PwaHelper();
        }

        public ObservableCollection<AppLaunching> AppsListed { get; set; } = new ObservableCollection<AppLaunching>();

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        internal void HandleMessage(LauncherMessage message)
        {
            if (_stopped || !_connection.IsOpen)
            {
                return;
            }

            switch (message.Type)
            {
                case "launch-status":
                    HandleAppLaunchingState(message.LaunchStatus);
                    break;
                case "elevation-warning":
                    if (!_seenRequests.Add(message.RequestId) || (_warning != null && !_warning.ResponseStarted))
                    {
                        throw new InvalidDataException("Replayed or overlapping elevation warning.");
                    }

                    if (!_snapshotWindow.IsVisible)
                    {
                        throw new InvalidOperationException("The elevation warning owner is unavailable.");
                    }

                    var warning = new ActiveWarning(message.RequestId, message.Warning);
                    _warning = warning;

                    // Never await ShowDialog from the pipe reader. Dismissal and shutdown must run
                    // inside its nested dispatcher, including before this scheduled dialog opens.
                    _snapshotWindow.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ShowWarning(warning)));
                    break;
                case "dismiss-warning":
                    _seenRequests.Add(message.RequestId);
                    if (_warning?.Id == message.RequestId)
                    {
                        SuppressWarning(_warning);
                    }

                    break;
                default:
                    throw new InvalidDataException("Unexpected launcher UI message.");
            }
        }

        private void ShowWarning(ActiveWarning warning)
        {
            if (!CanUseWarning(warning))
            {
                FinishWarning(warning);
                return;
            }

            var run = false;
            EventHandler rendered = (_, _) => OnWarningRendered(warning);
            EventHandler heartbeat = async (_, _) => await SendHeartbeatAsync(warning);
            try
            {
                warning.Window = new SignatureWarningWindow(warning.Request) { Owner = _snapshotWindow };
                warning.Timer = new DispatcherTimer(DispatcherPriority.Normal, _snapshotWindow.Dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(1),
                };
                warning.Timer.Tick += heartbeat;
                warning.Window.ContentRendered += rendered;
                run = warning.Window.ShowDialog() == true;
            }
            catch (Exception exception)
            {
                warning.Suppressed = true;
                _connection.Fail("Unable to display the Workspaces elevation warning", exception);
            }
            finally
            {
                if (warning.Timer != null)
                {
                    warning.Timer.Stop();
                    warning.Timer.Tick -= heartbeat;
                }

                if (warning.Window != null)
                {
                    warning.Window.ContentRendered -= rendered;
                    warning.Window = null;
                }

                _ = CompleteWarningAsync(warning, run);
            }
        }

        private void OnWarningRendered(ActiveWarning warning)
        {
            if (warning.Rendered || !CanUseWarning(warning))
            {
                return;
            }

            warning.Rendered = true;
            warning.ShownTask = AcknowledgeWarningAsync(warning);
        }

        private async Task<bool> AcknowledgeWarningAsync(ActiveWarning warning)
        {
            try
            {
                var sent = await _connection.SendWarningShownAsync(warning.Request.RequestId, warning.Cancellation.Token);
                if (!sent || !CanUseWarning(warning))
                {
                    return false;
                }

                warning.Acknowledged = true;
                if (warning.Window?.IsVisible == true)
                {
                    warning.Window.EnableRunChoice();
                    if (warning.Window.IsVisible)
                    {
                        warning.Timer.Start();
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                _connection.Fail("Unable to acknowledge the rendered Workspaces elevation warning", exception);
                return false;
            }
        }

        private async Task SendHeartbeatAsync(ActiveWarning warning)
        {
            if (!CanUseWarning(warning) || !warning.Acknowledged ||
                warning.Window?.IsVisible != true || warning.HeartbeatPending)
            {
                return;
            }

            warning.HeartbeatPending = true;
            try
            {
                await _connection.SendHeartbeatAsync(warning.Request.RequestId, warning.Cancellation.Token);
            }
            catch (Exception exception)
            {
                _connection.Fail("Unable to send the Workspaces warning UI heartbeat", exception);
            }
            finally
            {
                warning.HeartbeatPending = false;
            }
        }

        private async Task CompleteWarningAsync(ActiveWarning warning, bool run)
        {
            try
            {
                if (!CanUseWarning(warning))
                {
                    return;
                }

                if (warning.ShownTask == null)
                {
                    _connection.Fail("The elevation warning closed before rendering", new InvalidOperationException());
                    return;
                }

                if (await warning.ShownTask && CanUseWarning(warning))
                {
                    // A request is terminal before writing its sole response. The parent may send
                    // its next request before the await continuation gets a dispatcher turn.
                    warning.ResponseStarted = true;
                    await _connection.SendResponseAsync(warning.Request.RequestId, run, warning.Cancellation.Token);
                }
            }
            catch (Exception exception)
            {
                _connection.Fail("Unable to complete the Workspaces elevation warning", exception);
            }
            finally
            {
                FinishWarning(warning);
            }
        }

        private bool CanUseWarning(ActiveWarning warning)
        {
            return !_stopped && _connection.IsOpen && ReferenceEquals(_warning, warning) &&
                !warning.Suppressed && !warning.ResponseStarted;
        }

        private void SuppressWarning(ActiveWarning warning)
        {
            warning.Suppressed = true;
            if (ReferenceEquals(_warning, warning))
            {
                _warning = null;
            }

            warning.Timer?.Stop();
            warning.Cancellation.Cancel();
            warning.Window?.DismissWithoutResponse();
        }

        private void FinishWarning(ActiveWarning warning)
        {
            if (ReferenceEquals(_warning, warning))
            {
                _warning = null;
            }

            warning.Cancellation.Dispose();
        }

        private void HandleAppLaunchingState(AppLaunchData.AppLaunchDataWrapper appLaunchData)
        {
            List<AppLaunching> appLaunchingList = new List<AppLaunching>();
            foreach (var app in appLaunchData.AppLaunchInfos.AppLaunchInfoList)
            {
                appLaunchingList.Add(new AppLaunching()
                {
                    Name = app.Application.Application,
                    AppPath = app.Application.ApplicationPath,
                    PackagedName = app.Application.PackageFullName,
                    Aumid = app.Application.AppUserModelId,
                    PwaAppId = app.Application.PwaAppId,
                    LaunchState = app.State,
                });
            }

            AppsListed = new ObservableCollection<AppLaunching>(appLaunchingList);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(AppsListed)));
        }

        public void Dispose()
        {
            if (!_stopped)
            {
                _stopped = true;
                if (_warning != null)
                {
                    SuppressWarning(_warning);
                }
            }

            GC.SuppressFinalize(this);
        }

        internal void SetSnapshotWindow(StatusWindow snapshotWindow)
        {
            _snapshotWindow = snapshotWindow;
        }

        internal async Task CancelLaunchAsync()
        {
            Dispose();
            await _connection.SendCancelAsync();
        }

        private sealed class ActiveWarning
        {
            internal ActiveWarning(Guid id, SignatureWarningRequest request)
            {
                Id = id;
                Request = request;
            }

            internal Guid Id { get; }

            internal SignatureWarningRequest Request { get; }

            internal CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

            internal SignatureWarningWindow Window { get; set; }

            internal DispatcherTimer Timer { get; set; }

            internal Task<bool> ShownTask { get; set; }

            internal bool Rendered { get; set; }

            internal bool Acknowledged { get; set; }

            internal bool HeartbeatPending { get; set; }

            internal bool ResponseStarted { get; set; }

            internal bool Suppressed { get; set; }
        }
    }
}
