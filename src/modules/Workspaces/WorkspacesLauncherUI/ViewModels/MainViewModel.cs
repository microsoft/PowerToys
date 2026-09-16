// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using ManagedCommon;
using WorkspacesCsharpLibrary;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly Action<string> _sendMessage;
        private readonly Func<SignatureWarningRequest, SignatureWarningWindow> _createWarning;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly HashSet<Guid> _seenRequests = new HashSet<Guid>();
        private Window _snapshotWindow;
        private ActiveWarning _warning;
        private bool _stopped;

        public MainViewModel()
            : this(App.SendIPCMessage, request => new SignatureWarningWindow(request))
        {
            _ = new PwaHelper();
        }

        internal MainViewModel(Action<string> sendMessage, Func<SignatureWarningRequest, SignatureWarningWindow> createWarning)
        {
            _sendMessage = sendMessage;
            _createWarning = createWarning;
            App.IPCMessageReceivedCallback = ReceiveMessage;
        }

        public ObservableCollection<AppLaunching> AppsListed { get; set; } = new ObservableCollection<AppLaunching>();

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private void ReceiveMessage(string message)
        {
            if (!_dispatcher.HasShutdownStarted)
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => HandleMessage(message)));
            }
        }

        private void HandleMessage(string message)
        {
            if (_stopped)
            {
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(message);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("Expected a Workspaces launcher message object.");
                }

                if (!root.TryGetProperty("type", out _))
                {
                    HandleAppLaunchingState(new AppLaunchData().Deserialize(message));
                    return;
                }

                var type = RequiredString(root, "type");
                if (type != "elevation-warning" && type != "dismiss-warning")
                {
                    throw new JsonException("Unexpected Workspaces launcher message type.");
                }

                var requestId = RequiredString(root, "requestId");
                if (!Guid.TryParse(requestId, out var id) || id == Guid.Empty)
                {
                    throw new JsonException("Invalid elevation warning request ID.");
                }

                if (type == "dismiss-warning")
                {
                    _seenRequests.Add(id);
                    if (_warning?.Id == id)
                    {
                        CompleteWarning(_warning, null);
                    }

                    return;
                }

                var request = new SignatureWarningRequest
                {
                    RequestId = requestId,
                    AppName = RequiredString(root, "appName"),
                    Path = RequiredString(root, "path"),
                    Arguments = RequiredString(root, "arguments"),
                    Reason = RequiredString(root, "reason"),
                    Status = RequiredString(root, "status"),
                };
                if (!_seenRequests.Add(id))
                {
                    Logger.LogWarning("Ignoring a replayed Workspaces elevation warning.");
                    return;
                }

                if (_warning != null)
                {
                    Logger.LogWarning("Skipping an overlapping Workspaces elevation warning.");
                    SendResponse(requestId, false);
                    return;
                }

                var warning = new ActiveWarning(id, request);
                _warning = warning;

                // The pipe callback must return before ShowDialog enters a nested dispatcher.
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ShowWarning(warning)));
            }
            catch (Exception exception)
            {
                Logger.LogError("Unable to handle a Workspaces launcher message", exception);
                CompleteWarning(_warning, false);
            }
        }

        private static string RequiredString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"Missing or incorrectly typed Workspaces launcher field: {name}.");
            }

            return value.GetString();
        }

        private void ShowWarning(ActiveWarning warning)
        {
            if (!CanShowWarning(warning))
            {
                if (ReferenceEquals(_warning, warning))
                {
                    Logger.LogWarning("The Workspaces elevation warning owner is unavailable.");
                }

                CompleteWarning(warning, false);
                return;
            }

            var run = false;
            EventHandler rendered = (_, _) => OnWarningRendered(warning);
            try
            {
                warning.Window = _createWarning(warning.Request);
                warning.Window.Owner = _snapshotWindow;
                warning.Window.ContentRendered += rendered;
                if (CanShowWarning(warning))
                {
                    run = warning.Window.ShowDialog() == true;
                }
                else
                {
                    warning.Window.DismissWithoutResponse();
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("Unable to display the Workspaces elevation warning", exception);
            }
            finally
            {
                if (warning.Window != null)
                {
                    warning.Window.ContentRendered -= rendered;
                }

                CompleteWarning(warning, run);
                warning.Window = null;
            }
        }

        private void OnWarningRendered(ActiveWarning warning)
        {
            if (warning.Rendered || !CanShowWarning(warning) || warning.Window?.IsVisible != true)
            {
                return;
            }

            warning.Rendered = true;
            if (TrySendMessage(JsonSerializer.Serialize(new { type = "warning-shown", requestId = warning.Request.RequestId })) &&
                CanShowWarning(warning) && warning.Window.IsVisible)
            {
                warning.Shown = true;
                warning.Window.EnableRunChoice();
            }
            else
            {
                CompleteWarning(warning, false);
            }
        }

        private bool CanShowWarning(ActiveWarning warning)
        {
            return !_stopped && ReferenceEquals(_warning, warning) && _snapshotWindow?.IsVisible == true;
        }

        private void CompleteWarning(ActiveWarning warning, bool? run)
        {
            if (warning == null || !ReferenceEquals(_warning, warning))
            {
                return;
            }

            var allowRun = run == true && warning.Shown && CanShowWarning(warning);

            // Clear before closing or sending: either can allow the next request to be dispatched.
            _warning = null;
            try
            {
                warning.Window?.DismissWithoutResponse();
            }
            catch (Exception exception)
            {
                Logger.LogError("Unable to close the Workspaces elevation warning", exception);
                allowRun = false;
            }

            if (run.HasValue)
            {
                SendResponse(warning.Request.RequestId, allowRun && !_stopped && _snapshotWindow?.IsVisible == true);
            }
        }

        private void SendResponse(string requestId, bool run)
        {
            TrySendMessage(JsonSerializer.Serialize(new { type = "elevation-response", requestId, choice = run ? "run" : "skip" }));
        }

        private bool TrySendMessage(string message)
        {
            try
            {
                _sendMessage(message);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError("Unable to queue a Workspaces launcher UI message", exception);
                if (!_stopped)
                {
                    Stop(false);
                    _snapshotWindow?.Close();
                }

                return false;
            }
        }

        private void HandleAppLaunchingState(AppLaunchData.AppLaunchDataWrapper appLaunchData)
        {
            var launchInfos = appLaunchData.AppLaunchInfos.AppLaunchInfoList ??
                throw new JsonException("Missing Workspaces application launch status.");
            List<AppLaunching> appLaunchingList = new List<AppLaunching>();
            foreach (var app in launchInfos)
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
            Stop(false);
            GC.SuppressFinalize(this);
        }

        internal void SetSnapshotWindow(Window snapshotWindow)
        {
            _snapshotWindow = snapshotWindow;
        }

        internal void CancelLaunch()
        {
            Stop(true);
        }

        private void Stop(bool cancel)
        {
            _dispatcher.VerifyAccess();
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            if (App.IPCMessageReceivedCallback == ReceiveMessage)
            {
                App.IPCMessageReceivedCallback = null;
            }

            CompleteWarning(_warning, cancel ? null : false);
            if (cancel)
            {
                TrySendMessage("cancel");
            }
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

            internal SignatureWarningWindow Window { get; set; }

            internal bool Rendered { get; set; }

            internal bool Shown { get; set; }
        }
    }
}
