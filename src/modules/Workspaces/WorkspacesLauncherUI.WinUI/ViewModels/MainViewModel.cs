// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using WorkspacesCsharpLibrary;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Helpers;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan CancellationAcknowledgementTimeout = TimeSpan.FromSeconds(2);
        private readonly Action<string> _sendMessage;
        private readonly Func<SignatureWarningRequest, ISignatureWarningWindow> _createWarning;
        private readonly Action<Action, bool> _enqueue;
        private readonly Func<bool> _canDisplayWarning;
        private readonly Action _closeOwner;
        private readonly Func<string, string, string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> _loadIcon;
        private readonly Func<Action, IDisposable> _createCancellationTimer;
        private readonly HashSet<Guid> _seenRequests = new HashSet<Guid>();
        private ActiveWarning _warning;
        private IDisposable _cancellationTimer;
        private bool _stopped;
        private bool _isCancellationPending;

        public MainViewModel()
            : this(false, () => false, () => { })
        {
        }

        public MainViewModel(bool confirmationOnly, Func<bool> ownerVisible, Action closeOwner)
            : this(
                App.SendIPCMessage,
                request => new SignatureWarningWindow(request),
                (action, highPriority) => App.DispatcherQueue.TryEnqueue(highPriority ? DispatcherQueuePriority.High : DispatcherQueuePriority.Normal, () => action()),
                () => confirmationOnly || ownerVisible(),
                closeOwner,
                IconHelper.TryGetApplicationIcon)
        {
            if (!confirmationOnly)
            {
                _ = new PwaHelper();
            }
        }

        internal MainViewModel(
            Action<string> sendMessage,
            Func<SignatureWarningRequest, ISignatureWarningWindow> createWarning,
            Action<Action, bool> enqueue,
            Func<bool> canDisplayWarning,
            Action closeOwner,
            Func<string, string, string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> loadIcon = null,
            Func<Action, IDisposable> createCancellationTimer = null)
        {
            _sendMessage = sendMessage;
            _createWarning = createWarning;
            _enqueue = enqueue;
            _canDisplayWarning = canDisplayWarning;
            _closeOwner = closeOwner;
            _loadIcon = loadIcon;
            _createCancellationTimer = createCancellationTimer ??
                (callback => new Timer(_ => callback(), null, CancellationAcknowledgementTimeout, Timeout.InfiniteTimeSpan));
            App.IPCMessageReceivedCallback = ReceiveMessage;
        }

        public ObservableCollection<AppLaunching> AppsListed { get; } = new ObservableCollection<AppLaunching>();

        public bool IsCancellationPending => _isCancellationPending;

        public bool IsInteractionEnabled => !_stopped;

        private void ReceiveMessage(string message) => _enqueue(() => HandleMessage(message), true);

        private void HandleMessage(string message)
        {
            if (_stopped && !_isCancellationPending)
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

                if (root.TryGetProperty("type", out var messageType) &&
                    messageType.ValueKind == JsonValueKind.String &&
                    messageType.GetString() == "cancel-accepted")
                {
                    CompleteCancellation();
                    return;
                }

                if (_stopped)
                {
                    return;
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
                _enqueue(() => ShowWarning(warning), false);
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
                CompleteWarning(warning, false);
                return;
            }

            try
            {
                warning.Window = _createWarning(warning.Request);
                warning.Window.Rendered += (_, _) => OnWarningRendered(warning);
                warning.Window.ChoiceMade += (_, run) => CompleteWarning(warning, run);
                if (CanShowWarning(warning))
                {
                    warning.Window.Show();
                }
                else
                {
                    if (ReferenceEquals(_warning, warning))
                    {
                        CompleteWarning(warning, false);
                    }
                    else
                    {
                        warning.Window.DismissWithoutResponse();
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("Unable to display the Workspaces elevation warning", exception);
                CompleteWarning(warning, false);
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

        private bool CanShowWarning(ActiveWarning warning) => !_stopped && ReferenceEquals(_warning, warning) && _canDisplayWarning();

        private void CompleteWarning(ActiveWarning warning, bool? run)
        {
            if (warning == null || !ReferenceEquals(_warning, warning))
            {
                return;
            }

            var allowRun = run == true && warning.Shown && CanShowWarning(warning);

            // Clear before closing or sending: either can dispatch the next request.
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
                SendResponse(warning.Request.RequestId, allowRun && !_stopped && _canDisplayWarning());
            }
        }

        private void SendResponse(string requestId, bool run) =>
            TrySendMessage(JsonSerializer.Serialize(new { type = "elevation-response", requestId, choice = run ? "run" : "skip" }));

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
                    _closeOwner();
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
                    IconImage = _loadIcon?.Invoke(app.Application.ApplicationPath, app.Application.PackageFullName, app.Application.PwaAppId),
                    PackagedName = app.Application.PackageFullName,
                    Aumid = app.Application.AppUserModelId,
                    PwaAppId = app.Application.PwaAppId,
                    LaunchState = app.State,
                });
            }

            AppsListed.Clear();
            foreach (var app in appLaunchingList)
            {
                AppsListed.Add(app);
            }

            OnPropertyChanged(nameof(AppsListed));
        }

        public void Dispose()
        {
            Stop(false);
            ClearCancellation();
            GC.SuppressFinalize(this);
        }

        [RelayCommand]
        internal void CancelLaunch()
        {
            if (_stopped)
            {
                return;
            }

            // Keep IPC alive until the launcher has received cancellation or the wait expires.
            _isCancellationPending = true;
            Stop(true);
            OnPropertyChanged(nameof(IsCancellationPending));
            if (!_isCancellationPending)
            {
                return;
            }

            _cancellationTimer = _createCancellationTimer(() => _enqueue(OnCancellationTimeout, true));
            if (!TrySendMessage("cancel"))
            {
                CompleteCancellation();
            }
        }

        private void Stop(bool cancel)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            OnPropertyChanged(nameof(IsInteractionEnabled));
            if (!cancel)
            {
                DetachMessageCallback();
            }

            CompleteWarning(_warning, cancel ? null : false);
        }

        private void OnCancellationTimeout()
        {
            if (_isCancellationPending)
            {
                Logger.LogWarning("Timed out waiting for the Workspaces launcher to acknowledge cancellation.");
                CompleteCancellation();
            }
        }

        private void CompleteCancellation()
        {
            if (!_isCancellationPending)
            {
                return;
            }

            ClearCancellation();
            _closeOwner();
        }

        private void ClearCancellation()
        {
            var wasPending = _isCancellationPending;
            _isCancellationPending = false;
            _cancellationTimer?.Dispose();
            _cancellationTimer = null;
            DetachMessageCallback();
            if (wasPending)
            {
                OnPropertyChanged(nameof(IsCancellationPending));
            }
        }

        private void DetachMessageCallback()
        {
            if (App.IPCMessageReceivedCallback == ReceiveMessage)
            {
                App.IPCMessageReceivedCallback = null;
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

            internal ISignatureWarningWindow Window { get; set; }

            internal bool Rendered { get; set; }

            internal bool Shown { get; set; }
        }
    }
}
