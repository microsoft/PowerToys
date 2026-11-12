// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SoundManager.Helpers;
using WinUIEx;

namespace SoundManager
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private const int WindowWidth = 700;

        // The height the window starts with. It is worked out again as soon as the content is known.
        private const int InitialHeight = 600;

        // The window is never shorter than this.
        private const int MinimumHeight = 320;

        // The window is never taller than the screen it is on.
        private const int MaximumHeight = 1000;

        // The volume is shown from 0 to 100, Windows keeps it as a fraction.
        private const double VolumeScale = 100.0;

        // How much the volume may differ before the snapshot settles it.
        private const double VolumeTolerance = 0.5;

        // How much the balance may differ before the snapshot settles it.
        private const double BalanceTolerance = 0.005;

        // How far the balance slider has to move before the change is sent to Windows.
        private const double BalanceEpsilon = 0.01;

        // The window is only resized when it really changed.
        private const int ResizeTolerance = 2;

        private const string SpeakerGlyph = "\uE767";

        private const string MutedGlyph = "\uE74F";

        private const string MicrophoneGlyph = "\uE720";

        private const string CheckGlyph = "\uE73E";

        // How long the volume changes are collected before they are sent to the module interface.
        private static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(80);

        // How long a change is believed before the module interface is asked again.
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

        // How long the window waits before it looks for the module interface again.
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

        // How long a message about a change that was refused stays on the screen.
        private static readonly TimeSpan MessageDuration = TimeSpan.FromSeconds(4);

        // How often the meters are moved.
        private static readonly TimeSpan MeterInterval = TimeSpan.FromMilliseconds(16);

        // How long the change of the device of an application is believed before the snapshot settles it.
        private static readonly TimeSpan AppDeviceTimeout = TimeSpan.FromSeconds(6);

        // The messages the window shows while the module interface is not there.
        private static string ModuleMissingMessage => ResourceLoaderInstance.ResourceLoader.GetString("ModuleMissingMessage");

        private static string ModuleClosedMessage => ResourceLoaderInstance.ResourceLoader.GetString("ModuleClosedMessage");

        // A change that was sent to the module interface but was not confirmed by the next snapshot yet. The window keeps showing it.
        private sealed class PendingValue<T>
        {
            public PendingValue(T value, TimeSpan? timeout = null)
            {
                Value = value;
                RequestedAt = DateTime.UtcNow;
                Timeout = timeout ?? DefaultTimeout;
            }

            public T Value { get; }

            public DateTime RequestedAt { get; }

            public TimeSpan Timeout { get; }
        }

        // One device section of the window: the list of devices, and the slider and the mute button that belong to the device that is ticked.
        private sealed class DeviceSection
        {
            public DeviceSection(
                ObservableCollection<AudioDeviceInfo> items,
                Slider volumeSlider,
                TextBlock volumeLabel,
                FontIcon muteIcon,
                string unmutedGlyph)
            {
                Items = items;
                VolumeSlider = volumeSlider;
                VolumeLabel = volumeLabel;
                MuteIcon = muteIcon;
                UnmutedGlyph = unmutedGlyph;
            }

            public ObservableCollection<AudioDeviceInfo> Items { get; }

            public Slider VolumeSlider { get; }

            public TextBlock VolumeLabel { get; }

            public FontIcon MuteIcon { get; }

            public string UnmutedGlyph { get; }

            // The device that was picked and is on its way to Windows.
            public PendingValue<string> PendingDefault { get; set; }
        }

        private readonly MixerPipeClient _client = new();
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly string _pipeName;

        private readonly Dictionary<string, PendingValue<double>> _pendingAppVolumes = new();
        private readonly Dictionary<string, PendingValue<bool>> _pendingAppMutes = new();
        private readonly Dictionary<string, PendingValue<string>> _pendingAppDevices = new();
        private readonly Dictionary<string, PendingValue<(double Left, double Right)>> _pendingAppBalances = new();
        private readonly Dictionary<string, PendingValue<double>> _pendingDeviceVolumes = new();
        private readonly Dictionary<string, PendingValue<bool>> _pendingDeviceMutes = new();
        private readonly Dictionary<string, PendingValue<bool>> _pendingDeviceListens = new();
        private readonly Dictionary<string, PendingValue<(double Left, double Right)>> _pendingDeviceBalances = new();

        private readonly DeviceSection _outputSection;
        private readonly DeviceSection _inputSection;

        private List<AudioDeviceInfo> _routingDevices = new();
        private CancellationTokenSource _cancellation;
        private DispatcherQueueTimer _meterTimer;
        private DispatcherQueueTimer _writeTimer;
        private DispatcherQueueTimer _messageTimer;
        private bool _suppressEvents;
        private bool _resizing;
        private bool _hasConnected;
        private bool _disposed;
        private bool _writing;
        private string _lastShape = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<AppViewModel> Apps { get; } = new();

        public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();

        public ObservableCollection<AudioDeviceInfo> InputDevices { get; } = new();

        public MainWindow(string pipeName)
        {
            InitializeComponent();

            _pipeName = pipeName;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _outputSection = new DeviceSection(OutputDevices, OutputVolumeSlider, OutputVolumeLabel, OutputMuteIcon, SpeakerGlyph);
            _inputSection = new DeviceSection(InputDevices, InputVolumeSlider, InputVolumeLabel, InputMuteIcon, MicrophoneGlyph);

            // The name of the window comes from the resources, so that it is in the language of Windows.
            var title = ResourceLoaderInstance.ResourceLoader.GetString("AppTitle");
            if (string.IsNullOrEmpty(title))
            {
                // The resource map could not be read, so the plain name of the module is used.
                title = "Sound Manager";
            }

            Title = title;
            titleBar.Title = title;

            // The window grows with the content, so there is nothing to gain from maximizing it.
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
            }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(titleBar);
            TitleBarHelper.SetPreferredTheme(this);
            AppWindow.SetIcon("Assets/SoundManager/SoundManager.ico");

            AppWindow.Resize(new Windows.Graphics.SizeInt32(WindowWidth, InitialHeight));

            _writeTimer = DispatcherQueue.CreateTimer();
            _writeTimer.Interval = WriteDelay;
            _writeTimer.Tick += WriteTimer_Tick;

            _meterTimer = DispatcherQueue.CreateTimer();
            _meterTimer.Interval = MeterInterval;
            _meterTimer.Tick += MeterTimer_Tick;
            _meterTimer.Start();

            _messageTimer = DispatcherQueue.CreateTimer();
            _messageTimer.Interval = MessageDuration;
            _messageTimer.Tick += MessageTimer_Tick;

            RootGrid.Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // The timers hold the window up, so they are stopped first.
            _writeTimer.Stop();
            _meterTimer.Stop();
            _messageTimer.Stop();

            _cancellation?.Cancel();
            _client.SnapshotReceived -= Client_SnapshotReceived;
            _client.PeaksReceived -= Client_PeaksReceived;
            _client.Dispose();
            _cancellation?.Dispose();
            GC.SuppressFinalize(this);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _cancellation = new CancellationTokenSource();
            _client.SnapshotReceived += Client_SnapshotReceived;
            _client.PeaksReceived += Client_PeaksReceived;

            await RunAsync();
        }

        // Keeps talking to the module interface. When it is not there the window waits for it, and carries on where it left off when it comes back.
        private async Task RunAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    if (!await _client.ConnectAsync(_pipeName, _cancellation.Token))
                    {
                        SetStatus(_hasConnected ? ModuleClosedMessage : ModuleMissingMessage);
                        await Task.Delay(ReconnectDelay, _cancellation.Token);
                        continue;
                    }

                    _hasConnected = true;

                    SetStatus(string.Empty);

                    // Reading from the pipe blocks, so it happens on a background thread.
                    await Task.Run(() => _client.ListenAsync(_cancellation.Token));

                    if (_cancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    SetStatus(ModuleClosedMessage);
                    await Task.Delay(ReconnectDelay, _cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // The window keeps looking for the module interface, because it may well come back.
                    Logger.LogError($"Sound Manager lost the connection to the module interface: {ex.Message}");
                    SetStatus(ModuleClosedMessage);
                    await Task.Delay(ReconnectDelay, _cancellation.Token);
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Dispose();
        }

        private void Client_SnapshotReceived(object sender, MixerSnapshot snapshot)
        {
            _dispatcherQueue.TryEnqueue(() => ApplySnapshot(snapshot));
        }

        private void Client_PeaksReceived(object sender, MixerPeaks peaks)
        {
            _dispatcherQueue.TryEnqueue(() => ApplyPeaks(peaks));
        }

        private void ApplySnapshot(MixerSnapshot snapshot)
        {
            _suppressEvents = true;
            try
            {
                ApplyDeviceSection(_outputSection, snapshot.Outputs, snapshot.TestingDeviceId);
                ApplyDeviceSection(_inputSection, snapshot.Inputs, snapshot.TestingDeviceId);
                ApplyApps(snapshot);
            }
            finally
            {
                _suppressEvents = false;
            }

            ResizeWindowToContent();
        }

        // Hands the levels the module interface measured to the applications they belong to.
        private void ApplyPeaks(MixerPeaks peaks)
        {
            foreach (var peak in peaks.Peaks)
            {
                var app = FindApp(peak.SessionId);
                if (app != null)
                {
                    app.PeakLevel = peak.Peak;
                }
            }
        }

        // The devices an application can be routed to. An application that is not pinned to a device follows the system default.
        private static List<AudioDeviceInfo> BuildAppDevices(List<AudioDeviceInfo> outputs)
        {
            var devices = new List<AudioDeviceInfo>(outputs.Count + 1)
            {
                new AudioDeviceInfo { Id = string.Empty, Name = ResourceLoaderInstance.ResourceLoader.GetString("SystemDefaultDevice") },
            };

            devices.AddRange(outputs);
            return devices;
        }

        // The items are only rebuilt when the list of devices really changed, otherwise the selection would be lost on every snapshot.
        private void ApplyDeviceSection(DeviceSection section, List<AudioDeviceInfo> devices, string testingDeviceId)
        {
            var selectedId = SelectedDeviceId(devices, section.PendingDefault);

            if (SameDevices(section.Items, devices))
            {
                UpdateDevices(section, devices, selectedId, testingDeviceId);
            }
            else
            {
                RebuildDevices(section, devices, selectedId, testingDeviceId);
            }

            foreach (var item in section.Items)
            {
                item.Muted = Pending(item.Id, item.Muted, _pendingDeviceMutes);
                item.Listening = Pending(item.Id, item.Listening, _pendingDeviceListens);

                var balance = PendingBalance(item.Id, item.BalanceLeft, item.BalanceRight, _pendingDeviceBalances);
                item.BalanceLeft = balance.Left;
                item.BalanceRight = balance.Right;
            }

            ShowSelectedDevice(section);
        }

        // A device that is still there keeps what was done to it.
        private static void RebuildDevices(DeviceSection section, List<AudioDeviceInfo> devices, string selectedId, string testingDeviceId)
        {
            var before = new Dictionary<string, AudioDeviceInfo>();
            foreach (var item in section.Items)
            {
                before[item.Id] = item;
            }

            section.Items.Clear();
            foreach (var device in devices)
            {
                var item = new AudioDeviceInfo
                {
                    Id = device.Id,
                    Name = device.Name,
                    IsDefault = device.IsDefault,
                    IsDefaultCommunications = device.IsDefaultCommunications,
                    Volume = device.Volume,
                    Muted = device.Muted,
                    Listening = device.Listening,
                    BalanceLeft = device.BalanceLeft,
                    BalanceRight = device.BalanceRight,
                    IsSelected = device.Id == selectedId,
                    IsTesting = device.Id == testingDeviceId,
                };

                AudioDeviceInfo previous;
                if (before.TryGetValue(device.Id, out previous))
                {
                    item.IsExpanded = previous.IsExpanded;
                    item.IsMono = previous.IsMono;
                    item.MonoLeft = previous.MonoLeft;
                    item.MonoRight = previous.MonoRight;
                }

                section.Items.Add(item);
            }
        }

        private static void UpdateDevices(DeviceSection section, List<AudioDeviceInfo> devices, string selectedId, string testingDeviceId)
        {
            for (var i = 0; i < section.Items.Count; i++)
            {
                var item = section.Items[i];

                item.Volume = devices[i].Volume;
                item.Muted = devices[i].Muted;
                item.Listening = devices[i].Listening;
                item.BalanceLeft = devices[i].BalanceLeft;
                item.BalanceRight = devices[i].BalanceRight;
                item.IsDefault = devices[i].IsDefault;
                item.IsDefaultCommunications = devices[i].IsDefaultCommunications;
                item.IsSelected = item.Id == selectedId;
                item.IsTesting = item.Id == testingDeviceId;
            }
        }

        // The device Windows plays through, or the device Windows records from, unless a change is on its way to Windows.
        private static string SelectedDeviceId(List<AudioDeviceInfo> devices, PendingValue<string> pendingDefault)
        {
            if (pendingDefault != null && !IsStale(pendingDefault))
            {
                return pendingDefault.Value;
            }

            return devices.FirstOrDefault(device => device.IsDefault)?.Id
                   ?? devices.FirstOrDefault()?.Id
                   ?? string.Empty;
        }

        private void ShowSelectedDevice(DeviceSection section)
        {
            var selected = section.Items.FirstOrDefault(device => device.IsSelected);
            if (selected == null)
            {
                return;
            }

            var volume = PendingVolume(selected.Id, selected.Volume * VolumeScale, _pendingDeviceVolumes);
            section.VolumeSlider.Value = volume;
            section.VolumeLabel.Text = FormatVolume(volume);
            section.MuteIcon.Glyph = selected.Muted ? MutedGlyph : section.UnmutedGlyph;
        }

        // A change that is still on its way to the module interface wins over the snapshot, so that the control does not jump back to where it was.
        private static T Pending<T>(string key, T current, Dictionary<string, PendingValue<T>> pending)
        {
            if (!pending.TryGetValue(key, out var value))
            {
                return current;
            }

            if (EqualityComparer<T>.Default.Equals(value.Value, current) || IsStale(value))
            {
                pending.Remove(key);
                return current;
            }

            return value.Value;
        }

        // The same as Pending, but the module interface sends the volume back with the rounding of its own.
        private static double PendingVolume(string key, double current, Dictionary<string, PendingValue<double>> pending)
        {
            if (!pending.TryGetValue(key, out var value))
            {
                return current;
            }

            if (Math.Abs(value.Value - current) < VolumeTolerance || IsStale(value))
            {
                pending.Remove(key);
                return current;
            }

            return value.Value;
        }

        // The same as Pending, but the module interface sends the balance back with the rounding of its own.
        private static (double Left, double Right) PendingBalance(
            string key,
            double left,
            double right,
            Dictionary<string, PendingValue<(double Left, double Right)>> pending)
        {
            if (!pending.TryGetValue(key, out var value))
            {
                return (left, right);
            }

            if ((Math.Abs(value.Value.Left - left) < BalanceTolerance && Math.Abs(value.Value.Right - right) < BalanceTolerance) || IsStale(value))
            {
                pending.Remove(key);
                return (left, right);
            }

            return value.Value;
        }

        private static bool IsStale<T>(PendingValue<T> pending)
        {
            return DateTime.UtcNow - pending.RequestedAt > pending.Timeout;
        }

        private static bool SameDevices(IReadOnlyList<AudioDeviceInfo> current, List<AudioDeviceInfo> updated)
        {
            if (current.Count != updated.Count)
            {
                return false;
            }

            for (var i = 0; i < current.Count; i++)
            {
                if (current[i].Id != updated[i].Id)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyApps(MixerSnapshot snapshot)
        {
            _routingDevices = BuildAppDevices(snapshot.Outputs);

            var existing = Apps.ToDictionary(app => app.SessionId, app => app);

            foreach (var app in snapshot.Apps)
            {
                if (existing.TryGetValue(app.SessionId, out var current))
                {
                    current.Volume = PendingVolume(app.SessionId, app.Volume * VolumeScale, _pendingAppVolumes) / VolumeScale;
                    current.Muted = Pending(app.SessionId, app.Muted, _pendingAppMutes);
                    current.SelectedDeviceId = Pending(app.SessionId, app.OutputDeviceId, _pendingAppDevices);
                    current.SelectedDeviceName = DeviceName(current.SelectedDeviceId);
                    var balance = PendingBalance(app.SessionId, app.BalanceLeft, app.BalanceRight, _pendingAppBalances);
                    current.BalanceLeft = balance.Left;
                    current.BalanceRight = balance.Right;
                    current.AvailableDevices = _routingDevices;
                }
                else
                {
                    var entry = new AppViewModel
                    {
                        SessionId = app.SessionId,
                        ProcessPath = app.ProcessPath,
                        AppName = Path.GetFileNameWithoutExtension(app.DisplayName),
                        ProcessId = app.ProcessId,
                        IsActive = app.IsActive,
                        IsCapture = app.IsCapture,
                        Volume = app.Volume,
                        Muted = app.Muted,
                        AvailableDevices = _routingDevices,
                        SelectedDeviceId = app.OutputDeviceId,
                        SelectedDeviceName = DeviceName(app.OutputDeviceId),
                        BalanceLeft = app.BalanceLeft,
                        BalanceRight = app.BalanceRight,
                    };

                    Apps.Add(entry);
                    _ = LoadIconAsync(entry);
                }
            }

            // Drop the applications that no longer have a session.
            var live = snapshot.Apps.Select(app => app.SessionId).ToHashSet();
            for (var i = Apps.Count - 1; i >= 0; i--)
            {
                if (!live.Contains(Apps[i].SessionId))
                {
                    _pendingAppVolumes.Remove(Apps[i].SessionId);
                    _pendingAppMutes.Remove(Apps[i].SessionId);
                    _pendingAppDevices.Remove(Apps[i].SessionId);
                    _pendingAppBalances.Remove(Apps[i].SessionId);
                    Apps.RemoveAt(i);
                }
            }

            SortApps();

            EmptyStateText.Visibility = Apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // The sounds of Windows come first, the rest is sorted by name.
        private void SortApps()
        {
            var wanted = Apps
                .OrderBy(app => app.ProcessId == 0 ? 0 : 1)
                .ThenBy(app => app.AppName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(app => app.SessionId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < wanted.Count; i++)
            {
                var current = Apps.IndexOf(wanted[i]);
                if (current != i)
                {
                    Apps.Move(current, i);
                }
            }
        }

        private AppViewModel FindApp(string sessionId)
        {
            return Apps.FirstOrDefault(app => app.SessionId == sessionId);
        }

        private string DeviceName(string deviceId)
        {
            var systemDefault = ResourceLoaderInstance.ResourceLoader.GetString("SystemDefaultDevice");
            if (string.IsNullOrEmpty(deviceId))
            {
                return systemDefault;
            }

            return _routingDevices.FirstOrDefault(device => device.Id == deviceId)?.Name ?? systemDefault;
        }

        // The percentage is put together with the format of the language of Windows.
        private static string FormatVolume(double volume)
        {
            var format = ResourceLoaderInstance.ResourceLoader.GetString("VolumeFormat");
            return string.Format(CultureInfo.CurrentCulture, format, Math.Round(volume));
        }

        private async Task LoadIconAsync(AppViewModel app)
        {
            // The icon is read on the thread that owns the window.
            var icon = await IconLoader.LoadAsync(app.ProcessPath);

            if (icon == null && app.ProcessId == 0)
            {
                // The sounds of Windows have no program behind them, so they get the Windows logo.
                icon = await AppIconLoader.LoadWindowsLogoAsync();
            }

            app.Icon = icon ?? AppIconLoader.ManagerIcon;
        }

        private void ResizeWindowToContent()
        {
            if (_resizing)
            {
                return;
            }

            // The content is only measured again when the number of rows or the message at the bottom changed, so the list does not jump around.
            var opened = Apps.Count(app => app.IsExpanded) + OutputDevices.Count(device => device.IsExpanded);
            var shape = $"{Apps.Count}|{OutputDevices.Count}|{InputDevices.Count}|{StatusText.Visibility}|{opened}";
            if (shape == _lastShape)
            {
                return;
            }

            _lastShape = shape;
            _resizing = true;
            try
            {
                ContentStack.Measure(new Windows.Foundation.Size(ContentStack.ActualWidth, double.PositiveInfinity));
                var content = ContentStack.DesiredSize.Height;

                var status = 0.0;
                if (StatusText.Visibility != Visibility.Collapsed)
                {
                    status = StatusText.ActualHeight + StatusText.Margin.Bottom;
                }

                var wanted = titleBar.ActualHeight + content + status;

                // The frame is worked out from the size the window has right now, which keeps the calculation right on every screen.
                var client = RootGrid.ActualHeight;
                var height = (int)Math.Ceiling(AppWindow.Size.Height + (wanted - client));
                height = Math.Clamp(height, MinimumHeight, MaximumHeight);

                var size = AppWindow.Size;
                if (Math.Abs(size.Height - height) < ResizeTolerance)
                {
                    return;
                }

                AppWindow.Resize(new Windows.Graphics.SizeInt32(WindowWidth, height));
            }
            finally
            {
                _resizing = false;
            }
        }

        private void SetStatus(string text)
        {
            _statusMessage = text ?? string.Empty;
            StatusText.Text = _statusMessage;
            StatusText.Visibility = string.IsNullOrEmpty(_statusMessage) ? Visibility.Collapsed : Visibility.Visible;
        }

        // The message is taken away again when the timer runs out.
        private void ShowMessage(string text)
        {
            _messageTimer.Stop();
            StatusText.Text = text;
            StatusText.Visibility = Visibility.Visible;
            _messageTimer.Start();
        }

        private void MessageTimer_Tick(object sender, object e)
        {
            _messageTimer.Stop();
            SetStatus(_statusMessage);
        }

        private void OutputVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            DeviceVolumeChanged(_outputSection, e);
        }

        private void InputVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            DeviceVolumeChanged(_inputSection, e);
        }

        private void DeviceVolumeChanged(DeviceSection section, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressEvents || section.Items.FirstOrDefault(device => device.IsSelected) is not AudioDeviceInfo device)
            {
                return;
            }

            section.VolumeLabel.Text = FormatVolume(e.NewValue);
            _pendingDeviceVolumes[device.Id] = new PendingValue<double>(e.NewValue);
            RestartWriteTimer();
        }

        private async void OutputDeviceRadio_Click(object sender, RoutedEventArgs e)
        {
            await DeviceRadioClicked(sender, _outputSection);
        }

        private async void InputDeviceRadio_Click(object sender, RoutedEventArgs e)
        {
            await DeviceRadioClicked(sender, _inputSection);
        }

        private async Task DeviceRadioClicked(object sender, DeviceSection section)
        {
            if (sender is not RadioButton radio || radio.Tag is not AudioDeviceInfo device || _suppressEvents)
            {
                return;
            }

            if (device.IsDefault)
            {
                return;
            }

            SelectDefaultDevice(section, device);

            section.PendingDefault = new PendingValue<string>(device.Id);

            var ok = await _client.SetDefaultDeviceAsync(device.Id, false);
            if (!ok)
            {
                section.PendingDefault = null;
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        // Picking a device is not a volume change, so the volume is not written back.
        private void SelectDefaultDevice(DeviceSection section, AudioDeviceInfo device)
        {
            foreach (var item in section.Items)
            {
                item.IsSelected = item.Id == device.Id;
            }

            _suppressEvents = true;
            try
            {
                ShowSelectedDevice(section);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        // Makes a device the default one of Windows for calls and chats.
        private async void CallsDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            if (device.IsDefaultCommunications)
            {
                return;
            }

            var ok = await _client.SetDefaultDeviceAsync(device.Id, true);
            if (!ok)
            {
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        // The old Sound dialog calls this "Listen to this device".
        private async void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            var listening = !device.Listening;
            device.Listening = listening;
            _pendingDeviceListens[device.Id] = new PendingValue<bool>(listening);

            var ok = await _client.SetDeviceListenAsync(device.Id, listening);
            if (!ok)
            {
                _pendingDeviceListens.Remove(device.Id);
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        // Only one device is open at a time, so that the window does not grow out of shape.
        private void DeviceExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            var expanded = !device.IsExpanded;

            foreach (var other in OutputDevices)
            {
                if (other != device)
                {
                    other.IsExpanded = false;
                }
            }

            device.IsExpanded = expanded;

            ResizeWindowToContent();
        }

        // The slider that moves the sound of a device between the two ears. It runs from 0, all in the left ear, to 100, all in the right one.
        private void DeviceBalanceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressEvents || sender is not Slider slider || slider.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            var position = (e.NewValue * 2.0) - 100.0;

            // The model holds the value that is on the slider already, so there is nothing to send.
            if (Math.Abs(device.BalancePosition - position) < BalanceEpsilon)
            {
                return;
            }

            SetDeviceBalance(device, position);
        }

        private void DeviceMonoToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || sender is not ToggleSwitch toggle || toggle.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            if (device.IsMono == toggle.IsOn)
            {
                return;
            }

            SetDeviceMono(device, toggle.IsOn);
        }

        // The reset button only touches the balance and the mono switch.
        private async void DeviceResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            var balance = device.ResetBalance();

            _pendingDeviceBalances[device.Id] = new PendingValue<(double Left, double Right)>(balance);

            var ok = await _client.SetDeviceBalanceAsync(device.Id, balance.Left, balance.Right);
            if (!ok)
            {
                _pendingDeviceBalances.Remove(device.Id);
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        private async void DeviceTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AudioDeviceInfo device)
            {
                return;
            }

            // A test sound that is already playing cannot be played again.
            if (!device.CanTest)
            {
                return;
            }

            // The button shows that the sound is going before the module interface has answered.
            device.IsTesting = true;

            var ok = await _client.TestDeviceAsync(device.Id);
            if (!ok)
            {
                device.IsTesting = false;
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("TestSoundFailed"));
                return;
            }

            var message = string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("PlayingTestSound"), device.Name);
            ShowMessage(message);
        }

        private void AppVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressEvents || sender is not Slider slider || slider.Tag is not AppViewModel app)
            {
                return;
            }

            // The value is shown right away, the module interface follows a moment later.
            app.Volume = e.NewValue / VolumeScale;
            _pendingAppVolumes[app.SessionId] = new PendingValue<double>(e.NewValue);
            RestartWriteTimer();
        }

        private async void OutputMuteButton_Click(object sender, RoutedEventArgs e)
        {
            await MuteDeviceAsync(_outputSection);
        }

        private async void InputMuteButton_Click(object sender, RoutedEventArgs e)
        {
            await MuteDeviceAsync(_inputSection);
        }

        private async Task MuteDeviceAsync(DeviceSection section)
        {
            if (section.Items.FirstOrDefault(device => device.IsSelected) is not AudioDeviceInfo device)
            {
                return;
            }

            await SetDeviceMuteAsync(device, !device.Muted, section.MuteIcon, section.UnmutedGlyph);
        }

        private async void MuteToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AppViewModel app)
            {
                return;
            }

            await SetAppMuteAsync(app, !app.Muted);
        }

        // The name is shortened to fit the row, the full name is in the tooltip.
        private void AppDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AppViewModel app)
            {
                return;
            }

            var flyout = new MenuFlyout();
            foreach (var device in app.AvailableDevices)
            {
                var entry = new MenuFlyoutItem { Text = device.Name };
                if (device.Id == app.SelectedDeviceId)
                {
                    entry.Icon = new FontIcon { Glyph = CheckGlyph };
                }

                var deviceId = device.Id;
                entry.Click += async (menuSender, menuArgs) => await SetDeviceAsync(app, deviceId);
                flyout.Items.Add(entry);
            }

            button.Flyout = flyout;
            flyout.ShowAt(button);
        }

        private async Task ResetApplicationAsync(AppViewModel app)
        {
            // Windows refuses a request that changes nothing.
            var wasPinned = !string.IsNullOrEmpty(app.SelectedDeviceId);

            app.Volume = 1.0;
            app.Muted = false;
            app.ResetBalance();
            app.SelectedDeviceId = string.Empty;
            app.SelectedDeviceName = DeviceName(string.Empty);

            _pendingAppVolumes[app.SessionId] = new PendingValue<double>(VolumeScale);
            _pendingAppMutes[app.SessionId] = new PendingValue<bool>(false);
            _pendingAppBalances[app.SessionId] = new PendingValue<(double Left, double Right)>((1.0, 1.0));
            _pendingAppDevices[app.SessionId] = new PendingValue<string>(string.Empty, AppDeviceTimeout);

            var volumeOk = await _client.SetAppVolumeAsync(app.ProcessId, app.IsCapture, 1.0);
            var muteOk = await _client.SetAppMuteAsync(app.ProcessId, app.IsCapture, false);
            var balanceOk = await _client.SetAppBalanceAsync(app.ProcessId, app.IsCapture, 1.0, 1.0);

            var deviceOk = true;
            if (wasPinned)
            {
                deviceOk = await _client.SetAppDeviceAsync(app.ProcessId, app.IsCapture, string.Empty);
            }

            if (!volumeOk || !muteOk || !balanceOk || !deviceOk)
            {
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        // Only one row is open at a time, so that the window does not grow out of shape.
        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AppViewModel app)
            {
                return;
            }

            var expanded = !app.IsExpanded;

            foreach (var other in Apps)
            {
                if (other != app)
                {
                    other.IsExpanded = false;
                }
            }

            app.IsExpanded = expanded;

            ResizeWindowToContent();
        }

        // The slider that moves the sound of a program between the two ears. It runs from 0, all in the left ear, to 100, all in the right one.
        private void BalanceSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressEvents || sender is not Slider slider || slider.Tag is not AppViewModel app)
            {
                return;
            }

            var position = (e.NewValue * 2.0) - 100.0;

            // The model holds the value that is on the slider already, so there is nothing to send.
            if (Math.Abs(app.BalancePosition - position) < BalanceEpsilon)
            {
                return;
            }

            SetAppBalance(app, position);
        }

        private void MonoToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || sender is not ToggleSwitch toggle || toggle.Tag is not AppViewModel app)
            {
                return;
            }

            if (app.IsMono == toggle.IsOn)
            {
                return;
            }

            SetAppMono(app, toggle.IsOn);
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not AppViewModel app)
            {
                return;
            }

            await ResetApplicationAsync(app);
        }

        private void SetAppBalance(AppViewModel app, double position)
        {
            var balance = app.MoveBalance(position);

            _pendingAppBalances[app.SessionId] = new PendingValue<(double Left, double Right)>(balance);
            RestartWriteTimer();
        }

        private void SetAppMono(AppViewModel app, bool mono)
        {
            var balance = app.ApplyMono(mono);

            _pendingAppBalances[app.SessionId] = new PendingValue<(double Left, double Right)>(balance);
            RestartWriteTimer();
        }

        private void SetDeviceBalance(AudioDeviceInfo device, double position)
        {
            var balance = device.MoveBalance(position);

            _pendingDeviceBalances[device.Id] = new PendingValue<(double Left, double Right)>(balance);
            RestartWriteTimer();
        }

        private void SetDeviceMono(AudioDeviceInfo device, bool mono)
        {
            var balance = device.ApplyMono(mono);

            _pendingDeviceBalances[device.Id] = new PendingValue<(double Left, double Right)>(balance);
            RestartWriteTimer();
        }

        private async Task SetAppMuteAsync(AppViewModel app, bool muted)
        {
            app.Muted = muted;
            _pendingAppMutes[app.SessionId] = new PendingValue<bool>(muted);

            var ok = await _client.SetAppMuteAsync(app.ProcessId, app.IsCapture, muted);
            if (!ok)
            {
                _pendingAppMutes.Remove(app.SessionId);
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        private async Task SetDeviceMuteAsync(AudioDeviceInfo device, bool muted, FontIcon icon, string unmutedGlyph)
        {
            device.Muted = muted;
            _pendingDeviceMutes[device.Id] = new PendingValue<bool>(muted);
            icon.Glyph = muted ? MutedGlyph : unmutedGlyph;

            var ok = await _client.SetDeviceMuteAsync(device.Id, muted);
            if (!ok)
            {
                _pendingDeviceMutes.Remove(device.Id);
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        private async Task SetDeviceAsync(AppViewModel app, string deviceId)
        {
            app.SelectedDeviceId = deviceId;
            app.SelectedDeviceName = DeviceName(deviceId);
            _pendingAppDevices[app.SessionId] = new PendingValue<string>(deviceId, AppDeviceTimeout);

            var ok = await _client.SetAppDeviceAsync(app.ProcessId, app.IsCapture, deviceId);
            if (!ok)
            {
                _pendingAppDevices.Remove(app.SessionId);
                ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
            }
        }

        private void RestartWriteTimer()
        {
            _writeTimer.Stop();
            _writeTimer.Start();
        }

        private async void WriteTimer_Tick(object sender, object e)
        {
            // A tick that comes in while the last one is still sending does not start a second round.
            if (_writing)
            {
                return;
            }

            _writing = true;
            _writeTimer.Stop();

            try
            {
                await FlushPendingAsync(_pendingDeviceVolumes, (id, volume) => _client.SetDeviceVolumeAsync(id, volume / VolumeScale));
                await FlushPendingAsync(_pendingAppVolumes, (id, volume) => SendAppVolumeAsync(id, volume / VolumeScale));
                await FlushPendingAsync(_pendingAppBalances, (id, balance) => SendAppBalanceAsync(id, balance.Left, balance.Right));
                await FlushPendingAsync(_pendingDeviceBalances, (id, balance) => _client.SetDeviceBalanceAsync(id, balance.Left, balance.Right));
            }
            finally
            {
                _writing = false;
            }
        }

        // Sends everything that is waiting and drops the changes Windows refused.
        private async Task FlushPendingAsync<T>(Dictionary<string, PendingValue<T>> pending, Func<string, T, Task<bool>> send)
        {
            foreach (var entry in pending.ToArray())
            {
                var ok = await send(entry.Key, entry.Value.Value);
                if (!ok)
                {
                    pending.Remove(entry.Key);
                    ShowMessage(ResourceLoaderInstance.ResourceLoader.GetString("ChangeNotApplied"));
                }
            }
        }

        private async Task<bool> SendAppVolumeAsync(string sessionId, double volume)
        {
            var app = FindApp(sessionId);
            if (app == null)
            {
                // The application is gone, so there is nothing to send.
                return true;
            }

            return await _client.SetAppVolumeAsync(app.ProcessId, app.IsCapture, volume);
        }

        private async Task<bool> SendAppBalanceAsync(string sessionId, double left, double right)
        {
            var app = FindApp(sessionId);
            if (app == null)
            {
                // The application is gone, so there is nothing to send.
                return true;
            }

            return await _client.SetAppBalanceAsync(app.ProcessId, app.IsCapture, left, right);
        }

        private void MeterTimer_Tick(object sender, object e)
        {
            foreach (var app in Apps)
            {
                app.AdvanceMeter();
            }
        }
    }
}
