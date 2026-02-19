// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class KeyboardManagerViewModel : PageViewModelBase
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly SettingsUtils _settingsUtils;

        protected override string ModuleName => KeyboardManagerSettings.ModuleName;

        private const string JsonFileType = ".json";

        // Default editor path. Can be removed once the new WinUI3 editor is released.
        private const string KeyboardManagerEditorPath = "KeyboardManagerEditor\\PowerToys.KeyboardManagerEditor.exe";

        // New WinUI3 editor path. Still in development and do NOT use it in production.
        private const string KeyboardManagerEditorUIPath = "WinUI3Apps\\PowerToys.KeyboardManagerEditorUI.exe";

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
        private ICommand _openNewEditorCommand;
        private KeyboardManagerProfile _profile;

        private Func<string, int> SendConfigMSG { get; }

        private Func<List<KeysDataModel>, int> FilterRemapKeysList { get; }

        public KeyboardManagerViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<List<KeysDataModel>, int> filterRemapKeysList)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            FilterRemapKeysList = filterRemapKeysList;

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));

            if (_settingsUtils.SettingsExists(ModuleName))
            {
                try
                {
                    Settings = _settingsUtils.GetSettingsOrDefault<KeyboardManagerSettings>(ModuleName);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception encountered while reading {ModuleName} settings.", e);
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
                _settingsUtils.SaveSettings(Settings.ToJsonString(), ModuleName);
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

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [ToggleShortcut],
            };

            return hotkeysDict;
        }

        public HotkeySettings ToggleShortcut
        {
            get => Settings.Properties.ToggleShortcut;
            set
            {
                if (Settings.Properties.ToggleShortcut != value)
                {
                    Settings.Properties.ToggleShortcut = value ?? Settings.Properties.DefaultToggleShortcut;
                    OnPropertyChanged(nameof(ToggleShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public bool UseNewEditor
        {
            get => Settings.Properties.UseNewEditor;
            set
            {
                if (Settings.Properties.UseNewEditor != value)
                {
                    Settings.Properties.UseNewEditor = value;
                    OnPropertyChanged(nameof(UseNewEditor));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       ModuleName,
                       JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.KeyboardManagerSettings)));
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

        public ICommand OpenNewEditorCommand => _openNewEditorCommand ?? (_openNewEditorCommand = new RelayCommand(OnOpenNewEditor));

        public void OnRemapKeyboard()
        {
            OpenEditor((int)KeyboardManagerEditorType.KeyEditor);
        }

        public void OnEditShortcut()
        {
            OpenEditor((int)KeyboardManagerEditorType.ShortcutEditor);
        }

        public void OnOpenNewEditor()
        {
            OpenNewEditor();
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
                    Logger.LogInfo($"Previous instance of {ModuleName} editor exited");
                    editor = null;
                }

                if (editor != null)
                {
                    Logger.LogInfo($"The {ModuleName} editor instance {editor.Id} exists. Bringing the process to the front");
                    BringProcessToFront(editor);
                    return;
                }

                string path = Path.Combine(Environment.CurrentDirectory, KeyboardManagerEditorPath);
                Logger.LogInfo($"Starting {ModuleName} editor from {path}");

                // InvariantCulture: type represents the KeyboardManagerEditorType enum value
                ProcessStartInfo startInfo = new ProcessStartInfo(path);
                startInfo.UseShellExecute = true; // LOAD BEARING
                startInfo.Arguments = $"{type.ToString(CultureInfo.InvariantCulture)} {Environment.ProcessId}";
                System.Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", null);
                editor = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered when opening an {ModuleName} editor", e);
            }
        }

        private void OpenNewEditor()
        {
            try
            {
                if (editor != null && editor.HasExited)
                {
                    Logger.LogInfo($"Previous instance of {ModuleName} editor exited");
                    editor = null;
                }

                if (editor != null)
                {
                    Logger.LogInfo($"The {ModuleName} editor instance {editor.Id} exists. Bringing the process to the front");
                    BringProcessToFront(editor);
                    return;
                }

                string path = Path.Combine(Environment.CurrentDirectory, KeyboardManagerEditorUIPath);
                Logger.LogInfo($"Starting {ModuleName} new editor from {path}");

                System.Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", null);
                ProcessStartInfo startInfo = new ProcessStartInfo(path);
                startInfo.UseShellExecute = true; // LOAD BEARING
                startInfo.Arguments = $"{Environment.ProcessId}";
                editor = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered when opening the new {ModuleName} editor", e);
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
                        profileExists = _settingsUtils.SettingsExists(ModuleName, fileName);
                        if (!profileExists)
                        {
                            break;
                        }
                        else
                        {
                            try
                            {
                                _profile = _settingsUtils.GetSettingsOrDefault<KeyboardManagerProfile>(ModuleName, fileName);
                                readSuccessfully = true;
                            }
                            catch (Exception e)
                            {
                                Logger.LogError($"Exception encountered when reading {ModuleName} settings", e);
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
                    Logger.LogError($"Timeout encountered when loading {ModuleName} profile");
                }
            }
            catch (Exception e)
            {
                // Failed to load the configuration.
                Logger.LogError($"Exception encountered when loading {ModuleName} profile", e);
                success = false;
            }

            if (!profileExists)
            {
                Logger.LogInfo($"Couldn't load {ModuleName} profile because it doesn't exist");
            }
            else if (!success)
            {
                Logger.LogError($"Couldn't load {ModuleName} profile");
            }

            return success;
        }
    }
}
