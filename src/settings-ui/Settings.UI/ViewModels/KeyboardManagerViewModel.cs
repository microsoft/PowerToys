// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.Utilities;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class KeyboardManagerViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly ISettingsUtils _settingsUtils;

        private const string PowerToyName = KeyboardManagerSettings.ModuleName;
        private const string JsonFileType = ".json";

        // Default editor path. Can be removed once the new WinUI3 editor is released.
        private const string KeyboardManagerEditorPath = "KeyboardManagerEditor\\PowerToys.KeyboardManagerEditor.exe";

        // New WinUI3 editor path. Still in development and do NOT use it in production.
        private const string KeyboardManagerEditorUIPath = "KeyboardManagerEditorUI\\PowerToys.KeyboardManagerEditorUI.exe";

        private Process editor;

        private enum KeyboardManagerEditorType
        {
            KeyEditor = 0,
            ShortcutEditor,
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        public KeyboardManagerSettings Settings { get; set; }

        private ICommand _remapKeyboardCommand;
        private ICommand _editShortcutCommand;
        private KeyboardManagerProfile _profile;

        private Func<string, int> SendConfigMSG { get; }

        private Func<List<KeysDataModel>, int> FilterRemapKeysList { get; }

        public KeyboardManagerViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<List<KeysDataModel>, int> filterRemapKeysList)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            FilterRemapKeysList = filterRemapKeysList;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            if (_settingsUtils.SettingsExists(PowerToyName))
            {
                try
                {
                    Settings = _settingsUtils.GetSettingsOrDefault<KeyboardManagerSettings>(PowerToyName);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception encountered while reading {PowerToyName} settings.", e);
#if DEBUG
                    if (e is ArgumentException || e is ArgumentNullException || e is PathTooLongException)
                    {
                        throw;
                    }
#endif
                }

                // Load profile.
                if (!LoadProfile())
                {
                    _profile = new KeyboardManagerProfile();
                }
            }
            else
            {
                Settings = new KeyboardManagerSettings();
                _settingsUtils.SaveSettings(Settings.ToJsonString(), PowerToyName);
            }
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredKeyboardManagerEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.KeyboardManager;
            }
        }

        public bool Enabled
        {
            get
            {
                return _isEnabled;
            }

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

                    GeneralSettingsConfig.Enabled.KeyboardManager = value;
                    OnPropertyChanged(nameof(Enabled));

                    if (!Enabled && editor != null)
                    {
                        editor.CloseMainWindow();
                    }

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        // store remappings
        public List<KeysDataModel> RemapKeys
        {
            get
            {
                if (_profile != null)
                {
                    return _profile.RemapKeys.InProcessRemapKeys.Concat(_profile.RemapKeysToText.InProcessRemapKeys).ToList();
                }
                else
                {
                    return new List<KeysDataModel>();
                }
            }
        }

        public static List<AppSpecificKeysDataModel> CombineShortcutLists(List<KeysDataModel> globalShortcutList, List<AppSpecificKeysDataModel> appSpecificShortcutList)
        {
            string allAppsDescription = "All Apps";
            try
            {
                var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
                allAppsDescription = resourceLoader.GetString("KeyboardManager_All_Apps_Description");
            }
            catch (Exception ex)
            {
                Logger.LogError("Couldn't get translation for All Apps mention in KBM page.", ex);
            }

            if (globalShortcutList == null && appSpecificShortcutList == null)
            {
                return new List<AppSpecificKeysDataModel>();
            }
            else if (globalShortcutList == null)
            {
                return appSpecificShortcutList;
            }
            else if (appSpecificShortcutList == null)
            {
                return globalShortcutList.ConvertAll(x => new AppSpecificKeysDataModel { OriginalKeys = x.OriginalKeys, NewRemapKeys = x.NewRemapKeys, NewRemapString = x.NewRemapString, RunProgramFilePath = x.RunProgramFilePath, OperationType = x.OperationType, OpenUri = x.OpenUri, SecondKeyOfChord = x.SecondKeyOfChord, RunProgramArgs = x.RunProgramArgs, TargetApp = allAppsDescription }).ToList();
            }
            else
            {
                return globalShortcutList.ConvertAll(x => new AppSpecificKeysDataModel { OriginalKeys = x.OriginalKeys, NewRemapKeys = x.NewRemapKeys, NewRemapString = x.NewRemapString, RunProgramFilePath = x.RunProgramFilePath, OperationType = x.OperationType, OpenUri = x.OpenUri, SecondKeyOfChord = x.SecondKeyOfChord, RunProgramArgs = x.RunProgramArgs, TargetApp = allAppsDescription }).Concat(appSpecificShortcutList).ToList();
            }
        }

        public List<AppSpecificKeysDataModel> RemapShortcuts
        {
            get
            {
                if (_profile != null)
                {
                    return CombineShortcutLists(_profile.RemapShortcuts.GlobalRemapShortcuts, _profile.RemapShortcuts.AppSpecificRemapShortcuts).Concat(CombineShortcutLists(_profile.RemapShortcutsToText.GlobalRemapShortcuts, _profile.RemapShortcutsToText.AppSpecificRemapShortcuts)).ToList();
                }
                else
                {
                    return new List<AppSpecificKeysDataModel>();
                }
            }
        }

        public ICommand RemapKeyboardCommand => _remapKeyboardCommand ?? (_remapKeyboardCommand = new RelayCommand(OnRemapKeyboard));

        public ICommand EditShortcutCommand => _editShortcutCommand ?? (_editShortcutCommand = new RelayCommand(OnEditShortcut));

        public void OnRemapKeyboard()
        {
            OpenEditor((int)KeyboardManagerEditorType.KeyEditor);
        }

        public void OnEditShortcut()
        {
            OpenEditor((int)KeyboardManagerEditorType.ShortcutEditor);
        }

        private static void BringProcessToFront(Process process)
        {
            if (process == null)
            {
                return;
            }

            IntPtr handle = process.MainWindowHandle;
            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SWRESTORE);
            }

            NativeMethods.SetForegroundWindow(handle);
        }

        private void OpenEditor(int type)
        {
            try
            {
                if (editor != null && editor.HasExited)
                {
                    Logger.LogInfo($"Previous instance of {PowerToyName} editor exited");
                    editor = null;
                }

                if (editor != null)
                {
                    Logger.LogInfo($"The {PowerToyName} editor instance {editor.Id} exists. Bringing the process to the front");
                    BringProcessToFront(editor);
                    return;
                }

                // Launch the new editor if:
                // 1. the experimentation toggle is enabled in the settings
                // 2. the new WinUI3 editor is enabled in the registry. The registry value does not exist by default and is only used for development purposes
                string editorPath = KeyboardManagerEditorPath;
                try
                {
                    // Check if the experimentation toggle is enabled in the settings
                    var settingsUtils = new SettingsUtils();
                    bool isExperimentationEnabled = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.EnableExperimentation;

                    // Only read the registry value if the experimentation toggle is enabled
                    if (isExperimentationEnabled)
                    {
                        // Read the registry value to determine which editor to launch
                        var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\PowerToys\Keyboard Manager");
                        if (key != null && (int?)key.GetValue("UseNewEditor") == 1)
                        {
                            editorPath = KeyboardManagerEditorUIPath;
                        }

                        // Close the registry key
                        key?.Close();
                    }
                }
                catch (Exception e)
                {
                    // Fall back to the default editor path if any exception occurs
                    Logger.LogError("Failed to launch the new WinUI3 Editor", e);
                }

                string path = Path.Combine(Environment.CurrentDirectory, editorPath);
                Logger.LogInfo($"Starting {PowerToyName} editor from {path}");

                // InvariantCulture: type represents the KeyboardManagerEditorType enum value
                editor = Process.Start(path, $"{type.ToString(CultureInfo.InvariantCulture)} {Environment.ProcessId}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered when opening an {PowerToyName} editor", e);
            }
        }

        public void NotifyFileChanged()
        {
            OnPropertyChanged(nameof(RemapKeys));
            OnPropertyChanged(nameof(RemapShortcuts));
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(Enabled));
        }

        public bool LoadProfile()
        {
            var success = true;
            var readSuccessfully = false;

            // The KBM process out of runner doesn't create the default.json file if it does not exist.
            string fileName = Settings.Properties.ActiveConfiguration.Value + JsonFileType;
            var profileExists = false;

            try
            {
                // retry loop for reading
                CancellationTokenSource ts = new CancellationTokenSource();
                Task t = Task.Run(() =>
                {
                    while (!readSuccessfully && !ts.IsCancellationRequested)
                    {
                        profileExists = _settingsUtils.SettingsExists(PowerToyName, fileName);
                        if (!profileExists)
                        {
                            break;
                        }
                        else
                        {
                            try
                            {
                                _profile = _settingsUtils.GetSettingsOrDefault<KeyboardManagerProfile>(PowerToyName, fileName);
                                readSuccessfully = true;
                            }
                            catch (Exception e)
                            {
                                Logger.LogError($"Exception encountered when reading {PowerToyName} settings", e);
                            }
                        }

                        if (!readSuccessfully)
                        {
                            Task.Delay(500, ts.Token).Wait(ts.Token);
                        }
                    }
                });

                var completedInTime = t.Wait(3000, ts.Token);
                ts.Cancel();
                ts.Dispose();

                if (readSuccessfully)
                {
                    FilterRemapKeysList(_profile?.RemapKeys?.InProcessRemapKeys);
                    FilterRemapKeysList(_profile?.RemapKeysToText?.InProcessRemapKeys);
                }
                else
                {
                    success = false;
                }

                if (!completedInTime)
                {
                    Logger.LogError($"Timeout encountered when loading {PowerToyName} profile");
                }
            }
            catch (Exception e)
            {
                // Failed to load the configuration.
                Logger.LogError($"Exception encountered when loading {PowerToyName} profile", e);
                success = false;
            }

            if (!profileExists)
            {
                Logger.LogInfo($"Couldn't load {PowerToyName} profile because it doesn't exist");
            }
            else if (!success)
            {
                Logger.LogError($"Couldn't load {PowerToyName} profile");
            }

            return success;
        }
    }
}
