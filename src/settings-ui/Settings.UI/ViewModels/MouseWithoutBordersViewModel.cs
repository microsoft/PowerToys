// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using StreamJsonRpc;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class MouseWithoutBordersViewModel : Observable, IDisposable
    {
        // These should be in the same order as the ComboBoxItems in MouseWithoutBordersPage.xaml switch machine shortcut options
        private readonly int[] _switchBetweenMachineShortcutOptions =
        {
            112,
            49,
            0,
        };

        private readonly Lock _machineMatrixStringLock = new();

        private static readonly Dictionary<SocketStatus, Brush> StatusColors = new Dictionary<SocketStatus, Brush>()
{
    { SocketStatus.NA, new SolidColorBrush(ColorHelper.FromArgb(0, 0x71, 0x71, 0x71)) },
    { SocketStatus.Resolving, new SolidColorBrush(Colors.Yellow) },
    { SocketStatus.Connecting, new SolidColorBrush(Colors.Orange) },
    { SocketStatus.Handshaking, new SolidColorBrush(Colors.Blue) },
    { SocketStatus.Error, new SolidColorBrush(Colors.Red) },
    { SocketStatus.ForceClosed, new SolidColorBrush(Colors.Purple) },
    { SocketStatus.InvalidKey, new SolidColorBrush(Colors.Brown) },
    { SocketStatus.Timeout, new SolidColorBrush(Colors.Pink) },
    { SocketStatus.SendError, new SolidColorBrush(Colors.Maroon) },
    { SocketStatus.Connected, new SolidColorBrush(Colors.Green) },
};

        private bool _connectFieldsVisible;

        public bool IsElevated { get => GeneralSettingsConfig.IsElevated; }

        public bool CanUninstallService { get => GeneralSettingsConfig.IsElevated && !UseService; }

        public ButtonClickCommand AddFirewallRuleEventHandler => new ButtonClickCommand(AddFirewallRule);

        public ButtonClickCommand UninstallServiceEventHandler => new ButtonClickCommand(UninstallService);

        public bool ShowOriginalUI
        {
            get
            {
                if (_useOriginalUserInterfaceGpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                return Settings.Properties.ShowOriginalUI;
            }

            set
            {
                if (!_useOriginalUserInterfaceIsGPOConfigured && (Settings.Properties.ShowOriginalUI != value))
                {
                    Settings.Properties.ShowOriginalUI = value;
                    NotifyPropertyChanged(nameof(ShowOriginalUI));
                }
            }
        }

        public bool CardForOriginalUiSettingIsEnabled => _useOriginalUserInterfaceIsGPOConfigured == false;

        public bool ShowPolicyConfiguredInfoForOriginalUiSetting => IsEnabled && _useOriginalUserInterfaceIsGPOConfigured;

        public bool UseService
        {
            get
            {
                if (_allowServiceModeGpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                return Settings.Properties.UseService;
            }

            set
            {
                if (_allowServiceModeIsGPOConfigured)
                {
                    return;
                }

                var valueChanged = Settings.Properties.UseService != value;

                // Set the UI property itself instantly
                if (valueChanged)
                {
                    Settings.Properties.UseService = value;
                    OnPropertyChanged(nameof(UseService));
                    OnPropertyChanged(nameof(CanUninstallService));
                    OnPropertyChanged(nameof(ShowInfobarRunAsAdminText));

                    // Must block here until the process exits
                    Task.Run(async () =>
                    {
                        await SubmitShutdownRequestAsync();

                        _uiDispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                        {
                            Settings.Properties.UseService = value;
                            NotifyPropertyChanged(nameof(UseService));
                        });
                    });
                }
            }
        }

        public bool UseServiceSettingIsEnabled => _allowServiceModeIsGPOConfigured == false;

        public bool ConnectFieldsVisible
        {
            get => _connectFieldsVisible;

            set
            {
                if (_connectFieldsVisible != value)
                {
                    _connectFieldsVisible = value;
                    OnPropertyChanged(nameof(ConnectFieldsVisible));
                }
            }
        }

        private string _connectSecurityKey;

        public string ConnectSecurityKey
        {
            get => _connectSecurityKey;

            set
            {
                if (_connectSecurityKey != value)
                {
                    _connectSecurityKey = value;
                    OnPropertyChanged(nameof(ConnectSecurityKey));
                }
            }
        }

        private string _connectPCName;

        public string ConnectPCName
        {
            get => _connectPCName;

            set
            {
                if (_connectPCName != value)
                {
                    _connectPCName = value;
                    OnPropertyChanged(nameof(ConnectPCName));
                }
            }
        }

        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        // Configuration policy variables
        private GpoRuleConfigured _clipboardSharingEnabledGpoConfiguration;
        private bool _clipboardSharingEnabledIsGPOConfigured;
        private GpoRuleConfigured _fileTransferEnabledGpoConfiguration;
        private bool _fileTransferEnabledIsGPOConfigured;
        private GpoRuleConfigured _useOriginalUserInterfaceGpoConfiguration;
        private bool _useOriginalUserInterfaceIsGPOConfigured;
        private GpoRuleConfigured _disallowBlockingScreensaverGpoConfiguration;
        private bool _disallowBlockingScreensaverIsGPOConfigured;
        private GpoRuleConfigured _allowServiceModeGpoConfiguration;
        private bool _allowServiceModeIsGPOConfigured;
        private GpoRuleConfigured _sameSubnetOnlyGpoConfiguration;
        private bool _sameSubnetOnlyIsGPOConfigured;
        private GpoRuleConfigured _validateRemoteIpGpoConfiguration;
        private bool _validateRemoteIpIsGPOConfigured;
        private GpoRuleConfigured _disableUserDefinedIpMappingRulesGpoConfiguration;
        private bool _disableUserDefinedIpMappingRulesIsGPOConfigured;
        private string _policyDefinedIpMappingRulesGPOData;
        private bool _policyDefinedIpMappingRulesIsGPOConfigured;

        public string MachineHostName
        {
            get
            {
                try
                {
                    return Dns.GetHostName();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        private enum SocketStatus : int
        {
            NA = 0,
            Resolving = 1,
            Connecting = 2,
            Handshaking = 3,
            Error = 4,
            ForceClosed = 5,
            InvalidKey = 6,
            Timeout = 7,
            SendError = 8,
            Connected = 9,
        }

        private interface ISettingsSyncHelper
        {
            [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
            public struct MachineSocketState
            {
                // Disable false-positive warning due to IPC
#pragma warning disable CS0649
                [Newtonsoft.Json.JsonProperty]
                public string Name;

                [Newtonsoft.Json.JsonProperty]
                public SocketStatus Status;
#pragma warning restore CS0649
            }

            void Shutdown();

            void Reconnect();

            void GenerateNewKey();

            void ConnectToMachine(string machineName, string securityKey);

            Task<MachineSocketState[]> RequestMachineSocketStateAsync();
        }

        private static CancellationTokenSource _cancellationTokenSource;

        private static Task _machinePollingThreadTask;

        private static VisualStudio.Threading.AsyncSemaphore _ipcSemaphore = new VisualStudio.Threading.AsyncSemaphore(1);

        private sealed class SyncHelper : IDisposable
        {
            public SyncHelper(NamedPipeClientStream stream)
            {
                Stream = stream;
                Endpoint = JsonRpc.Attach<ISettingsSyncHelper>(Stream);
            }

            public NamedPipeClientStream Stream { get; }

            public ISettingsSyncHelper Endpoint { get; private set; }

            public void Dispose()
            {
                ((IDisposable)Endpoint).Dispose();
            }
        }

        private static NamedPipeClientStream syncHelperStream;

        private async Task<SyncHelper> GetSettingsSyncHelperAsync()
        {
            try
            {
                var recreateStream = false;
                if (syncHelperStream == null)
                {
                    recreateStream = true;
                }
                else
                {
                    if (!syncHelperStream.IsConnected || !syncHelperStream.CanWrite)
                    {
                        await syncHelperStream.DisposeAsync();
                        recreateStream = true;
                    }
                }

                if (recreateStream)
                {
                    syncHelperStream = new NamedPipeClientStream(".", "MouseWithoutBorders/SettingsSync", PipeDirection.InOut, PipeOptions.Asynchronous);
                    await syncHelperStream.ConnectAsync(10000);
                }

                return new SyncHelper(syncHelperStream);
            }
            catch (Exception ex)
            {
                if (IsEnabled)
                {
                    Logger.LogError($"Couldn't create SettingsSync: {ex}");
                }

                return null;
            }
        }

        public async Task SubmitShutdownRequestAsync()
        {
            using (await _ipcSemaphore.EnterAsync())
            {
                using (var syncHelper = await GetSettingsSyncHelperAsync())
                {
                    syncHelper?.Endpoint?.Shutdown();
                    var task = syncHelper?.Stream.FlushAsync();
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
        }

        public async Task SubmitReconnectRequestAsync()
        {
            using (await _ipcSemaphore.EnterAsync())
            {
                using (var syncHelper = await GetSettingsSyncHelperAsync())
                {
                    syncHelper?.Endpoint?.Reconnect();
                    var task = syncHelper?.Stream.FlushAsync();
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
        }

        public async Task SubmitNewKeyRequestAsync()
        {
            using (await _ipcSemaphore.EnterAsync())
            {
                using (var syncHelper = await GetSettingsSyncHelperAsync())
                {
                    syncHelper?.Endpoint?.GenerateNewKey();
                    var task = syncHelper?.Stream.FlushAsync();
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
        }

        public async Task SubmitConnectionRequestAsync(string pcName, string securityKey)
        {
            using (await _ipcSemaphore.EnterAsync())
            {
                using (var syncHelper = await GetSettingsSyncHelperAsync())
                {
                    syncHelper?.Endpoint?.ConnectToMachine(pcName, securityKey);
                    var task = syncHelper?.Stream.FlushAsync();
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
        }

        private async Task<ISettingsSyncHelper.MachineSocketState[]> PollMachineSocketStateAsync()
        {
            using (await _ipcSemaphore.EnterAsync())
            {
                using (var syncHelper = await GetSettingsSyncHelperAsync())
                {
                    var task = syncHelper?.Endpoint?.RequestMachineSocketStateAsync();
                    if (task != null)
                    {
                        return await task;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private MouseWithoutBordersSettings Settings { get; set; }

        private DispatcherQueue _uiDispatcherQueue;

        public MouseWithoutBordersViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, DispatcherQueue uiDispatcherQueue)
        {
            SettingsUtils = settingsUtils;

            _uiDispatcherQueue = uiDispatcherQueue;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();
            InitializePolicyValues();

            // MouseWithoutBorders settings may be changed by the logic in the utility as machines connect. We need to get a fresh version every time instead of using a repository.
            MouseWithoutBordersSettings moduleSettings;
            moduleSettings = SettingsUtils.GetSettingsOrDefault<MouseWithoutBordersSettings>("MouseWithoutBorders");

            LoadViewModelFromSettings(moduleSettings);

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            _cancellationTokenSource?.Cancel();

            _cancellationTokenSource = new CancellationTokenSource();

            _machinePollingThreadTask = StartMachineStatusPollingThread(_machinePollingThreadTask, _cancellationTokenSource.Token);
        }

        private Task StartMachineStatusPollingThread(Task previousThreadTask, CancellationToken token)
        {
            return Task.Run(
                async () =>
                {
                    previousThreadTask?.Wait();

                    while (!token.IsCancellationRequested)
                    {
                        Dictionary<string, ISettingsSyncHelper.MachineSocketState> states = null;
                        try
                        {
                            states = (await PollMachineSocketStateAsync())?.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogInfo($"Poll ISettingsSyncHelper.MachineSocketState error: {ex}");
                            continue;
                        }

                        if (states != null)
                        {
                            lock (_machineMatrixStringLock)
                            {
                                foreach (var machine in machineMatrixString)
                                {
                                    if (states.TryGetValue(machine.Item.Name, out var state))
                                    {
                                        _uiDispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                                        {
                                            try
                                            {
                                                machine.Item.StatusBrush = StatusColors[state.Status];
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        });
                                    }
                                }
                            }
                        }

                        Thread.Sleep(500);
                    }
                },
                _cancellationTokenSource.Token);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.MouseWithoutBorders;
            }
        }

        private void InitializePolicyValues()
        {
            // Policies supporting only enabled state
            _disallowBlockingScreensaverGpoConfiguration = GPOWrapper.GetConfiguredMwbDisallowBlockingScreensaverValue();
            _disallowBlockingScreensaverIsGPOConfigured = _disallowBlockingScreensaverGpoConfiguration == GpoRuleConfigured.Enabled;
            _disableUserDefinedIpMappingRulesGpoConfiguration = GPOWrapper.GetConfiguredMwbDisableUserDefinedIpMappingRulesValue();
            _disableUserDefinedIpMappingRulesIsGPOConfigured = _disableUserDefinedIpMappingRulesGpoConfiguration == GpoRuleConfigured.Enabled;

            // Policies supporting only disabled state
            _allowServiceModeGpoConfiguration = GPOWrapper.GetConfiguredMwbAllowServiceModeValue();
            _allowServiceModeIsGPOConfigured = _allowServiceModeGpoConfiguration == GpoRuleConfigured.Disabled;
            _clipboardSharingEnabledGpoConfiguration = GPOWrapper.GetConfiguredMwbClipboardSharingEnabledValue();
            _clipboardSharingEnabledIsGPOConfigured = _clipboardSharingEnabledGpoConfiguration == GpoRuleConfigured.Disabled;
            _fileTransferEnabledGpoConfiguration = GPOWrapper.GetConfiguredMwbFileTransferEnabledValue();
            _fileTransferEnabledIsGPOConfigured = _fileTransferEnabledGpoConfiguration == GpoRuleConfigured.Disabled;
            _useOriginalUserInterfaceGpoConfiguration = GPOWrapper.GetConfiguredMwbUseOriginalUserInterfaceValue();
            _useOriginalUserInterfaceIsGPOConfigured = _useOriginalUserInterfaceGpoConfiguration == GpoRuleConfigured.Disabled;

            // Policies supporting enabled and disabled state
            _sameSubnetOnlyGpoConfiguration = GPOWrapper.GetConfiguredMwbSameSubnetOnlyValue();
            _sameSubnetOnlyIsGPOConfigured = _sameSubnetOnlyGpoConfiguration == GpoRuleConfigured.Enabled || _sameSubnetOnlyGpoConfiguration == GpoRuleConfigured.Disabled;
            _validateRemoteIpGpoConfiguration = GPOWrapper.GetConfiguredMwbValidateRemoteIpValue();
            _validateRemoteIpIsGPOConfigured = _validateRemoteIpGpoConfiguration == GpoRuleConfigured.Enabled || _validateRemoteIpGpoConfiguration == GpoRuleConfigured.Disabled;

            // Special policies
            _policyDefinedIpMappingRulesGPOData = GPOWrapper.GetConfiguredMwbPolicyDefinedIpMappingRules();
            _policyDefinedIpMappingRulesIsGPOConfigured = !string.IsNullOrWhiteSpace(_policyDefinedIpMappingRulesGPOData);
        }

        private void LoadViewModelFromSettings(MouseWithoutBordersSettings moduleSettings)
        {
            ArgumentNullException.ThrowIfNull(moduleSettings);

            Settings = moduleSettings;
            /* TODO: Error handling */
            _selectedSwitchBetweenMachineShortcutOptionsIndex = Array.IndexOf(_switchBetweenMachineShortcutOptions, moduleSettings.Properties.HotKeySwitchMachine.Value);
            _easyMouseOptionIndex = (EasyMouseOption)moduleSettings.Properties.EasyMouse.Value;

            LoadMachineMatrixString();
        }

        // Loads the machine matrix, taking into account changes to the machine pool.
        private void LoadMachineMatrixString()
        {
            List<string> loadMachineMatrixString = Settings.Properties.MachineMatrixString ?? new List<string>() { string.Empty, string.Empty, string.Empty, string.Empty };

            if (loadMachineMatrixString.Count < 4)
            {
                // Current logic of MWB assumes there are always 4 slots. Any other configuration means data corruption here.
                loadMachineMatrixString = new List<string>() { string.Empty, string.Empty, string.Empty, string.Empty };
            }

            bool editedTheMatrix = false; // keep track of changes to the matrix because of changes to the available machine pool.

            if (!string.IsNullOrEmpty(Settings.Properties.MachinePool?.Value))
            {
                List<string> availableMachines = new List<string>();

                // Format of this field is "NAME1:ID1,NAME2:ID2,..."
                // Load the available machines
                foreach (string availableMachineIdPair in Settings.Properties.MachinePool.Value.Split(","))
                {
                    string availableMachineName = availableMachineIdPair.Split(':')[0];
                    availableMachines.Add(availableMachineName);
                }

                // Start by removing the machines from the matrix that are no longer available to pick.
                for (int i = 0; i < loadMachineMatrixString.Count; i++)
                {
                    if (!availableMachines.Contains(loadMachineMatrixString[i]))
                    {
                        editedTheMatrix = true;
                        loadMachineMatrixString[i] = string.Empty;
                    }
                }

                // If an available machine is not in the matrix already, fill it in the first available spot.
                foreach (string availableMachineName in availableMachines)
                {
                    if (!loadMachineMatrixString.Contains(availableMachineName))
                    {
                        int availableIndex = loadMachineMatrixString.FindIndex(name => string.IsNullOrEmpty(name));
                        if (availableIndex >= 0)
                        {
                            loadMachineMatrixString[availableIndex] = availableMachineName;
                            editedTheMatrix = true;
                        }
                    }
                }
            }

            // Dragging while elevated crashes on WinUI3: https://github.com/microsoft/microsoft-ui-xaml/issues/7690
            machineMatrixString = new IndexedObservableCollection<DeviceViewModel>(loadMachineMatrixString.Select(name => new DeviceViewModel { Name = name, CanDragDrop = !IsElevated }));

            if (editedTheMatrix)
            {
                // Set the property directly to save the new matrix right away with the new available machines.
                MachineMatrixString = machineMatrixString;
            }
        }

        public bool CanBeEnabled
        {
            get => !_enabledStateIsGPOConfigured;
        }

        public bool CanToggleUseService
        {
            get
            {
                return IsEnabled && !(!IsElevated && !UseService);
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.MouseWithoutBorders = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(ShowInfobarRunAsAdminText));
                    OnPropertyChanged(nameof(ShowInfobarCannotDragDropAsAdmin));
                    OnPropertyChanged(nameof(ShowPolicyConfiguredInfoForBehaviorSettings));
                    OnPropertyChanged(nameof(ShowPolicyConfiguredInfoForName2IPSetting));
                    OnPropertyChanged(nameof(ShowPolicyConfiguredInfoForOriginalUiSetting));
                    OnPropertyChanged(nameof(Name2IpListPolicyIsConfigured));

                    Task.Run(async () =>
                    {
                        if (!value)
                        {
                            try
                            {
                                await SubmitShutdownRequestAsync();
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Failed to shutdown MWB via SettingsSync: {ex}");
                            }
                        }

                        _uiDispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                        {
                            OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                            SendConfigMSG(outgoing.ToString());

                            NotifyPropertyChanged();

                            // Disable service mode if we're not elevated, because we cannot register service in that case
                            if (value == true && !IsElevated && UseService)
                            {
                                UseService = false;
                            }
                        });
                    });
                }
            }
        }

        public string SecurityKey
        {
            get => Settings.Properties.SecurityKey.Value;

            set
            {
                if (value != Settings.Properties.SecurityKey.Value)
                {
                    Settings.Properties.SecurityKey.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool WrapMouse
        {
            get
            {
                return Settings.Properties.WrapMouse;
            }

            set
            {
                if (Settings.Properties.WrapMouse != value)
                {
                    Settings.Properties.WrapMouse = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool MatrixOneRow
        {
            get
            {
                return Settings.Properties.MatrixOneRow;
            }

            set
            {
                if (Settings.Properties.MatrixOneRow != value)
                {
                    Settings.Properties.MatrixOneRow = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool ShareClipboard
        {
            get
            {
                if (_clipboardSharingEnabledGpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                return Settings.Properties.ShareClipboard;
            }

            set
            {
                if (!_clipboardSharingEnabledIsGPOConfigured && (Settings.Properties.ShareClipboard != value))
                {
                    Settings.Properties.ShareClipboard = value;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(TransferFile));
                    OnPropertyChanged(nameof(CardForTransferFileSettingIsEnabled));
                }
            }
        }

        public bool CardForShareClipboardSettingIsEnabled => _clipboardSharingEnabledIsGPOConfigured == false;

        public bool TransferFile
        {
            get
            {
                if (_fileTransferEnabledGpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                return Settings.Properties.TransferFile && Settings.Properties.ShareClipboard;
            }

            set
            {
                // If ShareClipboard is disabled the file transfer does not work and the setting is disabled. => Don't save toggle state.
                // If FileTransferGpo is configured the file transfer does not work and the setting is disabled. => Don't save toggle state.
                if (!ShareClipboard || _fileTransferEnabledIsGPOConfigured)
                {
                    return;
                }

                if (Settings.Properties.TransferFile != value)
                {
                    Settings.Properties.TransferFile = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool CardForTransferFileSettingIsEnabled
        {
            get => ShareClipboard && !_fileTransferEnabledIsGPOConfigured;
        }

        public bool HideMouseAtScreenEdge
        {
            get
            {
                return Settings.Properties.HideMouseAtScreenEdge;
            }

            set
            {
                if (Settings.Properties.HideMouseAtScreenEdge != value)
                {
                    Settings.Properties.HideMouseAtScreenEdge = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool DrawMouseCursor
        {
            get
            {
                return Settings.Properties.DrawMouseCursor;
            }

            set
            {
                if (Settings.Properties.DrawMouseCursor != value)
                {
                    Settings.Properties.DrawMouseCursor = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool ValidateRemoteMachineIP
        {
            get
            {
                if (_validateRemoteIpGpoConfiguration == GpoRuleConfigured.Enabled)
                {
                    return true;
                }
                else if (_validateRemoteIpGpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                return Settings.Properties.ValidateRemoteMachineIP;
            }

            set
            {
                if (!_validateRemoteIpIsGPOConfigured && (Settings.Properties.ValidateRemoteMachineIP != value))
                {
                    Settings.Properties.ValidateRemoteMachineIP = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool CardForValidateRemoteIpSettingIsEnabled => _validateRemoteIpIsGPOConfigured == false;

        public string Name2IP
        {
            // Due to https://github.com/microsoft/microsoft-ui-xaml/issues/1826, we must
            // add back \n chars on set and remove them on get for the widget
            // to make its behavior consistent with the old UI and MWB internal code.
            get
            {
                if (_disableUserDefinedIpMappingRulesGpoConfiguration == GpoRuleConfigured.Enabled)
                {
                    return string.Empty;
                }

                return Settings.Properties.Name2IP.Value.Replace("\r\n", "\r");
            }

            set
            {
                if (_disableUserDefinedIpMappingRulesIsGPOConfigured)
                {
                    return;
                }

                var newValue = value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                if (Settings.Properties.Name2IP.Value != newValue)
                {
                    Settings.Properties.Name2IP.Value = newValue;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool CardForName2IpSettingIsEnabled => _disableUserDefinedIpMappingRulesIsGPOConfigured == false;

        public bool ShowPolicyConfiguredInfoForName2IPSetting => _disableUserDefinedIpMappingRulesIsGPOConfigured && IsEnabled;

        public string Name2IpListPolicyData
        {
            // Due to https://github.com/microsoft/microsoft-ui-xaml/issues/1826, we must
            // add back \n chars on set and remove them on get for the widget
            // to make its behavior consistent with the old UI and MWB internal code.
            // get => GPOWrapper.GetConfiguredMwbPolicyDefinedIpMappingRules().Replace("\r\n", "\r");
            get => _policyDefinedIpMappingRulesGPOData.Replace("\r\n", "\r");
        }

        public bool Name2IpListPolicyIsConfigured => _policyDefinedIpMappingRulesIsGPOConfigured && IsEnabled;

        public bool SameSubnetOnly
        {
            get
            {
                if (_sameSubnetOnlyGpoConfiguration == GpoRuleConfigured.Enabled)
                {
                    return true;
                }
                else if (_sameSubnetOnlyGpoConfiguration == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                return Settings.Properties.SameSubnetOnly;
            }

            set
            {
                if (!_sameSubnetOnlyIsGPOConfigured && (Settings.Properties.SameSubnetOnly != value))
                {
                    Settings.Properties.SameSubnetOnly = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool CardForSameSubnetOnlySettingIsEnabled => _sameSubnetOnlyIsGPOConfigured == false;

        public bool BlockScreenSaverOnOtherMachines
        {
            get
            {
                if (_disallowBlockingScreensaverGpoConfiguration == GpoRuleConfigured.Enabled)
                {
                    return false;
                }

                return Settings.Properties.BlockScreenSaverOnOtherMachines;
            }

            set
            {
                if (_disallowBlockingScreensaverIsGPOConfigured)
                {
                    return;
                }

                if (Settings.Properties.BlockScreenSaverOnOtherMachines != value)
                {
                    Settings.Properties.BlockScreenSaverOnOtherMachines = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool CardForBlockScreensaverSettingIsEnabled => _disallowBlockingScreensaverIsGPOConfigured == false;

        // Should match EasyMouseOption enum from MouseWithoutBorders and the ComboBox in the MouseWithoutBordersView.cs
        private enum EasyMouseOption
        {
            Disable = 0,
            Enable = 1,
            Ctrl = 2,
            Shift = 3,
        }

        private EasyMouseOption _easyMouseOptionIndex;

        public int EasyMouseOptionIndex
        {
            get
            {
                return (int)_easyMouseOptionIndex;
            }

            set
            {
                if (value != (int)_easyMouseOptionIndex)
                {
                    _easyMouseOptionIndex = (EasyMouseOption)value;
                    Settings.Properties.EasyMouse.Value = value;
                    NotifyPropertyChanged(nameof(EasyMouseOptionIndex));
                }
            }
        }

        public HotkeySettings ToggleEasyMouseShortcut
        {
            get => Settings.Properties.ToggleEasyMouseShortcut;

            set
            {
                if (Settings.Properties.ToggleEasyMouseShortcut != value)
                {
                    Settings.Properties.ToggleEasyMouseShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeyToggleEasyMouse;
                    NotifyPropertyChanged();
                }
            }
        }

        public HotkeySettings LockMachinesShortcut
        {
            get => Settings.Properties.LockMachineShortcut;

            set
            {
                if (Settings.Properties.LockMachineShortcut != value)
                {
                    Settings.Properties.LockMachineShortcut = value;
                    Settings.Properties.LockMachineShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeyLockMachine;
                    NotifyPropertyChanged();
                }
            }
        }

        public HotkeySettings ReconnectShortcut
        {
            get => Settings.Properties.ReconnectShortcut;

            set
            {
                if (Settings.Properties.ReconnectShortcut != value)
                {
                    Settings.Properties.ReconnectShortcut = value;
                    Settings.Properties.ReconnectShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeyReconnect;
                    NotifyPropertyChanged();
                }
            }
        }

        public HotkeySettings HotKeySwitch2AllPC
        {
            get => Settings.Properties.Switch2AllPCShortcut;

            set
            {
                if (Settings.Properties.Switch2AllPCShortcut != value)
                {
                    Settings.Properties.Switch2AllPCShortcut = value;
                    Settings.Properties.Switch2AllPCShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeySwitch2AllPC;
                    NotifyPropertyChanged();
                }
            }
        }

        private int _selectedSwitchBetweenMachineShortcutOptionsIndex;

        public int SelectedSwitchBetweenMachineShortcutOptionsIndex
        {
            get
            {
                return _selectedSwitchBetweenMachineShortcutOptionsIndex;
            }

            set
            {
                if (_selectedSwitchBetweenMachineShortcutOptionsIndex != value)
                {
                    _selectedSwitchBetweenMachineShortcutOptionsIndex = value;
                    Settings.Properties.HotKeySwitchMachine.Value = _switchBetweenMachineShortcutOptions[value];
                    NotifyPropertyChanged();
                }
            }
        }

        public bool MoveMouseRelatively
        {
            get
            {
                return Settings.Properties.MoveMouseRelatively;
            }

            set
            {
                if (Settings.Properties.MoveMouseRelatively != value)
                {
                    Settings.Properties.MoveMouseRelatively = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool BlockMouseAtScreenCorners
        {
            get
            {
                return Settings.Properties.BlockMouseAtScreenCorners;
            }

            set
            {
                if (Settings.Properties.BlockMouseAtScreenCorners != value)
                {
                    Settings.Properties.BlockMouseAtScreenCorners = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private IndexedObservableCollection<DeviceViewModel> machineMatrixString;

        public class DeviceViewModel : Observable
        {
            public string Name { get; set; }

            public bool CanDragDrop { get; set; }

            private Brush _statusBrush = StatusColors[SocketStatus.NA];

            public Brush StatusBrush
            {
                get
                {
                    return _statusBrush;
                }

                set
                {
                    if (_statusBrush != value)
                    {
                        _statusBrush = value;
                        OnPropertyChanged(nameof(StatusBrush));
                    }
                }
            }
        }

        public IndexedObservableCollection<DeviceViewModel> MachineMatrixString
        {
            get
            {
                lock (_machineMatrixStringLock)
                {
                    return machineMatrixString;
                }
            }

            set
            {
                lock (_machineMatrixStringLock)
                {
                    machineMatrixString = value;
                }

                Settings.Properties.MachineMatrixString = new List<string>(value.ToEnumerable().Select(d => d.Name));
                NotifyPropertyChanged();
            }
        }

        public bool ShowClipboardAndNetworkStatusMessages
        {
            get
            {
                return Settings.Properties.ShowClipboardAndNetworkStatusMessages;
            }

            set
            {
                if (Settings.Properties.ShowClipboardAndNetworkStatusMessages != value)
                {
                    Settings.Properties.ShowClipboardAndNetworkStatusMessages = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool LoadUpdatedSettings()
        {
            try
            {
                LoadViewModelFromSettings(SettingsUtils.GetSettings<MouseWithoutBordersSettings>("MouseWithoutBorders"));
                return true;
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex.Message);
                return false;
            }
        }

        private void SendCustomAction(string actionName)
        {
            SendConfigMSG("{\"action\":{\"MouseWithoutBorders\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        public void AddFirewallRule()
        {
            SendCustomAction("add_firewall");
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private void NotifyModuleUpdatedSettings()
        {
            SendConfigMSG(
        string.Format(
        CultureInfo.InvariantCulture,
        "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
        MouseWithoutBordersSettings.ModuleName,
        JsonSerializer.Serialize(Settings)));
        }

        public void NotifyUpdatedSettings()
        {
            OnPropertyChanged(null); // Notify all properties might have changed.
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            // Skip saving settings for UI properties
            if (propertyName == nameof(ShowInfobarCannotDragDropAsAdmin) ||
                propertyName == nameof(ShowInfobarRunAsAdminText))
            {
                return;
            }

            SettingsUtils.SaveSettings(Settings.ToJsonString(), MouseWithoutBordersSettings.ModuleName);

            if (propertyName == nameof(UseService))
            {
                NotifyModuleUpdatedSettings();
            }
        }

        private Func<string, int> SendConfigMSG { get; }

        public void CopyMachineNameToClipboard()
        {
            var data = new DataPackage();
            data.SetText(Dns.GetHostName());
            Clipboard.SetContent(data);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        internal void UninstallService()
        {
            SendCustomAction("uninstall_service");
        }

        public bool ShowPolicyConfiguredInfoForServiceSettings
        {
            get
            {
                return IsEnabled && _allowServiceModeIsGPOConfigured;
            }
        }

        public bool ShowPolicyConfiguredInfoForBehaviorSettings
        {
            get
            {
                return IsEnabled && (_disallowBlockingScreensaverIsGPOConfigured
                    || _clipboardSharingEnabledIsGPOConfigured || _fileTransferEnabledIsGPOConfigured
                    || _sameSubnetOnlyIsGPOConfigured || _validateRemoteIpIsGPOConfigured);
            }
        }

        public bool ShowInfobarCannotDragDropAsAdmin
        {
            get { return IsElevated && IsEnabled; }
        }

        public bool ShowInfobarRunAsAdminText
        {
            get { return !CanToggleUseService && IsEnabled && !ShowPolicyConfiguredInfoForServiceSettings; }
        }
    }
}
