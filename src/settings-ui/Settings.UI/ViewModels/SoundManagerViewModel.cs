// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class SoundManagerViewModel : PageViewModelBase
    {
        // How long the runner is given to answer whether a shortcut is already taken.
        private static readonly TimeSpan ConflictTimeout = TimeSpan.FromSeconds(2);

        // The icon of the Sound Manager itself, used when the icon of a program cannot be found.
        private static readonly BitmapImage ModuleIcon = new()
        {
            UriSource = new Uri("ms-appx:///Assets/Settings/Icons/SoundManager.png"),
        };

        // The numbers Windows keeps the choices of the ducking setting under.
        private const int DuckingMuteOthers = 0;
        private const int DuckingLowerBy80 = 1;
        private const int DuckingLowerBy50 = 2;
        private const int DuckingDoNothing = 3;

        private bool _duckingEnabled = true;
        private int _duckingAmount = DuckingLowerBy80;

        protected override string ModuleName => SoundManagerSettings.ModuleName;

        private SettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private SoundManagerSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private readonly SoundManagerPipeClient _pipeClient = new();

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public SoundManagerViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<SoundManagerSettings> soundManagerSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            SettingsUtils = settingsUtils;

            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            ArgumentNullException.ThrowIfNull(soundManagerSettingsRepository);

            Settings = soundManagerSettingsRepository.SettingsConfig;

            // A settings file that was written before the shortcuts existed has none of them.
            Settings.Properties.MuteShortcuts ??= new SoundManagerMuteShortcuts();

            foreach (var muteShortcut in MuteShortcuts)
            {
                muteShortcut.PropertyChanged += OnMuteShortcutPropertyChanged;
                MuteShortcutItems.Add(new SoundManagerMuteShortcutItem(muteShortcut));
            }

            SendConfigMSG = ipcMSGCallBackFunc;

            DuckingAmountChoices = new List<SoundManagerDuckingChoice>
            {
                new SoundManagerDuckingChoice(ResourceLoaderInstance.ResourceLoader.GetString("SoundManager_Ducking_MuteOthers"), DuckingMuteOthers),
                new SoundManagerDuckingChoice(ResourceLoaderInstance.ResourceLoader.GetString("SoundManager_Ducking_LowerBy80"), DuckingLowerBy80),
                new SoundManagerDuckingChoice(ResourceLoaderInstance.ResourceLoader.GetString("SoundManager_Ducking_LowerBy50"), DuckingLowerBy50),
            };
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = ModuleGpoHelper.GetModuleGpoConfiguration(ModuleType.SoundManager);
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.SoundManager;
            }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            // The number of a shortcut is its place in this list. The one that opens the window comes first.
            var hotkeys = new List<HotkeySettings> { ActivationShortcut };
            hotkeys.AddRange(MuteShortcuts.Select(muteShortcut => muteShortcut.Shortcut));

            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = hotkeys.ToArray(),
            };

            return hotkeysDict;
        }

        public ObservableCollection<SoundManagerMuteShortcut> MuteShortcuts => Settings.Properties.MuteShortcuts.Value;

        // The settings cannot hold the icon of a program, so the rows are kept apart from them.
        public ObservableCollection<SoundManagerMuteShortcutItem> MuteShortcutItems { get; } = new();

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
                    GeneralSettingsConfig.Enabled.SoundManager = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public HotkeySettings ActivationShortcut
        {
            get => Settings.Properties.ActivationShortcut;

            set
            {
                if (Settings.Properties.ActivationShortcut != value)
                {
                    Settings.Properties.ActivationShortcut = value ?? Settings.Properties.DefaultActivationShortcut;

                    NotifyPropertyChanged();

                    SendConfigToRunner();
                }
            }
        }

        // Asks the module interface which programs are making sound.
        public Task<List<SoundManagerProgram>> GetProgramsAsync(CancellationToken cancellationToken)
        {
            return _pipeClient.GetProgramsAsync(App.PowerToysPID, cancellationToken);
        }

        // The ways in which Windows can hold the other sounds down.
        public IReadOnlyList<SoundManagerDuckingChoice> DuckingAmountChoices { get; }

        // Whether Windows holds the other sounds down while a program is talking.
        public bool DuckingEnabled
        {
            get => _duckingEnabled;
            set
            {
                if (_duckingEnabled != value)
                {
                    _duckingEnabled = value;
                    OnPropertyChanged(nameof(DuckingEnabled));
                    _ = ApplyDuckingModeAsync();
                }
            }
        }

        // The number Windows keeps the choice under.
        public int DuckingAmount
        {
            get => _duckingAmount;
            set
            {
                if (_duckingAmount != value)
                {
                    _duckingAmount = value;
                    OnPropertyChanged(nameof(DuckingAmount));
                    _ = ApplyDuckingModeAsync();
                }
            }
        }

        // Windows keeps the setting, so it is asked what it does instead of remembering it here.
        public async Task LoadDuckingAsync()
        {
            var mode = await _pipeClient.GetDuckingModeAsync(App.PowerToysPID, CancellationToken.None);
            if (mode < 0)
            {
                // The module interface is not there, so what is on the screen stays.
                return;
            }

            if (mode == DuckingDoNothing)
            {
                _duckingEnabled = false;
            }
            else
            {
                _duckingEnabled = true;
                _duckingAmount = mode;
            }

            OnPropertyChanged(nameof(DuckingEnabled));
            OnPropertyChanged(nameof(DuckingAmount));
        }

        // Tells Windows what to do with the other sounds.
        private async Task ApplyDuckingModeAsync()
        {
            var mode = _duckingEnabled ? _duckingAmount : DuckingDoNothing;

            var ok = await _pipeClient.SetDuckingModeAsync(App.PowerToysPID, mode, CancellationToken.None);
            if (ok)
            {
                return;
            }

            // Windows did not take it, so what is on the screen is put back to what Windows says.
            await LoadDuckingAsync();
        }

        // The shortcut that is picked is one that is not taken by anything else.
        public async Task AddMuteShortcutAsync(SoundManagerProgram program)
        {
            if (string.IsNullOrEmpty(program.Program))
            {
                return;
            }

            if (MuteShortcuts.Any(muteShortcut => string.Equals(muteShortcut.Program, program.Program, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var muteShortcut = new SoundManagerMuteShortcut
            {
                Program = program.Program,
                Name = program.Name,
                ProcessPath = program.ProcessPath,
                Shortcut = await PickShortcutAsync(program.Name),
            };

            muteShortcut.PropertyChanged += OnMuteShortcutPropertyChanged;

            MuteShortcuts.Add(muteShortcut);

            var item = new SoundManagerMuteShortcutItem(muteShortcut);
            MuteShortcutItems.Add(item);

            SaveSettings();

            SendConfigToRunner();

            await LoadIconAsync(item);
        }

        public void RemoveMuteShortcut(SoundManagerMuteShortcutItem item)
        {
            var muteShortcut = item.MuteShortcut;

            muteShortcut.PropertyChanged -= OnMuteShortcutPropertyChanged;

            MuteShortcuts.Remove(muteShortcut);
            MuteShortcutItems.Remove(item);

            SaveSettings();

            SendConfigToRunner();
        }

        // The icon is read out of the file of the program, which is remembered when the shortcut is made.
        public async Task LoadIconsAsync()
        {
            foreach (var item in MuteShortcutItems)
            {
                await LoadIconAsync(item);
            }
        }

        private async Task LoadIconAsync(SoundManagerMuteShortcutItem item)
        {
            var path = item.ProcessPath;

            if (string.IsNullOrEmpty(path))
            {
                // The shortcut was made before the path of the program was remembered.
                path = FindRunningProgram(item.Program);
            }

            var image = await IconLoader.LoadAsync(path);

            item.Icon = image != null
                ? new ImageIcon { Source = image }
                : new ImageIcon { Source = ModuleIcon };
        }

        // Looks for the file of a program that is running, by the name of its executable file.
        private static string FindRunningProgram(string program)
        {
            var name = Path.GetFileNameWithoutExtension(program);
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                    catch (Exception)
                    {
                        // The file of this process cannot be read, so the next one is tried.
                    }
                }
            }

            return string.Empty;
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            SaveSettings();
        }

        // Writes the settings of the module to the settings file.
        private void SaveSettings()
        {
            SettingsUtils.SaveSettings(Settings.ToJsonString(), SoundManagerSettings.ModuleName);
        }

        private void Launch()
        {
            SendCustomAction("open-sound-manager");
        }

        // Tells the runner to do something for the module, in the way the other modules ask for it.
        private void SendCustomAction(string actionName)
        {
            SendConfigMSG("{\"action\":{\"" + SoundManagerSettings.ModuleName + "\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private void OnMuteShortcutPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoundManagerMuteShortcut.Shortcut))
            {
                SaveSettings();

                SendConfigToRunner();
            }
        }

        // The letters of the name of the program are tried first, so that the shortcut is easy to remember.
        private async Task<HotkeySettings> PickShortcutAsync(string programName)
        {
            var letters = GetCandidateLetters(programName);

            foreach (var letter in letters)
            {
                var candidate = CreateShortcut(letter);
                if (IsTakenLocally(candidate))
                {
                    continue;
                }

                if (!await HasConflictAsync(candidate))
                {
                    return candidate;
                }
            }

            // Everything was taken, so the first letter is used and the conflict is shown.
            return CreateShortcut(letters[0]);
        }

        // The shortcut of a program is the Windows, Control and Shift keys with one letter.
        private static HotkeySettings CreateShortcut(int code)
        {
            return new HotkeySettings(true, true, false, true, code);
        }

        // The letters that a shortcut can be made of, the ones of the name of the program first.
        private static List<int> GetCandidateLetters(string programName)
        {
            var letters = new List<int>();

            if (!string.IsNullOrEmpty(programName))
            {
                foreach (var character in programName)
                {
                    var letter = char.ToUpperInvariant(character);

                    // Only the Latin letters are keys that Windows has.
                    if (letter < 'A' || letter > 'Z')
                    {
                        continue;
                    }

                    if (!letters.Contains(letter))
                    {
                        letters.Add(letter);
                    }
                }
            }

            for (var letter = 'A'; letter <= 'Z'; letter++)
            {
                if (!letters.Contains(letter))
                {
                    letters.Add(letter);
                }
            }

            return letters;
        }

        private bool IsTakenLocally(HotkeySettings candidate)
        {
            if (IsSameShortcut(candidate, ActivationShortcut))
            {
                return true;
            }

            return MuteShortcuts.Any(muteShortcut => IsSameShortcut(candidate, muteShortcut.Shortcut));
        }

        private static bool IsSameShortcut(HotkeySettings first, HotkeySettings second)
        {
            return first.Win == second.Win &&
                   first.Ctrl == second.Ctrl &&
                   first.Shift == second.Shift &&
                   first.Alt == second.Alt &&
                   first.Code == second.Code;
        }

        // Asks the runner whether a shortcut is already taken by another module of PowerToys or by Windows itself.
        private async Task<bool> HasConflictAsync(HotkeySettings hotkey)
        {
            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var requestId = HotkeyConflictHelper.CheckHotkeyConflict(
                hotkey,
                SendConfigMSG,
                (hasConflict, _) => answer.TrySetResult(hasConflict));

            if (string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            try
            {
                var finished = await Task.WhenAny(answer.Task, Task.Delay(ConflictTimeout));
                if (finished != answer.Task)
                {
                    return false;
                }

                return await answer.Task;
            }
            finally
            {
                HotkeyConflictHelper.CancelHotkeyConflictCheck(requestId);
            }
        }

        // Tells the runner about the settings, so that the module can register the shortcuts.
        private void SendConfigToRunner()
        {
            SendConfigMSG(
                string.Format(
                CultureInfo.InvariantCulture,
                "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                SoundManagerSettings.ModuleName,
                JsonSerializer.Serialize(Settings, SettingsSerializationContext.Default.SoundManagerSettings)));
        }
    }
}
