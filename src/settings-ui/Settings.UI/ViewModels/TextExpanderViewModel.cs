// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class TextExpanderViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private static readonly string[] SnippetFileNames = { TextExpanderSnippetFileNames.Current, TextExpanderSnippetFileNames.Legacy };

        private readonly TextExpanderSettings _settings;
        private readonly SettingsUtils _settingsUtils;
        private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
        private readonly bool _enabledStateIsGPOConfigured;
        private readonly bool _isElevated;
        private readonly Func<string, Task<string>> _pickSingleFolderDialog;
        private readonly Func<string, string> _getResourceString;
        private readonly ITextExpanderStorageEnvironment _storageEnvironment;

        private Func<string, int> SendConfigMSG { get; }

        private TextExpanderVariable[] _variables;
        private TextExpanderSnippetFile _file;
        private string _snippetError;
        private byte[] _loadedBytes;
        private string _newline = Environment.NewLine;
        private System.Text.Encoding _encoding = new System.Text.UTF8Encoding(false, false);
        private bool _hadFinalNewline = true;
        private bool _isEnabled;
        private string _searchText = string.Empty;
        private double _keycapColumnWidth = TextExpanderKeycapLayout.MinWidth;
        private Action<Action> _dispatch;
        private IDisposable _snippetFileWatch;
        private string _snippetFileWatchFolder;

        /// <param name="pickSingleFolderDialog">Shows a folder picker, opening at the folder it's given when that exists, and returns the folder picked, or null if cancelled.</param>
        public TextExpanderViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<string, Task<string>> pickSingleFolderDialog, bool isElevated, Func<string, string> getResourceString = null, ITextExpanderStorageEnvironment storageEnvironment = null)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _settingsRepository = settingsRepository;
            _isElevated = isElevated;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            SendConfigMSG = ipcMSGCallBackFunc;
            _pickSingleFolderDialog = pickSingleFolderDialog;
            _getResourceString = getResourceString ?? (key => ResourceLoaderInstance.ResourceLoader.GetString(key));
            _storageEnvironment = storageEnvironment ?? new TextExpanderStorageEnvironment();

            // Group policy wins over anything the user picks here, so read it before the
            // user-facing state and let it clamp the toggle.
            GpoRuleConfigured gpo = GPOWrapper.GetConfiguredTextExpanderEnabledValue();
            if (gpo == GpoRuleConfigured.Disabled || gpo == GpoRuleConfigured.Enabled)
            {
                _enabledStateIsGPOConfigured = true;
                _isEnabled = gpo == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.TextExpander;
            }

            // Deliberately not wrapped in a catch-all. GetSettingsOrDefault already handles the
            // cases a user can cause — a missing file, or one corrupted into invalid JSON — by
            // returning defaults. Anything else is a programming error on our side, and the one
            // that actually happened was exactly that: TextExpanderSettings was missing from
            // SettingsSerializationContext, so every load threw InvalidOperationException,
            // which GetSettingsOrDefault does not catch. A catch-all here swallowed it and the
            // page silently showed defaults while the engine ran on the real file. Failing
            // loudly is worth far more than a page that loads and lies.
            _settings = _settingsUtils.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName);

            // "Keep the space you typed" is no longer exposed; the typed delimiter is always kept.
            if (!_settings.Properties.WordBoundaryKeepsSpace)
            {
                _settings.Properties.WordBoundaryKeepsSpace = true;
                Save();
            }

            LoadSnippets();
        }

        /// <summary>
        /// Gets a one-line description of where the snippet file is kept and how many snippets it
        /// has, shown on the collapsed "Save snippets to a folder" setting.
        /// </summary>
        public string SnippetFileSummary
        {
            get
            {
                if (!CanEditSnippets)
                {
                    return _getResourceString("TextExpander_SnippetFile_SummaryElevated");
                }

                string key = IsDefaultSnippetsFile() ? "TextExpander_SnippetFile_SummaryDefault" : "TextExpander_SnippetFile_SummaryCustom";
                if (HasSnippetError)
                {
                    return _getResourceString(key + "CountUnavailable");
                }

                int count = Snippets.Count;
                return string.Format(CultureInfo.CurrentCulture, _getResourceString(count == 1 ? key + "_One" : key), count);
            }
        }

        public bool IsEnabledGpoConfigured => _enabledStateIsGPOConfigured;

        public bool IsElevated => _isElevated;

        public bool CanEditSnippets => !_isElevated;

        public bool IsSnippetEditingAvailable => CanEditSnippets && IsEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // Group policy decides this; the UI must not pretend otherwise.
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.TextExpander = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(IsSnippetEditingAvailable));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        /// <summary>
        /// Gets or sets how a replacement reaches the focused window. Index maps to
        /// auto / clipboard / type, matching <c>InjectionPolicy.ParseBackend</c> in the engine.
        /// </summary>
        public int InjectionBackendIndex
        {
            get => _settings.Properties.InjectionBackend.Value switch
            {
                "clipboard" => 1,
                "type" => 2,
                _ => 0,
            };

            set
            {
                string backend = value switch
                {
                    1 => "clipboard",
                    2 => "type",
                    _ => "auto",
                };

                if (_settings.Properties.InjectionBackend.Value != backend)
                {
                    _settings.Properties.InjectionBackend.Value = backend;
                    OnPropertyChanged(nameof(InjectionBackendIndex));
                    OnPropertyChanged(nameof(IsClipboardThresholdEnabled));
                    Save();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether Minimum length to paste applies. Only Automatic chooses
        /// between pasting and typing by length; Always paste and Always type ignore it.
        /// </summary>
        public bool IsClipboardThresholdEnabled => InjectionBackendIndex == 0;

        /// <summary>
        /// Gets or sets the length at which a replacement is pasted rather than typed. Kept at or
        /// above one: zero would mean pasting an empty replacement, and "never paste" is spelled
        /// by choosing the typing backend instead.
        /// </summary>
        public int ClipboardThresholdChars
        {
            get => _settings.Properties.ClipboardThresholdChars.Value;
            set
            {
                int clamped = Math.Max(1, value);
                if (_settings.Properties.ClipboardThresholdChars.Value != clamped)
                {
                    _settings.Properties.ClipboardThresholdChars.Value = clamped;
                    OnPropertyChanged(nameof(ClipboardThresholdChars));
                    Save();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether a Keyboard Manager remap counts as the user typing.
        ///
        /// On by default: the remapped character is the one the user meant and the only one
        /// anything downstream sees, so ignoring it would silently stop snippets matching for
        /// exactly the people who remap keys.
        /// </summary>
        public bool TreatRemapsAsTyping
        {
            get => _settings.Properties.TreatRemapsAsTyping;
            set
            {
                if (_settings.Properties.TreatRemapsAsTyping != value)
                {
                    _settings.Properties.TreatRemapsAsTyping = value;
                    OnPropertyChanged(nameof(TreatRemapsAsTyping));
                    Save();
                }
            }
        }

        /// <summary>Gets or sets whether uppercase and lowercase trigger letters are distinct.</summary>
        public bool CaseSensitive
        {
            get => _settings.Properties.CaseSensitive;
            set
            {
                if (_settings.Properties.CaseSensitive != value)
                {
                    _settings.Properties.CaseSensitive = value;
                    OnPropertyChanged(nameof(CaseSensitive));
                    ApplyCaseSensitivityToRows();
                    DescribeSnippets();
                    Save();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether a trigger waits for a word terminator before firing.
        ///
        /// Off by default: immediate expansion is the point of the tool. On, it stops a trigger
        /// going off inside a longer word — "btw" no longer fires while typing "abtward".
        /// </summary>
        public bool RequireWordBoundary
        {
            get => _settings.Properties.RequireWordBoundary;
            set
            {
                if (_settings.Properties.RequireWordBoundary != value)
                {
                    _settings.Properties.RequireWordBoundary = value;
                    OnPropertyChanged(nameof(RequireWordBoundary));
                    Save();

                    // Which triggers can never expand depends on this.
                    DescribeSnippets();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the space that released a trigger is given back after the
        /// replacement. On by default; off for snippets expanding to an email address or a code,
        /// where a trailing space is wrong rather than untidy.
        /// </summary>
        public bool WordBoundaryKeepsSpace
        {
            get => _settings.Properties.WordBoundaryKeepsSpace;
            set
            {
                if (_settings.Properties.WordBoundaryKeepsSpace != value)
                {
                    _settings.Properties.WordBoundaryKeepsSpace = value;
                    OnPropertyChanged(nameof(WordBoundaryKeepsSpace));
                    Save();
                }
            }
        }

        /// <summary>Gets or sets the folder holding the snippet library. Empty lets the engine resolve it.</summary>
        public string SnippetsPath
        {
            get => _settings.Properties.SnippetsPath.Value;
            set
            {
                if (!CanEditSnippets)
                {
                    return;
                }

                string path = value ?? string.Empty;
                if (_settings.Properties.SnippetsPath.Value != path)
                {
                    _settings.Properties.SnippetsPath.Value = path;
                    OnPropertyChanged(nameof(SnippetsPath));
                    OnPropertyChanged(nameof(SnippetsFolderPath));
                    Save();

                    // The folder moved, so the list on screen is now showing the wrong file.
                    LoadSnippets();
                }
            }
        }

        public string SnippetsFolderPath => CanEditSnippets ? Path.GetDirectoryName(ResolveSnippetsFile()) ?? string.Empty : string.Empty;

        public bool CanClearSnippets => CanEditSnippets && !HasSnippetError && Snippets.Count > 0;

        /// <summary>Lets the user pick a folder, then inspects it without changing the active library.</summary>
        public async Task<TextExpanderFolderChange> PickSnippetsFolderAsync()
        {
            if (!CanEditSnippets || _pickSingleFolderDialog is null)
            {
                return null;
            }

            try
            {
                string picked = await _pickSingleFolderDialog(SnippetsFolderPath);
                return string.IsNullOrWhiteSpace(picked) ? null : PrepareFolderChange(picked);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Logger.LogError("Error picking the Text Expander snippet folder.", ex);
                _snippetError = ex.Message;
                RaiseSnippetListChanged();
                return null;
            }
        }

        internal TextExpanderFolderChange PrepareFolderChange(string folder)
        {
            string full = Path.GetFullPath(_storageEnvironment.ExpandEnvironmentVariables(folder.Trim().Trim('"')));
            string file = TextExpanderSnippetFileNames.ResolveInFolder(full, _storageEnvironment.FileExists);
            return string.Equals(file, ResolveSnippetsFile(), StringComparison.OrdinalIgnoreCase)
                ? null
                : new TextExpanderFolderChange(full, file, _storageEnvironment.FileExists(file));
        }

        /// <summary>
        /// Switches the active folder. An existing destination is never overwritten without a
        /// backup; choosing its file also backs up the file this PC was using before the switch.
        /// </summary>
        public string ApplyFolderChange(TextExpanderFolderChange change, TextExpanderFolderConflictChoice choice, out string backupPath)
        {
            backupPath = string.Empty;
            if (!CanEditSnippets || change is null)
            {
                return null;
            }

            try
            {
                _storageEnvironment.CreateDirectory(change.FolderPath);
                string current = ResolveSnippetsFile();
                string target = TextExpanderSnippetFileNames.ResolveInFolder(change.FolderPath, _storageEnvironment.FileExists);
                bool targetExists = _storageEnvironment.FileExists(target);
                if (targetExists && !change.HasExistingFile)
                {
                    return _getResourceString("TextExpander_SnippetFolder_FileAppeared");
                }

                if (targetExists && choice == TextExpanderFolderConflictChoice.UseFolder)
                {
                    if (_storageEnvironment.FileExists(current))
                    {
                        backupPath = BackupCopy(current);
                    }
                }
                else
                {
                    byte[] bytes = _storageEnvironment.FileExists(current) ? _storageEnvironment.ReadAllBytes(current) : Array.Empty<byte>();
                    if (targetExists)
                    {
                        backupPath = BackupPath(target);
                        WriteAtomically(target, bytes, backupPath, replace: true);
                    }
                    else
                    {
                        target = Path.Combine(change.FolderPath, TextExpanderSnippetFileNames.Current);
                        WriteAtomically(target, bytes, backup: null, replace: false);
                    }
                }

                SnippetsPath = string.Equals(
                    Path.TrimEndingDirectorySeparator(change.FolderPath),
                    Path.TrimEndingDirectorySeparator(_storageEnvironment.DefaultSnippetsFolder),
                    StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : change.FolderPath;
                return null;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Logger.LogError("Error changing the Text Expander snippet folder.", ex);
                return ex.Message;
            }
        }

        /// <summary>Backs up the active file, replaces it with an empty library, and reloads it.</summary>
        public string ClearSnippets(out string backupPath)
        {
            backupPath = string.Empty;
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            try
            {
                string file = ResolveSnippetsFile();
                _storageEnvironment.CreateDirectory(Path.GetDirectoryName(file));
                if (_storageEnvironment.FileExists(file))
                {
                    backupPath = BackupPath(file);
                    WriteAtomically(file, Array.Empty<byte>(), backupPath, replace: true);
                }
                else
                {
                    WriteAtomically(file, Array.Empty<byte>(), backup: null, replace: false);
                }

                LoadSnippets();
                return null;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Logger.LogError("Error clearing the Text Expander snippet file.", ex);
                return ex.Message;
            }
        }

        /// <summary>Gets the snippets shown in the page, in file order.</summary>
        public TextExpanderRowCollection Snippets { get; } = new TextExpanderRowCollection();

        /// <summary>
        /// Gets or sets the width of the list's keycap column. The page measures the triggers it
        /// shows and sets this; every row, including rows loaded later, uses it.
        /// </summary>
        public double KeycapColumnWidth
        {
            get => _keycapColumnWidth;
            set
            {
                if (_keycapColumnWidth == value)
                {
                    return;
                }

                _keycapColumnWidth = value;
                foreach (TextExpanderSnippetViewModel snippet in Snippets)
                {
                    snippet.KeycapColumnWidth = value;
                }

                OnPropertyChanged(nameof(KeycapColumnWidth));
            }
        }

        /// <summary>Gets the rows the search shows, which the list displays.</summary>
        public TextExpanderRowCollection FilteredSnippets { get; } = new TextExpanderRowCollection();

        public int SnippetRevision { get; private set; }

        /// <summary>
        /// Gets or sets how a file watcher's callback reaches the UI thread. The page sets it while
        /// it's shown, and clears it when it closes. While it's set and there is no snippet file
        /// yet, the page watches for one, because the engine writes the starter snippets when it
        /// starts, so they show without opening the page again.
        /// </summary>
        public Action<Action> Dispatch
        {
            get => _dispatch;
            set
            {
                _dispatch = value;
                WatchWhileMissing();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                value ??= string.Empty;
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    RefreshSnippetFilter();
                }
            }
        }

        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

        public bool HasNoMatchingSnippets => CanEditSnippets && !HasSnippetError && Snippets.Count > 0 && FilteredSnippets.Count == 0;

        public string SnippetCountText => !CanEditSnippets || HasSnippetError
            ? _getResourceString("TextExpander_SnippetCountUnavailable")
            : string.Format(
                CultureInfo.CurrentCulture,
                _getResourceString(Snippets.Count == 1 ? "TextExpander_SnippetCount_One" : "TextExpander_SnippetCount"),
                FilteredSnippets.Count,
                Snippets.Count);

        /// <summary>Gets a value indicating whether the snippet file could not be read or written.</summary>
        public bool HasSnippetError => CanEditSnippets && !string.IsNullOrEmpty(_snippetError);

        /// <summary>Gets the reason the snippet file could not be read or written.</summary>
        public string SnippetError => _isElevated ? ElevatedSnippetAccessDeniedMessage() : _snippetError ?? string.Empty;

        /// <summary>Gets a value indicating whether there are no snippets to show.</summary>
        public bool HasNoSnippets => CanEditSnippets && Snippets.Count == 0 && !HasSnippetError;

        /// <summary>Gets the resolved snippet file, shown so the user can find it outside the app.</summary>
        public string SnippetsFilePath => ResolveSnippetsFile();

        /// <summary>Gets the folder used while <see cref="SnippetsPath"/> is empty.</summary>
        public string DefaultSnippetsFolder => CanEditSnippets ? _storageEnvironment.DefaultSnippetsFolder : string.Empty;

        /// <summary>
        /// Gets the variables the engine can expand, for the Insert variable menu.
        ///
        /// <para>
        /// Deliberately only the ones <c>Variables.Expand</c> actually implements, so the UI does not advertise variables the engine would ignore.
        /// The sample is what the value looks like now, so the menu answers "what will I get?"
        /// rather than only naming a token.
        /// </para>
        /// </summary>
        public IReadOnlyList<TextExpanderVariable> Variables => _variables ??=
        [
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Cursor"), "$|$", null),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Date"), "{{date}}", "MM/dd/yy"),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Time"), "{{time}}", "hh:mm tt"),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_DateTime"), "{{datetime}}", "MM/dd/yy hh:mm tt"),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_IsoDate"), "{{isodate}}", "yyyy-MM-dd"),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_CustomDate"), "{{date:dddd}}", "dddd"),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Clipboard"), "{{clipboard}}", null),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Prompt"), "{{prompt:Label}}", null),
        ];

        /// <summary>
        /// Gets <paramref name="variable"/>'s Insert variable menu text, with what it gives at
        /// <paramref name="now"/>. The page asks again each time the menu opens, so the time
        /// and date it shows are never left over from when the page opened.
        /// </summary>
        public string VariableMenuText(TextExpanderVariable variable, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(variable);
            return variable.HasSample
                ? string.Format(CultureInfo.CurrentCulture, _getResourceString("TextExpander_VariableWithSample"), variable.Name, variable.SampleAt(now))
                : variable.Name;
        }

        /// <summary>
        /// Resolves the snippet file the engine is using, applying the same rule the engine does.
        ///
        /// <para>
        /// An unset folder means the engine's host default, which is this module's own settings
        /// folder. That is why the engine derives its default from the settings path the shim
        /// passes it: both sides compute the same answer without a second setting to disagree on.
        /// </para>
        /// </summary>
        private string ResolveSnippetsFile()
        {
            if (!CanEditSnippets)
            {
                return string.Empty;
            }

            string configured = _settings.Properties.SnippetsPath.Value;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                try
                {
                    string expanded = _storageEnvironment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));

                    // Must match the engine's rule exactly. A relative path is resolved against
                    // the *process* working directory, and the engine runs in a different process
                    // from this one -- so "snips" would mean two different folders, and the user
                    // would edit one file while the engine expanded from another with nothing on
                    // screen to say so. Neither side rebases it; both fall back.
                    if (Path.IsPathFullyQualified(expanded))
                    {
                        string full = Path.GetFullPath(expanded);

                        // Accept a file too: someone will paste the path of the snippet file itself.
                        return File.Exists(full) ? full : TextExpanderSnippetFileNames.ResolveInFolder(full, _storageEnvironment.FileExists);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                              or PathTooLongException or IOException
                                              or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    // Fall through to the default; a typo in a path must not leave the page blank.
                }
            }

            // The same folder the engine derives from the settings path the shim passes it, so
            // both sides land on one file without a second setting to disagree about. Within a
            // folder, both pick the file the same way (TextExpanderSnippetFileNames.ResolveInFolder).
            return TextExpanderSnippetFileNames.ResolveInFolder(_storageEnvironment.DefaultSnippetsFolder, _storageEnvironment.FileExists);
        }

        /// <summary>Reads the snippet file into <see cref="Snippets"/>.</summary>
        public void LoadSnippets()
        {
            SnippetRevision++;

            if (!CanEditSnippets)
            {
                _file = null;
                _loadedBytes = null;
                Snippets.ReplaceAll(Array.Empty<TextExpanderSnippetViewModel>());
                _snippetError = null;
                RaiseSnippetListChanged();
                WatchWhileMissing();
                return;
            }

            _snippetError = null;
            var rows = new List<TextExpanderSnippetViewModel>();

            try
            {
                string path = ResolveSnippetsFile();

                if (!File.Exists(path))
                {
                    // Not an error: nothing has been written yet. The engine writes the starter
                    // snippets when it starts and finds no file (see WatchWhileMissing); a save
                    // here first writes just the snippets saved.
                    _file = TextExpanderSnippetFile.Parse(Array.Empty<string>());
                    _loadedBytes = null;
                    _encoding = new System.Text.UTF8Encoding(false, false);
                    _newline = Environment.NewLine;
                    _hadFinalNewline = true;
                }
                else
                {
                    // Read the raw bytes so the file's own conventions survive a save. Reading
                    // only decoded lines would silently re-encode it: an existing BOM would be
                    // dropped and LF endings rewritten to CRLF, which is whole-file churn for
                    // anyone keeping their snippets in git. The encoding is the one the engine
                    // reads it with, so a UTF-16 file is shown, and saved, as UTF-16.
                    _loadedBytes = ReadAllBytesShared(path);
                    string text = TextExpanderSnippetEncoding.Decode(_loadedBytes, out _encoding);

                    _newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
                        : text.Contains('\n', StringComparison.Ordinal) ? "\n"
                        : Environment.NewLine;
                    _hadFinalNewline = text.Length == 0 || text.EndsWith('\n') || text.EndsWith('\r');

                    _file = TextExpanderSnippetFile.Parse(SplitLines(text));
                }

                foreach (TextExpanderSnippetFile.Entry entry in _file.Snippets)
                {
                    rows.Add(new TextExpanderSnippetViewModel(entry.Id, entry.Trigger, entry.Replacement) { KeycapColumnWidth = _keycapColumnWidth });
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                          or System.Security.SecurityException or ArgumentException
                                          or NotSupportedException or PathTooLongException)
            {
                // Say why rather than showing an empty list, which would read as "no snippets"
                // and invite the user to recreate everything on top of a file that still exists.
                _file = null;
                _snippetError = ex.Message;
                rows.Clear();
            }

            Snippets.ReplaceAll(rows);
            RaiseSnippetListChanged();
            WatchWhileMissing();
        }

        /// <summary>
        /// Brings the rows in line with the snippets in memory after a save, without reading the
        /// file again. A row whose snippet didn't change is kept as it is, so one save changes one
        /// row of the list.
        /// </summary>
        private void SyncRows()
        {
            SnippetRevision++;
            var current = new Dictionary<int, TextExpanderSnippetViewModel>(Snippets.Count);
            foreach (TextExpanderSnippetViewModel row in Snippets)
            {
                current[row.Id] = row;
            }

            var rows = new List<TextExpanderSnippetViewModel>(_file.Snippets.Count);
            foreach (TextExpanderSnippetFile.Entry entry in _file.Snippets)
            {
                rows.Add(current.TryGetValue(entry.Id, out TextExpanderSnippetViewModel row)
                        && string.Equals(row.Trigger, entry.Trigger, StringComparison.Ordinal)
                        && string.Equals(row.Replacement, entry.Replacement, StringComparison.Ordinal)
                    ? row
                    : new TextExpanderSnippetViewModel(entry.Id, entry.Trigger, entry.Replacement) { KeycapColumnWidth = _keycapColumnWidth });
            }

            Snippets.SyncTo(rows);
            RaiseSnippetListChanged();
            WatchWhileMissing();
        }

        /// <summary>
        /// Watches the snippet folder while the page is shown and there is no snippet file yet,
        /// and stops once there is one, or the page closes or can't edit snippets.
        /// </summary>
        private void WatchWhileMissing()
        {
            string folder = null;
            if (_dispatch != null && CanEditSnippets && _file != null)
            {
                try
                {
                    string path = ResolveSnippetsFile();
                    folder = File.Exists(path) ? null : Path.GetDirectoryName(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    folder = null;
                }
            }

            if (folder != null && _snippetFileWatch != null && string.Equals(folder, _snippetFileWatchFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _snippetFileWatch?.Dispose();
            _snippetFileWatch = null;
            _snippetFileWatchFolder = folder;
            if (!string.IsNullOrEmpty(folder))
            {
                _snippetFileWatch = _storageEnvironment.WatchForFiles(folder, SnippetFileNames, () => _dispatch?.Invoke(SnippetFileMayHaveAppeared));
            }
        }

        private void SnippetFileMayHaveAppeared()
        {
            // The watcher also polls, so most calls find nothing new.
            if (_snippetFileWatch != null && _file != null && _file.Snippets.Count == 0 && File.Exists(ResolveSnippetsFile()))
            {
                LoadSnippets();
            }
        }

        /// <summary>
        /// Adds or updates a snippet and writes the file. Pass null as <paramref name="id"/> to
        /// add. Returns null on success, or the reason it was refused.
        /// </summary>
        public string SaveSnippet(int? id, string trigger, string replacement)
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            trigger = trigger ?? string.Empty;
            replacement = replacement ?? string.Empty;

            string problem = ValidateSnippet(id, trigger, replacement);
            if (problem != null)
            {
                return problem;
            }

            TextExpanderSnippetFile previousFile = _file;
            _file = previousFile.Clone();
            try
            {
                _file.Upsert(id, trigger, replacement, CaseSensitive);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // Checked above, so what's left is a snippet that's no longer in the file.
                _file = previousFile;
                return _getResourceString("TextExpander_EditorFileChanged");
            }

            return WriteEditedFile(previousFile);
        }

        public string ValidateSnippet(int? id, string trigger, string replacement)
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            if (!TextExpanderSnippetFile.IsValidTrigger(trigger, out TextExpanderSnippetProblem problem))
            {
                return DescribeProblem(problem);
            }

            // A block body cannot contain its own terminator, and there is no escape for it, so
            // this really is unrepresentable rather than merely awkward.
            if (!TextExpanderSnippetFile.IsValidReplacement(replacement, out TextExpanderSnippetProblem replacementProblem))
            {
                return DescribeProblem(replacementProblem);
            }

            if (_file == null)
            {
                return SnippetError;
            }

            TextExpanderSnippetFile.Entry clash = _file.FindDuplicate(trigger, ignoringId: id, CaseSensitive);
            if (clash != null)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    _getResourceString("TextExpander_DuplicateTrigger"),
                    clash.Trigger);
            }

            return null;
        }

        private string DescribeProblem(TextExpanderSnippetProblem problem) => _getResourceString(problem switch
        {
            TextExpanderSnippetProblem.EmptyTrigger => "TextExpander_Problem_EmptyTrigger",
            TextExpanderSnippetProblem.TriggerHasLineBreak => "TextExpander_Problem_TriggerHasLineBreak",
            TextExpanderSnippetProblem.TriggerStartsWithHiddenCharacter => "TextExpander_Problem_TriggerStartsWithHiddenCharacter",
            TextExpanderSnippetProblem.TriggerHasOuterSpace => "TextExpander_Problem_TriggerHasOuterSpace",
            TextExpanderSnippetProblem.TriggerHasEquals => "TextExpander_Problem_TriggerHasEquals",
            TextExpanderSnippetProblem.TriggerEndsWithBlockStart => "TextExpander_Problem_TriggerEndsWithBlockStart",
            TextExpanderSnippetProblem.TriggerStartsWithComment => "TextExpander_Problem_TriggerStartsWithComment",
            TextExpanderSnippetProblem.ReplacementHasBlockEnd => "TextExpander_Problem_ReplacementHasBlockEnd",
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        });

        private void RefreshSnippetFilter()
        {
            string query = SearchText.Trim();
            var matches = new List<TextExpanderSnippetViewModel>(query.Length == 0 ? Snippets.Count : 0);
            foreach (TextExpanderSnippetViewModel snippet in Snippets)
            {
                if (query.Length == 0
                    || snippet.Trigger.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || snippet.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(snippet);
                }
            }

            // One notification for the whole change, or one per row when only a row changed.
            FilteredSnippets.SyncTo(matches);

            OnPropertyChanged(nameof(HasSearchText));
            OnPropertyChanged(nameof(HasNoMatchingSnippets));
            OnPropertyChanged(nameof(SnippetCountText));
        }

        /// <summary>Deletes the snippet with <paramref name="id"/> and writes the file.</summary>
        public string DeleteSnippet(int id)
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            if (_file == null)
            {
                return SnippetError;
            }

            TextExpanderSnippetFile previousFile = _file;
            _file = previousFile.Clone();
            _file.Remove(id);
            return WriteEditedFile(previousFile);
        }

        /// <summary>
        /// Suggests a trigger for a copy of the snippet with <paramref name="id"/>, which the editor
        /// opens with. Null while elevated or when the snippet is gone.
        ///
        /// <para>
        /// Not the trigger plus "2": typing <c>!mysig2</c> completes <c>!mysig</c> first, and the
        /// engine expands as soon as the typed text ends with a trigger, so unless Wait for the whole
        /// word is on, that copy could never fire. The number goes after any leading symbols
        /// instead, as in <c>!2mysig</c>, and is checked by the engine's rules in the current mode.
        /// </para>
        /// </summary>
        public string SuggestDuplicateTrigger(int id)
        {
            if (!CanEditSnippets || _file == null)
            {
                return null;
            }

            TextExpanderSnippetFile.Entry source = _file.Snippets.FirstOrDefault(s => s.Id == id);
            return source == null ? null : SuggestCopyTrigger(source.Trigger, _file.Snippets.Select(s => s.Trigger), RequireWordBoundary, CaseSensitive);
        }

        /// <summary>
        /// Gets a numbered copy of <paramref name="trigger"/> that isn't taken and that, by the
        /// engine's rules in the current mode, can fire and doesn't keep a longer trigger from
        /// firing. The number goes after any leading symbols, and a trigger that is already a copy
        /// counts on: <c>!mysig</c> gives <c>!2mysig</c>, and <c>!2mysig</c> gives <c>!3mysig</c>.
        /// Empty when no number up to 999 works, such as when another trigger fires inside the part
        /// every copy shares.
        /// </summary>
        internal static string SuggestCopyTrigger(string trigger, IEnumerable<string> triggers, bool wordMode, bool caseSensitive = true)
        {
            var taken = new TextExpanderTriggerSet(triggers, caseSensitive);
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            int insertAt = 0;
            while (insertAt < trigger.Length && !char.IsLetterOrDigit(trigger[insertAt]))
            {
                insertAt++;
            }

            if (insertAt == trigger.Length)
            {
                // Only symbols, so a number after them would extend the trigger itself.
                insertAt = 0;
            }

            // A copy made here before already has its number in that place.
            int digits = 0;
            while (insertAt + digits < trigger.Length && char.IsAsciiDigit(trigger[insertAt + digits]))
            {
                digits++;
            }

            int first = 2;
            if (digits is > 0 and <= 3 && trigger[insertAt] != '0')
            {
                first = int.Parse(trigger.AsSpan(insertAt, digits), CultureInfo.InvariantCulture) + 1;
            }
            else
            {
                digits = 0;
            }

            string prefix = trigger[..insertAt];
            string rest = trigger[(insertAt + digits)..];

            // Every copy contains the part after the number, so only these can contain a copy.
            List<string> containingRest = taken.Triggers.Where(other => other.Contains(rest, comparison)).ToList();

            // From the next number to 999, then from 2.
            for (int i = 0; i < 998; i++)
            {
                string number = (2 + ((first - 2 + i) % 998)).ToString(CultureInfo.InvariantCulture);
                string candidate = prefix + number + rest;
                if (taken.Contains(candidate))
                {
                    continue;
                }

                if (TextExpanderTriggerReach.FindBlocker(candidate, taken, wordMode, out int blockedAt) is not null)
                {
                    if (blockedAt >= prefix.Length + number.Length)
                    {
                        // Another trigger fires inside the part every copy shares.
                        return string.Empty;
                    }

                    continue;
                }

                if (!BlocksAnother(candidate, containingRest, taken, wordMode, comparison))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="candidate"/> could be added as a trigger:
        /// it isn't taken and, by the engine's rules in the given mode, it can fire and doesn't keep
        /// another trigger from firing.
        /// </summary>
        internal static bool CanFireAlongside(string candidate, IEnumerable<string> triggers, bool wordMode, bool caseSensitive = true)
        {
            var taken = new TextExpanderTriggerSet(triggers, caseSensitive);
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return !taken.Contains(candidate)
                && TextExpanderTriggerReach.FindBlocker(candidate, taken, wordMode) is null
                && !BlocksAnother(candidate, taken.Triggers.Where(other => other.Contains(candidate, comparison)).ToList(), taken, wordMode, comparison);
        }

        /// <summary>
        /// Gets a value indicating whether adding <paramref name="candidate"/> would make it fire
        /// while one of <paramref name="longer"/> is being typed. <paramref name="taken"/> is
        /// changed only while this runs.
        /// </summary>
        private static bool BlocksAnother(string candidate, List<string> longer, TextExpanderTriggerSet taken, bool wordMode, StringComparison comparison)
        {
            taken.Add(candidate);
            try
            {
                foreach (string other in longer)
                {
                    if (other.Length > candidate.Length
                        && other.Contains(candidate, comparison)
                        && TextExpanderTriggerReach.FindBlocker(other, taken, wordMode) is { } blocker
                        && string.Equals(blocker.Trigger, candidate, comparison))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                taken.Remove(candidate);
            }
        }

        private string WriteEditedFile(TextExpanderSnippetFile previousFile)
        {
            TextExpanderSnippetFile editedFile = _file;
            string error = WriteFile();
            if (error != null && ReferenceEquals(_file, editedFile))
            {
                // A failed write must not reserve a new trigger or lose a deleted row in memory.
                // Keep a freshly reloaded file when WriteFile detected an external edit instead.
                _file = previousFile;
            }

            return error;
        }

        private string WriteFile()
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            try
            {
                string path = ResolveSnippetsFile();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Refuse to clobber a file we did not load as snippets. ResolveSnippetsFile
                // accepts a path to an existing file by design, so a stray snippets_path can aim
                // the editor at something else entirely and the next save would overwrite it.
                if (_loadedBytes == null && File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    LoadSnippets();
                    return _getResourceString("TextExpander_FileAppeared");
                }

                // Someone else changed the file since it was loaded. Rewriting our snapshot would
                // silently throw their edit away, so stop and reload instead.
                if (_loadedBytes != null && File.Exists(path))
                {
                    byte[] current = ReadAllBytesShared(path);
                    if (!current.AsSpan().SequenceEqual(_loadedBytes))
                    {
                        LoadSnippets();
                        return _getResourceString("TextExpander_FileChangedBeforeSave");
                    }
                }

                // Preserve the file's own encoding and line endings rather than imposing ours.
                string text = string.Join(_newline ?? Environment.NewLine, _file.ToLines());
                if (_hadFinalNewline)
                {
                    text += _newline ?? Environment.NewLine;
                }

                byte[] payload = TextExpanderSnippetEncoding.Encode(text, _encoding);

                // Write a temp file and swap it in, so a reader can never observe the truncated
                // window that File.WriteAllText leaves between truncate and write. The engine
                // watches this file and reads it with permissive sharing, and a partial read
                // parses perfectly well - it just yields fewer snippets, with nothing to signal
                // that anything went wrong.
                string temp = path + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
                File.WriteAllBytes(temp, payload);

                if (File.Exists(path))
                {
                    File.Replace(temp, path, null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temp, path);
                }

                // The file now holds exactly what was written, so there is nothing to read back.
                _loadedBytes = payload;
                _snippetError = null;
                SyncRows();
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                          or System.Security.SecurityException or ArgumentException
                                          or NotSupportedException or PathTooLongException)
            {
                _snippetError = ex.Message;
                RaiseSnippetListChanged();
                return ex.Message;
            }
        }

        /// <summary>Reads with permissive sharing so the engine's watcher does not block the page.</summary>
        private static byte[] ReadAllBytesShared(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>
        /// Splits on CRLF, LF and lone CR, matching what StreamReader.ReadLine would produce.
        /// A lone CR really is a line break on read, which is why the model refuses to emit one
        /// inside a single-line entry.
        /// </summary>
        internal static string[] SplitLines(string text)
        {
            var lines = new List<string>();
            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n' && text[i] != '\r')
                {
                    continue;
                }

                lines.Add(text.Substring(start, i - start));

                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }

            if (start < text.Length)
            {
                lines.Add(text.Substring(start));
            }

            return lines.ToArray();
        }

        private string ElevatedSnippetAccessDeniedMessage() => _getResourceString("TextExpander_ElevatedSnippetAccessDenied");

        private string BackupCopy(string path)
        {
            string backup = BackupPath(path);
            _storageEnvironment.WriteNewFile(backup, _storageEnvironment.ReadAllBytes(path));
            return backup;
        }

        private string BackupPath(string path)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string candidate = $"{path}.backup-{stamp}";
            for (int suffix = 2; _storageEnvironment.FileExists(candidate); suffix++)
            {
                candidate = $"{path}.backup-{stamp}-{suffix}";
            }

            return candidate;
        }

        private void WriteAtomically(string path, byte[] bytes, string backup, bool replace)
        {
            string folder = Path.GetDirectoryName(path);
            _storageEnvironment.CreateDirectory(folder);

            string temp = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                _storageEnvironment.WriteNewFile(temp, bytes);
                if (replace)
                {
                    _storageEnvironment.ReplaceFile(temp, path, backup);
                }
                else
                {
                    _storageEnvironment.MoveFile(temp, path);
                }
            }
            finally
            {
                try
                {
                    if (_storageEnvironment.FileExists(temp))
                    {
                        _storageEnvironment.DeleteFile(temp);
                    }
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    Logger.LogWarning($"Could not delete temporary Text Expander file '{temp}': {ex.Message}");
                }
            }
        }

        private static bool IsFileError(Exception ex)
            => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                or ArgumentException or NotSupportedException or PathTooLongException;

        /// <summary>Gets a value indicating whether the engine's own folder holds the snippet file in use.</summary>
        private bool IsDefaultSnippetsFile()
        {
            string folder = Path.GetDirectoryName(ResolveSnippetsFile()) ?? string.Empty;
            return string.Equals(
                Path.TrimEndingDirectorySeparator(folder),
                Path.TrimEndingDirectorySeparator(_storageEnvironment.DefaultSnippetsFolder),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Describes every row: what it does, and whether it can ever expand. That depends on the
        /// other triggers and on Wait for the whole word, so it is worked out for the whole list.
        /// </summary>
        private void DescribeSnippets()
        {
            var triggers = new TextExpanderTriggerSet(caseSensitive: CaseSensitive);
            var lastRow = new Dictionary<string, int>(CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            for (int row = 0; row < Snippets.Count; row++)
            {
                triggers.Add(Snippets[row].Trigger);
                lastRow[Snippets[row].Trigger] = row;
            }

            DateTime now = DateTime.Now;
            bool wordMode = RequireWordBoundary;
            for (int row = 0; row < Snippets.Count; row++)
            {
                TextExpanderSnippetViewModel snippet = Snippets[row];

                // The engine keeps the last of two rows with the same trigger.
                bool hasLaterDuplicate = lastRow[snippet.Trigger] > row;
                snippet.Description = TextExpanderRowDescriber.Describe(snippet.Trigger, snippet.Replacement, hasLaterDuplicate, triggers, wordMode, now, _getResourceString);
            }
        }

        private void ApplyCaseSensitivityToRows()
        {
            foreach (TextExpanderSnippetViewModel snippet in Snippets)
            {
                snippet.CaseSensitive = CaseSensitive;
                snippet.CaseVariantAutomationName = snippet.ShowsCaseVariants
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        _getResourceString("TextExpander_Row_CaseInsensitiveAutomation"),
                        snippet.Trigger.ToLowerInvariant(),
                        snippet.Trigger.ToUpperInvariant())
                    : string.Empty;
            }
        }

        private void RaiseSnippetListChanged()
        {
            ApplyCaseSensitivityToRows();
            DescribeSnippets();
            RefreshSnippetFilter();
            OnPropertyChanged(nameof(HasNoSnippets));
            OnPropertyChanged(nameof(HasSnippetError));
            OnPropertyChanged(nameof(SnippetError));
            OnPropertyChanged(nameof(SnippetsFilePath));
            OnPropertyChanged(nameof(SnippetsFolderPath));
            OnPropertyChanged(nameof(CanClearSnippets));

            OnPropertyChanged(nameof(SnippetFileSummary));
        }

        private void Save()
        {
            // The engine watches this file, so a save is what applies the change -- no restart.
            _settingsUtils.SaveSettings(_settings.ToJsonString(), TextExpanderSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            if (_enabledStateIsGPOConfigured)
            {
                return;
            }

            GeneralSettingsConfig = _settingsRepository.SettingsConfig;
            _isEnabled = GeneralSettingsConfig.Enabled.TextExpander;
            OnPropertyChanged(nameof(IsEnabled));
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
