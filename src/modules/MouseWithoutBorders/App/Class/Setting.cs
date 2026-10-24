// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

// <summary>
//     Application settings.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using Microsoft.Win32;
using MouseWithoutBorders.Core;
using Settings.UI.Library.Attributes;

using Lock = System.Threading.Lock;
using SettingsHelper = Microsoft.PowerToys.Settings.UI.Library.Utilities.Helper;

[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Properties.Setting.Values.#LoadIntSetting(System.String,System.Int32)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Properties.Setting.Values.#SaveSetting(System.String,System.Object)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Properties.Setting.Values.#LoadStringSetting(System.String,System.String)", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.Properties.Setting.Values.#SaveSettingQWord(System.String,System.Int64)", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
    internal class Settings
    {
        internal bool Changed;

        private readonly SettingsUtils _settingsUtils;
        private readonly Lock _loadingSettingsLock = new Lock();
        private readonly Lock _pairingKeyLock = new Lock();
        private readonly IFileSystemWatcher _watcher;
        private readonly TaskScheduler _saveScheduler = TaskScheduler.Default;
        private readonly IPairingKeyCodec _pairingKeyCodec;
        private long _saveSequence;
        private long _lastWrittenSequence;

        private MouseWithoutBordersProperties _properties;
        private MouseWithoutBordersSettings _settings;

        internal static bool IsProtectedMode
        {
            get
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return identity.IsSystem;
            }
        }

        // Avoid instantly saving every change to the file when updating properties.
        public bool PauseInstantSaving { get; set; }

        private void UpdateSettingsFromJson()
        {
            try
            {
                if (!_settingsUtils.SettingsExists("MouseWithoutBorders"))
                {
                    var defaultSettings = new MouseWithoutBordersSettings();
                    if (!Common.RunOnLogonDesktop)
                    {
                        defaultSettings.Save(_settingsUtils);
                    }
                }

                var settings = _settingsUtils.GetSettingsOrDefault<MouseWithoutBordersSettings>("MouseWithoutBorders");
                if (settings != null)
                {
                    PauseInstantSaving = true;

                    ApplyLoadedSettings(
                        settings,
                        () => CustomCursor.ShowFakeMouseCursor(int.MinValue, int.MinValue),
                        () => MachineStuff.MachineMatrix = null,
                        MachineStuff.SendMachineMatrix,
                        SaveSettings);
                }
            }
            catch (IOException ex)
            {
                EventLogger.LogEvent($"Failed to read settings: {ex.Message}", System.Diagnostics.EventLogEntryType.Error);
            }

            PauseInstantSaving = false;
        }

        // Exercise the reload ordering with substitute cursor/cache/network/save effects in tests.
        // Production supplies the real effects above; adoption and comparison stay in this path.
        internal void ApplyLoadedSettings(
            MouseWithoutBordersSettings settings,
            Action hideFakeMouseCursor,
            Action invalidateMachineMatrix,
            Action sendMachineMatrix,
            Action saveSettings)
        {
            var last_properties = _properties;

            _settings = settings;
            _properties = settings.Properties;

            // Keep track of the need to resend the machine matrix.
            bool shouldSendMachineMatrix = false;

            // Keep track of the need to save into the settings file.
            bool shouldSaveNewSettingsValues = false;

            if (last_properties != null)
            {
                // Same as in CheckBoxCircle_CheckedChanged
                if (last_properties.WrapMouse != _settings.Properties.WrapMouse)
                {
                    shouldSendMachineMatrix = true;
                }

                // Same as CheckBoxDrawMouse_CheckedChanged
                if (last_properties.DrawMouseCursor != _settings.Properties.DrawMouseCursor && !_settings.Properties.DrawMouseCursor)
                {
                    hideFakeMouseCursor();
                }

                // The cursor path reads DrawMouse under _loadingSettingsLock. A writer may
                // finish replacing last_properties' matrix during that wait, so compare here,
                // not when adopting the loaded properties above.
                if (!Enumerable.SequenceEqual(last_properties.MachineMatrixString, _settings.Properties.MachineMatrixString))
                {
                    invalidateMachineMatrix(); // Forces read next time it's needed.
                    shouldSendMachineMatrix = true;
                }

                var shouldReopenSockets = UpdateRuntimeKey();

                if (shouldReopenSockets)
                {
                    SocketStuff.InvalidKeyFound = false;
                    InitAndCleanup.ReopenSocketDueToReadError = true;
                    Common.ReopenSockets(true);
                }

                if (shouldSendMachineMatrix)
                {
                    sendMachineMatrix();
                    shouldSaveNewSettingsValues = true;
                }

                if (shouldSaveNewSettingsValues)
                {
                    saveSettings();
                }
            }
        }

        public void SaveSettings()
        {
            if (!Common.RunOnLogonDesktop)
            {
                SaveSettingsToJson();
            }
        }

        private void SaveSettingsToJson()
        {
            string serializedSettings;
            long sequence;
            lock (_loadingSettingsLock)
            {
                var currentSettings = _settings;
                var settingsToSave = new MouseWithoutBordersSettings
                {
                    Name = currentSettings.Name,
                    Version = currentSettings.Version,
                    Properties = (MouseWithoutBordersProperties)_properties.Clone(),
                };

                // Capture immutable JSON before a reload or later mutation can replace the queued snapshot.
                serializedSettings = settingsToSave.ToSnapshotJson();
                sequence = ++_saveSequence;
            }

            _ = Task.Factory.StartNew(
                () =>
            {
                bool saved = false;

                for (int i = 0; i < 5; ++i)
                {
                    try
                    {
                        lock (_loadingSettingsLock)
                        {
                            // A newer snapshot may have finished while this task was queued or retrying.
                            if (sequence < _lastWrittenSequence)
                            {
                                return;
                            }

                            _settingsUtils.SaveSettings(serializedSettings, MouseWithoutBordersSettings.ModuleName);
                            _lastWrittenSequence = sequence;
                        }

                        saved = true;
                    }
                    catch (IOException ex)
                    {
                        EventLogger.LogEvent($"Failed to write settings: {ex.Message}", System.Diagnostics.EventLogEntryType.Error);
                    }

                    if (saved)
                    {
                        break;
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
            },
                System.Threading.CancellationToken.None,
                TaskCreationOptions.None,
                _saveScheduler);
        }

        internal Settings(IPairingKeyCodec pairingKeyCodec)
        {
            ArgumentNullException.ThrowIfNull(pairingKeyCodec);
            _pairingKeyCodec = pairingKeyCodec;
            _settingsUtils = SettingsUtils.Default;

            _watcher = SettingsHelper.GetFileWatcher("MouseWithoutBorders", "settings.json", () =>
            {
                try
                {
                    UpdateSettingsFromJson();
                }
                catch (Exception ex)
                {
                    EventLogger.LogEvent($"Failed to update settings: {ex.Message}", System.Diagnostics.EventLogEntryType.Error);
                }
            });

            UpdateSettingsFromJson();
        }

        // Initialize the same settings state without starting a file watcher or network work.
        internal Settings(SettingsUtils settingsUtils, MouseWithoutBordersSettings settings, IPairingKeyCodec pairingKeyCodec, TaskScheduler saveScheduler = null)
        {
            ArgumentNullException.ThrowIfNull(pairingKeyCodec);
            _pairingKeyCodec = pairingKeyCodec;
            _settingsUtils = settingsUtils;
            _settings = settings;
            _properties = settings.Properties;
            _saveScheduler = saveScheduler ?? TaskScheduler.Default;
        }

        internal string Username { get; set; }

        internal bool IsMyKeyRandom { get; set; }

        internal string MachineMatrixString
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return string.Join(",", _properties.MachineMatrixString);
                }
            }

            set => SetMachineMatrix(value.Split(","));
        }

        internal void SetMachineMatrix(IEnumerable<string> machineNames)
        {
            lock (_loadingSettingsLock)
            {
                // Capture one properties instance and replace its list in one assignment.
                // Reload adoption is intentionally not under this lock: MachineStuff takes
                // McMatrixLock before this lock, so reversing that order could deadlock.
                _properties.ReplaceMachineMatrixString(machineNames);
                if (!PauseInstantSaving)
                {
                    SaveSettings();
                }
            }
        }

        internal string MachinePoolString
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.MachinePool.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    if (!value.Equals(_properties.MachinePool.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        _properties.MachinePool.Value = value;
                    }
                }
            }
        }

        internal string MyID => Application.ProductName + " Application";

        internal string MyIdEx => Application.ProductName + " Application-Ex";

        internal bool ShareClipboard
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbClipboardSharingEnabledValue() == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.ShareClipboard;
                }
            }

            set
            {
                if (ShareClipboardIsGpoConfigured)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.ShareClipboard = value;
                }
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool ShareClipboardIsGpoConfigured => GPOWrapper.GetConfiguredMwbClipboardSharingEnabledValue() == GpoRuleConfigured.Disabled;

        internal bool TransferFile
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbFileTransferEnabledValue() == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.TransferFile;
                }
            }

            set
            {
                if (TransferFileIsGpoConfigured)
                {
                    return;
                }

                _properties.TransferFile = value;
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool TransferFileIsGpoConfigured => GPOWrapper.GetConfiguredMwbFileTransferEnabledValue() == GpoRuleConfigured.Disabled;

        internal bool MatrixOneRow
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.MatrixOneRow;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.MatrixOneRow = value;
                    if (!PauseInstantSaving)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        internal bool MatrixCircle
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.WrapMouse;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.WrapMouse = value;
                    if (!PauseInstantSaving)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        internal int EasyMouse
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.EasyMouse.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.EasyMouse.Value = value;
                    if (!PauseInstantSaving)
                    {
                        // Easy Mouse can be enabled or disabled through a shortcut, so a save is required.
                        SaveSettings();
                    }
                }
            }
        }

        internal bool BlockMouseAtCorners
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.BlockMouseAtScreenCorners;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.BlockMouseAtScreenCorners = value;
                }
            }
        }

        internal bool DisableEasyMouseWhenForegroundWindowIsFullscreen
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.DisableEasyMouseWhenForegroundWindowIsFullscreen;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.DisableEasyMouseWhenForegroundWindowIsFullscreen = value;
                }
            }
        }

        internal HashSet<string> EasyMouseFullscreenSwitchBlockExcludedApps
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.EasyMouseFullscreenSwitchBlockExcludedApps.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.EasyMouseFullscreenSwitchBlockExcludedApps.Value = value;
                }
            }
        }

        internal string Enc(string st, bool dec, DataProtectionScope protectionScope)
        {
            if (st == null || st.Length < 1)
            {
                return string.Empty;
            }

            byte[] ep = Common.GetBytesU(st);
            byte[] rv, st2;

            if (dec)
            {
                st2 = Convert.FromBase64String(st);
                rv = ProtectedData.Unprotect(st2, ep, protectionScope);
                return Common.GetStringU(rv);
            }
            else
            {
                st2 = Common.GetBytesU(st);
                rv = ProtectedData.Protect(st2, ep, protectionScope);
                return Convert.ToBase64String(rv);
            }
        }

        internal string PairingKeyDisplayText
        {
            get
            {
                if (IsProtectedMode)
                {
                    return Encryption.MyKey ?? string.Empty;
                }

                lock (_loadingSettingsLock)
                {
                    if (_pairingKeyCodec.IsEncrypted(_properties.SecurityKey.Value))
                    {
                        return PairingKeyCodec.IsEncryptedValue(_properties.SecurityKey.Value)
                            ? MouseWithoutBordersIpc.PairingConfiguredIndicator
                            : string.Empty;
                    }

                    return _properties.SecurityKey.Value;
                }
            }
        }

        internal bool IsPairingKeyModeMismatch
        {
            get
            {
                lock (_pairingKeyLock)
                {
                    var storedKey = _properties.SecurityKey.Value;
                    return !string.IsNullOrEmpty(storedKey) &&
                        (_pairingKeyCodec is DpapiPairingKeyCodec) != PairingKeyCodec.IsEncryptedValue(storedKey);
                }
            }
        }

        internal string MyKey
        {
            get
            {
                lock (_pairingKeyLock)
                {
                    if (!IsProtectedMode && string.IsNullOrEmpty(_properties.SecurityKey.Value))
                    {
                        string randomKey = Encryption.CreateDefaultKey();
                        _properties.SecurityKey.Value = _pairingKeyCodec.Encrypt(randomKey);
                    }

                    // Only the settings JSON carries the encoded value; callers always receive plaintext.
                    var key = _pairingKeyCodec.Decrypt(_properties.SecurityKey.Value);
                    if (PairingKeyCodec.IsEncryptedValue(key))
                    {
                        throw new CryptographicException("The saved pairing key cannot be used in the current mode.");
                    }

                    return key;
                }
            }

            set
            {
                lock (_pairingKeyLock)
                {
                    bool reuseStoredKey = false;
                    var storedKey = _properties.SecurityKey.Value;
                    if (!string.IsNullOrEmpty(storedKey))
                    {
                        try
                        {
                            reuseStoredKey = string.Equals(_pairingKeyCodec.Decrypt(storedKey), value, StringComparison.Ordinal);
                        }
                        catch (Exception ex) when (ex is CryptographicException or ArgumentException or FormatException)
                        {
                            EventLogger.LogEvent($"Failed to compare stored pairing key: {ex.GetType().Name}", System.Diagnostics.EventLogEntryType.Warning);
                        }
                    }

                    if (!reuseStoredKey)
                    {
                        storedKey = _pairingKeyCodec.Encrypt(value);
                    }

                    // Reload adoption can replace the properties during either codec call.
                    _properties.SecurityKey.Value = storedKey;

                    if (!PauseInstantSaving)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        internal bool UpdateRuntimeKey()
        {
            string key;
            bool keyCorrupted = false;
            try
            {
                key = MyKey;
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException or FormatException)
            {
                key = string.Empty;
                keyCorrupted = !IsPairingKeyModeMismatch;
            }

            Encryption.KeyCorrupted = keyCorrupted;
            if (Encryption.MyKey == key)
            {
                return false;
            }

            Encryption.MyKey = key;
            Encryption.MagicNumber = string.IsNullOrEmpty(key) ? 0 : Encryption.Get24BitHash(key);
            return true;
        }

        internal int MyKeyDaysToExpire
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return int.MaxValue; // TODO(@yuyoyuppe): do we still need expiration mechanics now?
                }
            }
        }

        // Note(@htcfreek): Settings UI CheckBox is disabled in frmMatrix.cs > FrmMatrix_Load()
        // Note(@htcfreek): If this settings gets implemented in the future we need a Group Policy for it!
        internal bool DisableCAD
        {
            get
            {
                return false;
            }
        }

        // Note(@htcfreek): Settings UI CheckBox is disabled in frmMatrix.cs > FrmMatrix_Load()
        // Note(@htcfreek): If this settings gets implemented in the future we need a Group Policy for it!
        internal bool HideLogonLogo
        {
            get
            {
                return false;
            }
        }

        internal bool HideMouse
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.HideMouseAtScreenEdge;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.HideMouseAtScreenEdge = value;
                }
            }
        }

        internal bool BlockScreenSaver
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbDisallowBlockingScreensaverValue() == GpoRuleConfigured.Enabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.BlockScreenSaverOnOtherMachines;
                }
            }

            set
            {
                if (BlockScreenSaverIsGpoConfigured)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.BlockScreenSaverOnOtherMachines = value;
                }
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool BlockScreenSaverIsGpoConfigured => GPOWrapper.GetConfiguredMwbDisallowBlockingScreensaverValue() == GpoRuleConfigured.Enabled;

        internal bool MoveMouseRelatively
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.MoveMouseRelatively;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.MoveMouseRelatively = value;
                }
            }
        }

        internal string LastPersonalizeLogonScr
        {
            get
            {
                return string.Empty;
            }
        }

        internal uint DesMachineID
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return (uint)_properties.MachineID.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.MachineID.Value = (int)value;
                }
            }
        }

        internal int LastX
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.LastX.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    Common.LastX = value;
                    _properties.LastX.Value = value;
                }
            }
        }

        internal int LastY
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.LastY.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    Common.LastY = value;
                    _properties.LastY.Value = value;
                }
            }
        }

        internal int PackageID
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.PackageID.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.PackageID.Value = value;
                }
            }
        }

        internal bool FirstRun
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.FirstRun;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.FirstRun = value;
                    if (!PauseInstantSaving)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        internal int HotKeySwitchMachine
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.HotKeySwitchMachine.Value;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.HotKeySwitchMachine.Value = value;
                }
            }
        }

        internal HotkeySettings HotKeySwitch2AllPC
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.Switch2AllPCShortcut;
                }
            }
        }

        internal HotkeySettings HotKeyToggleEasyMouse
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.ToggleEasyMouseShortcut;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.ToggleEasyMouseShortcut = value;
                }
            }
        }

        internal HotkeySettings HotKeyLockMachine
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.LockMachineShortcut;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.LockMachineShortcut = value;
                }
            }
        }

        internal HotkeySettings HotKeyReconnect
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.ReconnectShortcut;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.ReconnectShortcut = value;
                }
            }
        }

        internal int HotKeyExitMM
        {
            get
            {
                return 0;
            }
        }

        private int switchCount;

        internal int SwitchCount
        {
            get
            {
                return switchCount;
            }

            set
            {
                switchCount = value;
                if (!PauseInstantSaving)
                {
                    SaveSettings();
                }
            }
        }

        internal int DumpObjectsLevel => 6;

        internal int TcpPort => _properties.TCPPort.Value;

        internal bool DrawMouse
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.DrawMouseCursor;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.DrawMouseCursor = value;
                }
            }
        }

        [CmdConfigureIgnore]
        internal bool DrawMouseEx
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.DrawMouseEx;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.DrawMouseEx = value;
                }
            }
        }

        internal bool ReverseLookup
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbValidateRemoteIpValue() == GpoRuleConfigured.Enabled)
                {
                    return true;
                }
                else if (GPOWrapper.GetConfiguredMwbValidateRemoteIpValue() == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.ValidateRemoteMachineIP;
                }
            }

            set
            {
                if (ReverseLookupIsGpoConfigured)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.ValidateRemoteMachineIP = value;
                }
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool ReverseLookupIsGpoConfigured => GPOWrapper.GetConfiguredMwbValidateRemoteIpValue() == GpoRuleConfigured.Enabled || GPOWrapper.GetConfiguredMwbValidateRemoteIpValue() == GpoRuleConfigured.Disabled;

        internal bool SameSubNetOnly
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbSameSubnetOnlyValue() == GpoRuleConfigured.Enabled)
                {
                    return true;
                }
                else if (GPOWrapper.GetConfiguredMwbSameSubnetOnlyValue() == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.SameSubnetOnly;
                }
            }

            set
            {
                if (SameSubNetOnlyIsGpoConfigured)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.SameSubnetOnly = value;
                }
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool SameSubNetOnlyIsGpoConfigured => GPOWrapper.GetConfiguredMwbSameSubnetOnlyValue() == GpoRuleConfigured.Enabled || GPOWrapper.GetConfiguredMwbSameSubnetOnlyValue() == GpoRuleConfigured.Disabled;

        internal string Name2IP
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbDisableUserDefinedIpMappingRulesValue() == GpoRuleConfigured.Enabled)
                {
                    return string.Empty;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.Name2IP.Value;
                }
            }

            set
            {
                if (Name2IpIsGpoConfigured)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.Name2IP.Value = value;
                }
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool Name2IpIsGpoConfigured => GPOWrapper.GetConfiguredMwbDisableUserDefinedIpMappingRulesValue() == GpoRuleConfigured.Enabled;

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal string Name2IpPolicyList => GPOWrapper.GetConfiguredMwbPolicyDefinedIpMappingRules();

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool Name2IpPolicyListIsGpoConfigured => !string.IsNullOrWhiteSpace(Name2IpPolicyList);

        internal bool FirstCtrlShiftS
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.FirstCtrlShiftS;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.FirstCtrlShiftS = value;
                }
            }
        }

        // Was a value read from registry on original Mouse Without Border, but default should be true. We wrongly released it as false, so we're forcing true here.
        // This value wasn't changeable from UI, anyway.
        internal bool StealFocusWhenSwitchingMachine => true;

        private string deviceId;

        internal string DeviceId
        {
            get
            {
                string newGuid = Guid.NewGuid().ToString();

                if (deviceId == null || deviceId.Length != newGuid.Length)
                {
                    string defaultId = newGuid;
                    lock (_loadingSettingsLock)
                    {
                        _properties.DeviceID = defaultId;
                        deviceId = _properties.DeviceID.Value;

                        if (deviceId.Equals(defaultId, StringComparison.OrdinalIgnoreCase))
                        {
                            return _properties.DeviceID.Value;
                        }
                    }
                }

                return deviceId;
            }
        }

        private int? machineId;

        internal int MachineId
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    machineId ??= (machineId = _properties.MachineID.Value).Value;

                    if (machineId == 0)
                    {
                        var newMachineId = Encryption.Ran.Next();
                        _properties.MachineID.Value = newMachineId;
                        machineId = newMachineId;
                        if (!PauseInstantSaving)
                        {
                            SaveSettings();
                        }
                    }
                }

                return machineId.Value;
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.MachineID.Value = value;
                    machineId = value;
                    if (!PauseInstantSaving)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        internal bool OneWayControlMode => false;

        internal bool OneWayClipboardMode => false;

        internal bool ShowClipNetStatus
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.ShowClipboardAndNetworkStatusMessages;
                }
            }

            set
            {
                lock (_loadingSettingsLock)
                {
                    _properties.ShowClipboardAndNetworkStatusMessages = value;
                }
            }
        }

        internal bool ShowOriginalUI
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbUseOriginalUserInterfaceValue() == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.ShowOriginalUI;
                }
            }

            set
            {
                if (GPOWrapper.GetConfiguredMwbUseOriginalUserInterfaceValue() == GpoRuleConfigured.Disabled)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.ShowOriginalUI = value;
                }
            }
        }

        internal bool AllowNonConsoleSessions
        {
            get
            {
                lock (_loadingSettingsLock)
                {
                    return _properties.AllowNonConsoleSessions.Value;
                }
            }
        }

        // If starting the service fails, work in not service mode.
        internal bool UseService
        {
            get
            {
                if (GPOWrapper.GetConfiguredMwbAllowServiceModeValue() == GpoRuleConfigured.Disabled)
                {
                    return false;
                }

                lock (_loadingSettingsLock)
                {
                    return _properties.UseService;
                }
            }

            set
            {
                if (AllowServiceModeIsGpoConfigured)
                {
                    return;
                }

                lock (_loadingSettingsLock)
                {
                    _properties.UseService = value;
                    if (!PauseInstantSaving)
                    {
                        SaveSettings();
                    }
                }
            }
        }

        [CmdConfigureIgnore]
        [JsonIgnore]
        internal bool AllowServiceModeIsGpoConfigured => GPOWrapper.GetConfiguredMwbAllowServiceModeValue() == GpoRuleConfigured.Disabled;

        // Note(@htcfreek): Settings UI CheckBox is disabled in frmMatrix.cs > FrmMatrix_Load()
        internal bool SendErrorLogV2
        {
            get
            {
                return false;
            }
        }
    }

    public static class Setting
    {
        internal static Settings Values = new Settings(CreatePairingKeyCodec());

        private static IPairingKeyCodec CreatePairingKeyCodec()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var ownerSid = Program.ResolveSettingsIpcUserSid(
                identity.User,
                System.Diagnostics.Process.GetCurrentProcess().SessionId,
                Program.GetInteractiveSessionUserSid);
            return DpapiPairingKeyCodec.CreateForCurrentIdentity(ownerSid);
        }
    }
}
