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
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Storage;

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    public sealed partial class TextExpanderViewModel : Observable, IDisposable
    {
        // About as many characters as fit on one line of the snippets file flyout and of the error bar.
        private const int FilePathDisplayLength = 69;

        // The Windows error when no app is set to open a file's type.
        private const int ErrorNoAssociation = 1155;

        private readonly TextExpanderSettingsUtils _settingsUtils;
        private readonly bool _isElevated;
        private readonly Func<string, string> _getResourceString;
        private readonly ITextExpanderStorageEnvironment _storageEnvironment;
        private readonly Action<string> _openFile;

        private TextExpanderSettings _settings;
        private TextExpanderVariable[] _variables;
        private SnippetFile _file;
        private string _snippetError;
        private string _unavailableFolder;
        private bool _snippetsFileMissing;
        private string _loadedPath;
        private byte[] _loadedBytes;

        // Whether a save writes the loaded file back exactly, so it needs no backup. Worked out at the first save after a
        // load, not at every save, since SavesAsIs reads and writes the whole file.
        private bool? _loadedSavesAsIs;
        private string _searchText = string.Empty;
        private Action<Action> _dispatch;
        private IDisposable _snippetFileWatch;
        private string _snippetFileWatchPath;
        private long _snippetFileWatchGeneration;
        private int _snippetFileCheckQueued;
        private IDisposable _settingsWatch;

        private enum PersistedFolderSettingStatus
        {
            Previous,
            New,
            Other,
            Unreadable,
        }

        private readonly record struct PersistedFolderSetting(
            PersistedFolderSettingStatus Status,
            string SnippetsPath,
            Exception Error);

        public TextExpanderViewModel(TextExpanderSettingsUtils settingsUtils, bool isElevated, Func<string, string> getResourceString = null, ITextExpanderStorageEnvironment storageEnvironment = null, Action<string> openFile = null)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _isElevated = isElevated;
            _getResourceString = getResourceString ?? (key => ResourceLoaderInstance.ResourceLoader.GetString(key));
            _storageEnvironment = storageEnvironment ?? new TextExpanderStorageEnvironment();
            _openFile = openFile ?? OpenFileWithDefaultApplication;

            // Not wrapped in a catch-all: GetSettingsOrDefault already handles a missing or corrupt file, and
            // anything else is a bug that must fail loudly rather than show defaults.
            _settings = _settingsUtils.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName);

            // "Keep the space you typed" is no longer exposed; this is the one place that clears a false
            // saved by earlier builds, which the engine still honors.
            if (!_settings.Properties.WordBoundaryKeepsSpace)
            {
                Save(properties => properties.WordBoundaryKeepsSpace = true);
            }

            LoadSnippets();
        }

        /// <summary>
        /// Gets the first line of the snippets file menu: where the file is kept and how many snippets it has.
        /// </summary>
        public string SnippetFileSummary
        {
            get
            {
                if (!CanEditSnippets)
                {
                    return _getResourceString("TextExpander_SnippetFile_SummaryElevated");
                }

                if (HasSnippetError)
                {
                    return SnippetFolderLabel;
                }

                string folder = SnippetsFolderPath;
                string key = "TextExpander_SnippetFile_Summary" + PlaceKey(folder);
                int count = Snippets.Count;
                return string.Format(CultureInfo.CurrentCulture, _getResourceString(count == 1 ? key + "_One" : key), count, TextExpanderFolderText.FolderName(folder));
            }
        }

        /// <summary>
        /// Gets where the snippets file is kept, without the count, for the button that opens the snippets file menu: on
        /// this PC (the default location), in OneDrive, or in the folder the file is in, by that folder's name.
        /// </summary>
        public string SnippetFolderLabel
        {
            get
            {
                if (!CanEditSnippets)
                {
                    return _getResourceString("TextExpander_SnippetFile_SummaryElevated");
                }

                string folder = SnippetsFolderPath;
                return string.Format(
                    CultureInfo.CurrentCulture,
                    _getResourceString("TextExpander_SnippetFile_Summary" + PlaceKey(folder) + "CountUnavailable"),
                    TextExpanderFolderText.FolderName(folder));
            }
        }

        public bool IsElevated => _isElevated;

        public bool CanEditSnippets => !_isElevated;

        /// <summary>Gets a value indicating whether uppercase and lowercase trigger letters are distinct. Settings owns this option.</summary>
        public bool CaseSensitive => _settings.Properties.CaseSensitive;

        /// <summary>
        /// Gets a value indicating whether a trigger waits for a word terminator before firing.
        /// Settings owns this option: on by default, and an explicitly saved false value remains false.
        /// </summary>
        public bool RequireWordBoundary => _settings.Properties.RequireWordBoundary;

        /// <summary>
        /// Gets or sets the snippets file the user chose. Empty uses the file in this PC's own folder, the default location.
        /// </summary>
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
                    Save(properties => properties.SnippetsPath.Value = path);

                    // The file changed, so the list on screen is now showing the wrong one.
                    LoadSnippets();
                }
            }
        }

        public string SnippetsFolderPath => CanEditSnippets ? Path.GetDirectoryName(SnippetsFilePath) ?? string.Empty : string.Empty;

        /// <summary>
        /// Gets <see cref="SnippetsFilePath"/> on one line, with folders in the middle replaced by "…". The full path stays in
        /// <see cref="SnippetsFilePath"/> for its tooltip and for copying.
        /// </summary>
        public string SnippetsFileDisplayPath => TextExpanderFolderText.TrimMiddle(SnippetsFilePath, FilePathDisplayLength);

        /// <summary>
        /// Gets a value indicating whether the snippets file is inside OneDrive, which picks the sync advice: what to do on
        /// the other PCs when it is, and two numbered steps to get there when it isn't.
        /// </summary>
        public bool IsSnippetsFileInOneDrive => CanEditSnippets && IsInOneDrive(SnippetsFolderPath);

        /// <summary>Gets the sync advice's heading, shown bold.</summary>
        public string SyncHeading => _getResourceString(IsSnippetsFileInOneDrive
            ? "TextExpander_SnippetFile_SyncHeadingOneDrive"
            : "TextExpander_SnippetFile_SyncHeading");

        /// <summary>
        /// Gets the sync advice under <see cref="SyncHeading"/>: two steps, shown as a numbered list, or for a file inside
        /// OneDrive one sentence. Each names a button, which is shown bold.
        /// </summary>
        public IReadOnlyList<TextExpanderSyncStep> SyncSteps => IsSnippetsFileInOneDrive
            ? new[] { SyncStep("TextExpander_SnippetFile_SyncStepOneDrive", "TextExpander_SnippetFile_Use/Content") }
            : new[]
            {
                SyncStep("TextExpander_SnippetFile_SyncStepMove", "TextExpander_SnippetFile_Move/Content"),
                SyncStep("TextExpander_SnippetFile_SyncStepUse", "TextExpander_SnippetFile_Use/Content"),
            };

        /// <summary>
        /// Gets the folder the Save and Open dialogs open in: the snippets file's folder, or null to open in Documents while
        /// the snippets are in this PC's own folder, which is hidden.
        /// </summary>
        public string SnippetsPickerFolder => !CanEditSnippets || IsDefaultFolder(SnippetsFolderPath) ? null : SnippetsFolderPath;

        /// <summary>
        /// Gets a value indicating whether the file at <paramref name="path"/> has a snippet, so moving the snippets onto it
        /// first asks whether to use its snippets or replace them. One with an empty list, one that isn't a snippets file,
        /// or one that can't be read is replaced without asking, after it's backed up. The file in use isn't asked about,
        /// since moving the snippets onto it does nothing.
        /// </summary>
        public bool FileHasSnippets(string path)
        {
            try
            {
                return !IsLoadedFile(path) && SnippetFile.Read(ReadAllBytesShared(Path.GetFullPath(path))).Snippets.Count > 0;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return false;
            }
        }

        /// <summary>
        /// Writes the snippets to <paramref name="path"/>, chosen in the Save dialog, and uses that file from now on; the
        /// file they were in is left as it was. A file already at <paramref name="path"/> is backed up beside itself before
        /// it's replaced. For one with snippets (<see cref="FileHasSnippets"/>), <paramref name="choice"/> is the user's
        /// answer: <see cref="TextExpanderFileConflictChoice.UseFile"/> uses its snippets instead and writes nothing,
        /// as <see cref="UseSnippetsFile"/> does. The file in use is left as it is, with no status to show.
        /// </summary>
        /// <returns>null, with <paramref name="status"/> to show, when the snippets moved; otherwise why they didn't.</returns>
        public string MoveSnippetsTo(string path, TextExpanderFileConflictChoice choice, out string status)
        {
            status = null;
            if (IsLoadedFile(path))
            {
                return null;
            }

            if (choice == TextExpanderFileConflictChoice.UseFile)
            {
                return UseSnippetsFile(path, out status);
            }

            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            if (_file == null || _loadedBytes == null)
            {
                return SnippetError;
            }

            bool backedUp;
            try
            {
                // What moves is what the list shows, so a change that arrived since it was read is loaded instead.
                byte[] bytes = _loadedBytes;
                if (!ReadAllBytesShared(_loadedPath).AsSpan().SequenceEqual(bytes))
                {
                    LoadSnippets();
                    return _getResourceString("TextExpander_FileChangedBeforeSave");
                }

                string target = Path.GetFullPath(path);
                backedUp = PublishSnippetsFile(target, bytes);
                string error = UseSnippetsPath(target);
                if (error != null)
                {
                    return error;
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Logger.LogError("Error moving the Text Expander snippets.", ex);
                return ex.Message;
            }

            status = DescribeSnippetsFile("TextExpander_SnippetFile_Saved");
            if (backedUp)
            {
                status += " " + _getResourceString("TextExpander_SnippetFile_ReplacedBackedUp");
            }

            return null;
        }

        /// <summary>
        /// Uses the snippets file at <paramref name="path"/>, chosen in the Open dialog, from now on, and writes nothing.
        /// A file that isn't a snippets file (<see cref="LooksLikeSnippets"/>) is refused.
        /// </summary>
        /// <returns>null, with <paramref name="status"/> to show, when the file is in use; otherwise why it isn't.</returns>
        public string UseSnippetsFile(string path, out string status)
        {
            status = null;
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            try
            {
                string target = Path.GetFullPath(path);
                if (!LooksLikeSnippets(ReadAllBytesShared(target)))
                {
                    return string.Format(CultureInfo.CurrentCulture, _getResourceString("TextExpander_SnippetFile_NotSnippets"), Path.GetFileName(target));
                }

                if (string.Equals(target, _loadedPath, StringComparison.OrdinalIgnoreCase))
                {
                    LoadSnippets();
                }
                else
                {
                    string error = UseSnippetsPath(target);
                    if (error != null)
                    {
                        return error;
                    }
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Logger.LogError("Error opening a Text Expander snippets file.", ex);
                return ex.Message;
            }

            status = HasSnippetError ? null : DescribeSnippetsFile("TextExpander_SnippetFile_Using");
            return null;
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="bytes"/> are a snippets file: JSON with a "snippets" list, which
        /// may be empty. Anything else, such as notes, isn't.
        /// </summary>
        internal static bool LooksLikeSnippets(byte[] bytes)
        {
            try
            {
                SnippetFile.Read(bytes);
                return true;
            }
            catch (SnippetFileFormatException)
            {
                return false;
            }
        }

        /// <summary>Gets the snippets shown in the page, in file order.</summary>
        public TextExpanderRowCollection Snippets { get; } = new TextExpanderRowCollection();

        /// <summary>Gets the rows the search shows, which the list displays.</summary>
        public TextExpanderRowCollection FilteredSnippets { get; } = new TextExpanderRowCollection();

        public int SnippetRevision { get; private set; }

        /// <summary>
        /// Gets or sets how a file watcher's callback reaches the UI thread. The page sets it while
        /// it's shown, and clears it when it closes.
        /// </summary>
        public Action<Action> Dispatch
        {
            get => _dispatch;
            set
            {
                _dispatch = value;
                WatchSnippetFolder();
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

        /// <summary>
        /// Gets the count in the list's heading: how many snippets there are, or how many of them the search shows.
        /// </summary>
        public string SnippetCountText => !CanEditSnippets || HasSnippetError
            ? _getResourceString("TextExpander_SnippetCountUnavailable")
            : string.Format(
                CultureInfo.CurrentCulture,
                _getResourceString((HasSearchText ? "TextExpander_SnippetCount" : "TextExpander_SnippetTotal") + (Snippets.Count == 1 ? "_One" : string.Empty)),
                HasSearchText ? FilteredSnippets.Count : Snippets.Count,
                Snippets.Count);

        /// <summary>Gets a value indicating whether the snippet file could not be read or written.</summary>
        public bool HasSnippetError => CanEditSnippets && !string.IsNullOrEmpty(_snippetError);

        /// <summary>Gets a value indicating whether a snippet can be added: editing is allowed and the file could be read.</summary>
        public bool CanAddSnippets => CanEditSnippets && !HasSnippetError;

        /// <summary>Gets the reason the snippet file could not be read or written.</summary>
        public string SnippetError => _isElevated ? ElevatedSnippetAccessDeniedMessage() : _snippetError ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether the error is that the chosen snippets file is missing or can't be reached: moved,
        /// renamed or deleted, or in a folder, on a drive or in OneDrive that isn't available. The page offers Use a
        /// snippets file… then.
        /// </summary>
        public bool IsSnippetsFileUnavailable => HasSnippetError && _unavailableFolder != null;

        /// <summary>
        /// Gets a value indicating whether the error is that the chosen snippets file isn't where it was, so the error bar
        /// shows its path (<see cref="SnippetsFileDisplayPath"/>).
        /// </summary>
        public bool IsSnippetsFileMissing => IsSnippetsFileUnavailable && _snippetsFileMissing;

        /// <summary>Gets the error's title: that the chosen file can't be found or reached, or that the snippets can't be opened.</summary>
        public string SnippetErrorTitle => !IsSnippetsFileUnavailable
            ? _getResourceString("TextExpander_SnippetError_CantOpen")
            : _snippetsFileMissing
                ? _getResourceString("TextExpander_SnippetError_CantFind")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    _getResourceString("TextExpander_SnippetError_CantReach" + PlaceKey(_unavailableFolder)),
                    TextExpanderFolderText.FolderName(_unavailableFolder));

        /// <summary>Gets a value indicating whether there are no snippets to show.</summary>
        public bool HasNoSnippets => CanEditSnippets && Snippets.Count == 0 && !HasSnippetError;

        /// <summary>Gets a value indicating whether the list shows its heading and count, which the empty state replaces.</summary>
        public bool ShowsSnippetCount => !HasNoSnippets;

        /// <summary>Gets the resolved snippet file, shown so the user can find it outside the app.</summary>
        public string SnippetsFilePath => _loadedPath ?? ResolveSnippetsFile();

        /// <summary>Opens the snippets file in the app Windows uses for JSON files, or in Notepad when no app is set for them.</summary>
        /// <returns>A localized error message, or null when the file was opened.</returns>
        public string OpenSnippetsFile()
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            string path = SnippetsFilePath;
            try
            {
                if (!_storageEnvironment.FileExists(path))
                {
                    return _getResourceString("TextExpander_SnippetFile_OpenMissing");
                }

                _openFile(path);
                return null;
            }
            catch (Exception ex) when (IsFileError(ex) || ex is InvalidOperationException)
            {
                Logger.LogError($"Could not open the Text Expander snippet file '{path}'.", ex);
            }

            return _getResourceString("TextExpander_SnippetFile_OpenFailed");
        }

        /// <summary>
        /// Gets the variables the engine can expand, for the Insert variable menu: only those
        /// <c>Variables.Expand</c> implements, each with a sample of what it gives now.
        /// </summary>
        public IReadOnlyList<TextExpanderVariable> Variables => _variables ??=
        [
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Cursor"), "$|$", hasSample: false),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Date"), "{{date}}", hasSample: true),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Time"), "{{time}}", hasSample: true),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_DateTime"), "{{datetime}}", hasSample: true),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_IsoDate"), "{{isodate}}", hasSample: true),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_CustomDate"), "{{date:dddd}}", hasSample: true),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Clipboard"), "{{clipboard}}", hasSample: false),
            new TextExpanderVariable(_getResourceString("TextExpander_Variable_Prompt"), "{{prompt:Label}}", hasSample: false),
        ];

        /// <summary>
        /// Gets <paramref name="variable"/>'s Insert variable menu text, with what it gives at <paramref name="now"/>.
        /// The page asks each time the menu opens, so the sample is never stale.
        /// </summary>
        public string VariableMenuText(TextExpanderVariable variable, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(variable);
            return variable.HasSample
                ? string.Format(CultureInfo.CurrentCulture, _getResourceString("TextExpander_VariableWithSample"), variable.Name, variable.SampleAt(now))
                : variable.Name;
        }

        /// <summary>
        /// Resolves the snippet file the engine is using, with the rule both share
        /// (<see cref="SnippetPathResolution.FromConfiguredPath"/>); an unset or unusable value means this
        /// module's settings folder, the engine's host default.
        /// </summary>
        private string ResolveSnippetsFile()
            => ResolveSnippetsFileInfo().FilePath;

        private SnippetPathResolution ResolveSnippetsFileInfo()
        {
            if (!CanEditSnippets)
            {
                return new SnippetPathResolution(string.Empty, IsDefault: false);
            }

            try
            {
                if (SnippetPathResolution.FromConfiguredPath(_settings.Properties.SnippetsPath.Value, _storageEnvironment.ExpandEnvironmentVariables) is { } configured)
                {
                    return configured;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                          or PathTooLongException or IOException
                                          or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Fall through to the default; a typo in a path must not leave the page blank.
            }

            return SnippetPathResolution.Default(_storageEnvironment.DefaultSnippetsFolder);
        }

        /// <summary>Reads the snippet file into <see cref="Snippets"/>.</summary>
        public void LoadSnippets()
        {
            AdvanceSnippetRevision(notifyEditor: true);
            _loadedSavesAsIs = null;

            if (!CanEditSnippets)
            {
                _file = null;
                _loadedPath = null;
                _loadedBytes = null;
                Snippets.ReplaceAll(Array.Empty<TextExpanderSnippetViewModel>());
                _snippetError = null;
                RaiseSnippetListChanged();
                WatchSnippetFolder();
                return;
            }

            _snippetError = null;
            var rows = new List<TextExpanderSnippetViewModel>();
            SnippetPathResolution resolution = default;

            try
            {
                resolution = ResolveSnippetsFileInfo();
                string path = resolution.FilePath;

                // Until it loads, the file being tried is the one the label, path and any error describe.
                _loadedPath = path;

                // A chosen file is read where it is and never created there: one that's gone is reported, rather than
                // coming back holding the starter for a sync service to copy to the user's other PCs. Only the default
                // location is created, with the starter.
                if (resolution.IsDefault)
                {
                    _storageEnvironment.CreateDirectory(Path.GetDirectoryName(path));
                    TextExpanderStarter.TryCreateFile(path);
                }

                // The bytes are kept to tell, before a save, whether the file changed since it was read.
                _loadedBytes = ReadAllBytesShared(path);
                _file = SnippetFile.Read(_loadedBytes);

                foreach (SnippetFile.Entry entry in _file.Snippets)
                {
                    rows.Add(new TextExpanderSnippetViewModel(entry.Id, entry.Trigger, entry.Replacement));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                          or System.Security.SecurityException or ArgumentException
                                          or NotSupportedException or PathTooLongException)
            {
                // Say why rather than showing an empty list, which would read as "no snippets" and invite the user to
                // recreate everything on top of a file that still exists. A file that isn't a snippets file stays
                // unloaded, so it's never saved over.
                _file = null;
                _loadedBytes = null;
                ShowFileError(ex, resolution);
                rows.Clear();
            }

            Snippets.ReplaceAll(rows);
            RaiseSnippetListChanged();
            WatchSnippetFolder();
        }

        /// <summary>
        /// Brings the rows in line with the snippets in memory after a save, without reading the
        /// file again. A row whose snippet didn't change is kept as it is, so one save changes one
        /// row of the list.
        /// </summary>
        private void SyncRows()
        {
            AdvanceSnippetRevision(notifyEditor: false);
            var current = new Dictionary<int, TextExpanderSnippetViewModel>(Snippets.Count);
            foreach (TextExpanderSnippetViewModel row in Snippets)
            {
                current[row.Id] = row;
            }

            var rows = new List<TextExpanderSnippetViewModel>(_file.Snippets.Count);
            foreach (SnippetFile.Entry entry in _file.Snippets)
            {
                rows.Add(current.TryGetValue(entry.Id, out TextExpanderSnippetViewModel row)
                        && string.Equals(row.Trigger, entry.Trigger, StringComparison.Ordinal)
                        && string.Equals(row.Replacement, entry.Replacement, StringComparison.Ordinal)
                    ? row
                    : new TextExpanderSnippetViewModel(entry.Id, entry.Trigger, entry.Replacement));
            }

            Snippets.SyncTo(rows);
            RaiseSnippetListChanged();
            WatchSnippetFolder();
        }

        /// <summary>Watches the snippets file while the page is shown.</summary>
        private void WatchSnippetFolder()
        {
            string file = null;
            if (_dispatch != null && CanEditSnippets)
            {
                try
                {
                    file = ResolveSnippetsFileInfo().FilePath;
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    file = null;
                }
            }

            if (file != null && _snippetFileWatch != null && string.Equals(file, _snippetFileWatchPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _snippetFileWatch?.Dispose();
            _snippetFileWatch = null;
            _snippetFileWatchPath = file;
            long generation = ++_snippetFileWatchGeneration;
            string folder = string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder))
            {
                IDisposable watch = _storageEnvironment.WatchForFiles(
                    folder,
                    new[] { Path.GetFileName(file) },
                    () =>
                    {
                        // One check on the UI thread covers every change reported before it runs, since it reads
                        // the file then.
                        if (_dispatch is { } dispatch && Interlocked.Exchange(ref _snippetFileCheckQueued, 1) == 0)
                        {
                            dispatch(() =>
                            {
                                Volatile.Write(ref _snippetFileCheckQueued, 0);
                                SnippetFilesMayHaveChanged();
                            });
                        }
                    });
                _snippetFileWatch = watch;

                // The file can change after LoadSnippets reads it but before the watcher becomes active, so check once
                // after registration.
                SnippetFilesMayHaveChanged(generation);
            }
        }

        internal void SnippetFilesMayHaveChanged()
            => SnippetFilesMayHaveChanged(_snippetFileWatchGeneration);

        private void SnippetFilesMayHaveChanged(long generation)
        {
            if (_snippetFileWatch == null || generation != _snippetFileWatchGeneration)
            {
                return;
            }

            try
            {
                SnippetPathResolution resolution = ResolveSnippetsFileInfo();
                string preferred = resolution.FilePath;
                if (!_storageEnvironment.FileExists(preferred)
                    || !string.Equals(preferred, _loadedPath, StringComparison.OrdinalIgnoreCase)
                    || _loadedBytes == null)
                {
                    LoadSnippets();
                    return;
                }

                byte[] current = ReadAllBytesShared(preferred);
                if (!current.AsSpan().SequenceEqual(_loadedBytes))
                {
                    LoadSnippets();
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                // An editor may still hold the file during a non-atomic save. The watch calls back
                // again when its size or write time next changes, so retry then rather than
                // showing a false error.
            }
        }

        private void AdvanceSnippetRevision(bool notifyEditor)
        {
            SnippetRevision++;
            if (notifyEditor)
            {
                OnPropertyChanged(nameof(SnippetRevision));
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

            string problem = ValidateSnippet(id, trigger);
            if (problem != null)
            {
                return problem;
            }

            SnippetFile previousFile = _file;
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

        public string ValidateSnippet(int? id, string trigger)
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            if (!SnippetFile.IsValidTrigger(trigger, out SnippetProblem problem))
            {
                return DescribeProblem(problem);
            }

            if (_file == null)
            {
                return SnippetError;
            }

            SnippetFile.Entry clash = _file.FindDuplicate(trigger, ignoringId: id, CaseSensitive);
            if (clash != null)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    _getResourceString("TextExpander_DuplicateTrigger"),
                    clash.Trigger);
            }

            return null;
        }

        private string DescribeProblem(SnippetProblem problem) => _getResourceString(problem switch
        {
            SnippetProblem.EmptyTrigger => "TextExpander_Problem_EmptyTrigger",
            SnippetProblem.TriggerHasLineBreak => "TextExpander_Problem_TriggerHasLineBreak",
            SnippetProblem.TriggerStartsWithHiddenCharacter => "TextExpander_Problem_TriggerStartsWithHiddenCharacter",
            SnippetProblem.TriggerHasOuterSpace => "TextExpander_Problem_TriggerHasOuterSpace",
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

        /// <summary>
        /// Gets why the row <paramref name="snippet"/> can't be edited, duplicated or deleted, or null. A reload makes new
        /// rows and numbers the snippets again, so a row from before it may have a different snippet's ID now.
        /// </summary>
        public string ValidateRow(TextExpanderSnippetViewModel snippet)
        {
            if (!CanEditSnippets)
            {
                return ElevatedSnippetAccessDeniedMessage();
            }

            return snippet != null && Snippets.Contains(snippet) ? null : _getResourceString("TextExpander_FileChangedBeforeSave");
        }

        /// <summary>Deletes the row <paramref name="snippet"/>'s snippet and writes the file. See <see cref="ValidateRow"/>.</summary>
        public string DeleteSnippet(TextExpanderSnippetViewModel snippet)
        {
            string problem = ValidateRow(snippet);
            if (problem != null)
            {
                return problem;
            }

            if (_file == null)
            {
                return SnippetError;
            }

            SnippetFile previousFile = _file;
            _file = previousFile.Clone();
            if (!_file.Remove(snippet.Id))
            {
                // Nothing went, so the page mustn't say it was deleted.
                _file = previousFile;
                return _getResourceString("TextExpander_FileChangedBeforeSave");
            }

            return WriteEditedFile(previousFile);
        }

        /// <summary>
        /// Suggests a trigger for a copy of the snippet with <paramref name="id"/>, which the editor opens with;
        /// null while elevated or when the snippet is gone. See <see cref="SuggestCopyTrigger"/>.
        /// </summary>
        public string SuggestDuplicateTrigger(int id)
        {
            if (!CanEditSnippets || _file == null)
            {
                return null;
            }

            SnippetFile.Entry source = _file.Snippets.FirstOrDefault(s => s.Id == id);
            return source == null ? null : SuggestCopyTrigger(source.Trigger, _file.Snippets.Select(s => s.Trigger), RequireWordBoundary, CaseSensitive);
        }

        /// <summary>
        /// Gets a numbered copy of <paramref name="trigger"/> that isn't taken and, by the engine's rules in the
        /// current mode, can fire without blocking a longer trigger. The number goes after any leading symbols
        /// (<c>!mysig</c> gives <c>!2mysig</c>, not <c>!mysig2</c>, which typing <c>!mysig</c> would fire first).
        /// Empty when no number up to 999 works.
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
        /// Gets a value indicating whether adding <paramref name="candidate"/> to <paramref name="taken"/> would make it
        /// fire while one of <paramref name="longer"/> is being typed.
        /// </summary>
        private static bool BlocksAnother(string candidate, List<string> longer, TextExpanderTriggerSet taken, bool wordMode, StringComparison comparison)
        {
            // An index of the candidate alone, matched alongside the taken ones, so theirs isn't rebuilt for each candidate.
            TriggerIndex added = TriggerIndex.Build(new[] { candidate }, comparison == StringComparison.Ordinal);
            foreach (string other in longer)
            {
                if (other.Length > candidate.Length
                    && other.Contains(candidate, comparison)
                    && TextExpanderTriggerReach.FindBlocker(other, taken, wordMode, out _, added) is { } blocker
                    && string.Equals(blocker.Trigger, candidate, comparison))
                {
                    return true;
                }
            }

            return false;
        }

        private string WriteEditedFile(SnippetFile previousFile)
        {
            SnippetFile editedFile = _file;
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

            SnippetPathResolution resolution = default;
            try
            {
                resolution = ResolveSnippetsFileInfo();
                string path = _loadedPath ?? resolution.FilePath;
                if (!string.Equals(resolution.FilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    LoadSnippets();
                    return _getResourceString("TextExpander_FileChangedBeforeSave");
                }

                // A chosen file that's gone isn't written again, since it may have been moved or renamed on another PC.
                // Reloading says so.
                if (!resolution.IsDefault && !File.Exists(path))
                {
                    LoadSnippets();
                    return HasSnippetError ? SnippetError : _getResourceString("TextExpander_FileChangedBeforeSave");
                }

                if (resolution.IsDefault)
                {
                    _storageEnvironment.CreateDirectory(Path.GetDirectoryName(path));
                }

                // Refuse to clobber a file we did not load as snippets. A configured path can aim
                // the editor at an existing arbitrary file, so the next save must not overwrite it.
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

                // A newer PowerToys may keep what this version would lose, so its file isn't saved over.
                if (_file.Version > SnippetFile.FormatVersion)
                {
                    return _getResourceString("TextExpander_SnippetFile_NewerVersion");
                }

                byte[] payload = _file.ToBytes();

                // Saving drops comments and a layout of the file's own, so the first save keeps the file as it was.
                _loadedSavesAsIs ??= SavesAsIs(_loadedBytes);
                PublishSnippetsFile(path, payload, backUp: !_loadedSavesAsIs.Value);

                // The file now holds exactly what was written, so there is nothing to read back.
                _loadedPath = path;
                _loadedBytes = payload;
                _loadedSavesAsIs = true;
                _snippetError = null;
                SyncRows();
                return null;
            }
            catch (SnippetFileWriteException ex)
            {
                // Its snippets still load and expand, and nothing was written; the text has to be fixed by hand.
                Logger.LogError("Text Expander can't save the snippets file as it is.", ex);
                return _getResourceString("TextExpander_SnippetFile_CantSaveText");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                          or System.Security.SecurityException or ArgumentException
                                          or NotSupportedException or PathTooLongException)
            {
                string error = ShowFileError(ex, resolution);
                RaiseSnippetListChanged();
                return error;
            }
        }

        /// <summary>Reads with permissive sharing so the engine's watcher does not block the page.</summary>
        private static byte[] ReadAllBytesShared(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > Array.MaxLength)
            {
                throw new IOException($"The snippets file '{path}' is too large to read.");
            }

            // A file still being written can end before its length said, which throws EndOfStreamException, an IOException.
            byte[] bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }

        /// <summary>
        /// Gets a value indicating whether saving <paramref name="bytes"/> unchanged writes them back exactly, so they have no
        /// comments or layout of their own for a save to lose. An empty file (null) has nothing to lose; one with text that
        /// can't be written back, left in a snippet the save removes, has.
        /// </summary>
        private static bool SavesAsIs(byte[] bytes)
        {
            try
            {
                return bytes == null || SnippetFile.Read(bytes).ToBytes().AsSpan().SequenceEqual(bytes);
            }
            catch (Exception ex) when (ex is SnippetFileFormatException or SnippetFileWriteException)
            {
                return false;
            }
        }

        /// <summary>Gets a value indicating whether <paramref name="path"/> is the snippets file the list was read from.</summary>
        private bool IsLoadedFile(string path)
        {
            try
            {
                return _loadedPath != null && string.Equals(Path.GetFullPath(path), _loadedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return false;
            }
        }

        private string ElevatedSnippetAccessDeniedMessage() => _getResourceString("TextExpander_ElevatedSnippetAccessDenied");

        private PersistedFolderSetting InspectPersistedFolderSetting(
            string previousSnippetsPath,
            string newSnippetsPath)
        {
            try
            {
                TextExpanderSettings persisted = _settingsUtils.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(
                    TextExpanderSettings.ModuleName);
                string persistedPath = persisted?.Properties?.SnippetsPath?.Value ?? string.Empty;
                if (string.Equals(persistedPath, newSnippetsPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new PersistedFolderSetting(
                        PersistedFolderSettingStatus.New,
                        persistedPath,
                        Error: null);
                }

                if (string.Equals(persistedPath, previousSnippetsPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new PersistedFolderSetting(
                        PersistedFolderSettingStatus.Previous,
                        persistedPath,
                        Error: null);
                }

                return new PersistedFolderSetting(
                    PersistedFolderSettingStatus.Other,
                    persistedPath,
                    Error: null);
            }
            catch (Exception ex) when (IsFileError(ex) || ex is InvalidOperationException or System.Text.Json.JsonException)
            {
                Logger.LogWarning($"Could not verify the persisted Text Expander snippet folder after a settings save failure: {ex.Message}");
                return new PersistedFolderSetting(
                    PersistedFolderSettingStatus.Unreadable,
                    SnippetsPath: string.Empty,
                    Error: ex);
            }
        }

        private string FormatSettingsPublicationRecovery(
            string resourceKey,
            AtomicSettingsPublicationException error)
        {
            string settingsPath = error?.SettingsPath
                ?? _settingsUtils.GetSettingsFilePath(TextExpanderSettings.ModuleName);
            string recoveryPaths = error is not null && error.RecoveryPaths.Count > 0
                ? string.Join("; ", error.RecoveryPaths)
                : settingsPath;
            return string.Format(
                CultureInfo.CurrentCulture,
                _getResourceString(resourceKey),
                settingsPath,
                recoveryPaths);
        }

        private void AdoptPersistedFolderSetting(string snippetsPath)
        {
            _settings.Properties.SnippetsPath.Value = snippetsPath ?? string.Empty;
            OnPropertyChanged(nameof(SnippetsPath));
            OnPropertyChanged(nameof(SnippetsFolderPath));
            LoadSnippets();
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

        private static bool IsFileError(Exception ex)
            => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                or ArgumentException or NotSupportedException or PathTooLongException
                or System.ComponentModel.Win32Exception;

        private static void OpenFileWithDefaultApplication(string path)
            => OpenFile(path, info => Process.Start(info)?.Dispose());

        /// <summary>
        /// Opens <paramref name="path"/> through <paramref name="start"/> in the app Windows uses for .json files, or in
        /// Notepad when no app is set for them. Any other file opens in Notepad, since opening a script or a shortcut
        /// in the app for its type would run it.
        /// </summary>
        internal static void OpenFile(string path, Action<ProcessStartInfo> start)
        {
            if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return;
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == ErrorNoAssociation)
                {
                    // No app opens .json files on this PC, so Notepad does.
                }
            }

            var notepad = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { UseShellExecute = false };
            notepad.ArgumentList.Add(path);
            start(notepad);
        }

        /// <summary>Gets a value indicating whether <paramref name="folder"/> is the engine's own folder, the one that's on this PC only.</summary>
        private bool IsDefaultFolder(string folder)
            => string.Equals(
                Path.TrimEndingDirectorySeparator(folder ?? string.Empty),
                Path.TrimEndingDirectorySeparator(_storageEnvironment.DefaultSnippetsFolder),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a value indicating whether <paramref name="folder"/> is a chosen folder inside OneDrive. Only the path is
        /// compared with the folders OneDrive's environment variables name; no account is looked at.
        /// </summary>
        private bool IsInOneDrive(string folder)
            => !IsDefaultFolder(folder) && TextExpanderFolderText.IsInOneDrive(folder, _storageEnvironment.ExpandEnvironmentVariables);

        /// <summary>Gets the part of a resource name that says where <paramref name="folder"/> is: Default (this PC), OneDrive or Custom.</summary>
        private string PlaceKey(string folder)
            => IsDefaultFolder(folder) ? "Default" : IsInOneDrive(folder) ? "OneDrive" : "Custom";

        /// <summary>
        /// Shows why the snippets file in <paramref name="resolution"/> couldn't be read or written, and returns that message.
        /// A chosen file that's missing, or whose folder can't be reached, gets guidance instead of the system's message, and
        /// the page offers Use a snippets file… with it. A folder where the file should be (earlier builds saved one) counts
        /// as missing. One that isn't a snippets file says where its mistake is. This PC's own folder keeps the system's
        /// message.
        /// </summary>
        private string ShowFileError(Exception error, SnippetPathResolution resolution)
        {
            string folder = Path.GetDirectoryName(resolution.FilePath ?? string.Empty) ?? string.Empty;
            bool chosenFile = !resolution.IsDefault && folder.Length > 0;
            _snippetsFileMissing = chosenFile
                && (error is FileNotFoundException || (error is UnauthorizedAccessException && Directory.Exists(resolution.FilePath)));
            bool unreachable = chosenFile && !IsDefaultFolder(folder) && TextExpanderFolderText.IsUnreachable(error);
            _unavailableFolder = _snippetsFileMissing || unreachable ? folder : null;
            _snippetError = _snippetsFileMissing ? _getResourceString("TextExpander_SnippetError_CantFindHelp")
                : unreachable ? _getResourceString("TextExpander_SnippetError_CantReach" + PlaceKey(folder) + "Help")
                : error is SnippetFileFormatException { LineNumber: long line } ? string.Format(CultureInfo.CurrentCulture, _getResourceString("TextExpander_SnippetError_NotJson"), line)
                : error is SnippetFileFormatException ? _getResourceString("TextExpander_SnippetError_NoList")
                : error.Message;
            return _snippetError;
        }

        /// <summary>Formats a status from the resources starting <paramref name="keyPrefix"/>: how many snippets are in use, and where.</summary>
        private string DescribeSnippetsFile(string keyPrefix)
        {
            string folder = SnippetsFolderPath;
            int count = Snippets.Count;
            string key = keyPrefix + PlaceKey(folder);
            return string.Format(CultureInfo.CurrentCulture, _getResourceString(count == 1 ? key + "_One" : key), count, TextExpanderFolderText.FolderName(folder));
        }

        /// <summary>Splits the sentence <paramref name="key"/> around its {0}, where the button named by <paramref name="buttonKey"/> goes.</summary>
        private TextExpanderSyncStep SyncStep(string key, string buttonKey)
        {
            string sentence = _getResourceString(key);
            int button = sentence.IndexOf("{0}", StringComparison.Ordinal);
            return button < 0
                ? new TextExpanderSyncStep(sentence, string.Empty, string.Empty)
                : new TextExpanderSyncStep(sentence[..button], _getResourceString(buttonKey), sentence[(button + 3)..]);
        }

        /// <summary>
        /// Writes <paramref name="bytes"/> to <paramref name="target"/> in one step, through a temporary file beside it, so
        /// nothing reads it half-written. A file already there is backed up beside itself as it's replaced, unless
        /// <paramref name="backUp"/> is false.
        /// </summary>
        /// <returns>Whether a file was replaced.</returns>
        private bool PublishSnippetsFile(string target, byte[] bytes, bool backUp = true)
        {
            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            _storageEnvironment.WriteNewFile(temp, bytes);
            try
            {
                if (_storageEnvironment.FileExists(target))
                {
                    _storageEnvironment.ReplaceFile(temp, target, backUp ? BackupPath(target) : null);
                    return true;
                }

                _storageEnvironment.MoveFile(temp, target);
                return false;
            }
            catch
            {
                if (_storageEnvironment.FileExists(target))
                {
                    // Only the temporary file this wrote is deleted, never a file that replaced it.
                    HandleBoundFileDeletionResult cleanup = _storageEnvironment.DeleteOwnedFile(temp, bytes);
                    if (!cleanup.Succeeded)
                    {
                        Logger.LogWarning($"Could not delete the Text Expander temporary file '{temp}': {cleanup.Error?.Message}");
                    }
                }
                else
                {
                    // A replace that fails part way can leave nothing at the target (ERROR_UNABLE_TO_MOVE_REPLACEMENT
                    // without a backup, or ERROR_UNABLE_TO_MOVE_REPLACEMENT_2), so the temporary file may be the only
                    // copy: it goes into place, or stays where it is.
                    try
                    {
                        _storageEnvironment.MoveFile(temp, target);
                    }
                    catch (Exception ex) when (IsFileError(ex))
                    {
                        Logger.LogWarning($"Kept the Text Expander snippets in '{temp}': {ex.Message}");
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Saves <paramref name="path"/> as the snippets file setting, then loads it. The setting goes first: the engine
        /// follows it, so the editor never shows a file the engine isn't using.
        /// </summary>
        /// <returns>null when the setting was saved; otherwise what happened to it.</returns>
        private string UseSnippetsPath(string path)
        {
            string previous = _settings.Properties.SnippetsPath.Value;
            try
            {
                _settingsUtils.UpdateSnippetsPath(path);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving the Text Expander snippets file setting.", ex);
                AtomicSettingsPublicationException atomicError = ex as AtomicSettingsPublicationException;
                PersistedFolderSetting persisted = InspectPersistedFolderSetting(previous, path);
                bool unreadable = persisted.Status == PersistedFolderSettingStatus.Unreadable;
                if (persisted.Status == PersistedFolderSettingStatus.New
                    || (unreadable && atomicError?.Outcome == AtomicSettingsPublicationOutcome.NewSettingsVerified))
                {
                    AdoptPersistedFolderSetting(path);
                    return FormatSettingsPublicationRecovery("TextExpander_SnippetFolder_SettingsCommittedAfterError", atomicError);
                }

                if (persisted.Status == PersistedFolderSettingStatus.Previous
                    || (unreadable && atomicError?.Outcome == AtomicSettingsPublicationOutcome.PreviousSettingsVerified))
                {
                    return _getResourceString("TextExpander_SnippetFolder_SettingsSaveFailed");
                }

                if (persisted.Status == PersistedFolderSettingStatus.Other)
                {
                    AdoptPersistedFolderSetting(persisted.SnippetsPath);
                }

                return FormatSettingsPublicationRecovery("TextExpander_SnippetFolder_SettingsStateUncertain", atomicError);
            }

            AdoptPersistedFolderSetting(path);
            return null;
        }

        /// <summary>
        /// Describes every row: what it does, and whether it can ever expand. That depends on the
        /// other triggers and on Wait for space or punctuation, so it is worked out for the
        /// whole list.
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
            OnPropertyChanged(nameof(ShowsSnippetCount));
            OnPropertyChanged(nameof(HasSnippetError));
            OnPropertyChanged(nameof(CanAddSnippets));
            OnPropertyChanged(nameof(SnippetError));
            OnPropertyChanged(nameof(SnippetErrorTitle));
            OnPropertyChanged(nameof(IsSnippetsFileUnavailable));
            OnPropertyChanged(nameof(IsSnippetsFileMissing));
            OnPropertyChanged(nameof(SnippetsFilePath));
            OnPropertyChanged(nameof(SnippetsFolderPath));
            OnPropertyChanged(nameof(SnippetsFileDisplayPath));
            OnPropertyChanged(nameof(SnippetsPickerFolder));
            OnPropertyChanged(nameof(IsSnippetsFileInOneDrive));
            OnPropertyChanged(nameof(SyncHeading));
            OnPropertyChanged(nameof(SyncSteps));

            OnPropertyChanged(nameof(SnippetFileSummary));
            OnPropertyChanged(nameof(SnippetFolderLabel));
        }

        private void Save(Action<TextExpanderProperties> update)
        {
            // The engine watches this file, so a save is what applies the change -- no restart.
            _settings = _settingsUtils.UpdateOptions(update);
        }

        public void WatchSettings()
        {
            string path = _settingsUtils.GetSettingsFilePath(TextExpanderSettings.ModuleName);
            _settingsWatch?.Dispose();
            _settingsWatch = _storageEnvironment.WatchForFiles(
                Path.GetDirectoryName(path),
                new[] { Path.GetFileName(path) },
                () => _dispatch?.Invoke(RefreshSettings));
        }

        public void RefreshSettings()
        {
            try
            {
                TextExpanderSettings latest = _settingsUtils.GetSettingsOrDefaultReadOnly<TextExpanderSettings>(TextExpanderSettings.ModuleName);
                if (latest.ToJsonString() == _settings.ToJsonString())
                {
                    return;
                }

                bool folderChanged = latest.Properties.SnippetsPath.Value != _settings.Properties.SnippetsPath.Value;
                bool caseSensitivityChanged = latest.Properties.CaseSensitive != _settings.Properties.CaseSensitive;
                bool wordBoundaryChanged = latest.Properties.RequireWordBoundary != _settings.Properties.RequireWordBoundary;
                _settings = latest;
                if (folderChanged)
                {
                    LoadSnippets();
                }
                else
                {
                    if (caseSensitivityChanged)
                    {
                        ApplyCaseSensitivityToRows();
                    }

                    if (caseSensitivityChanged || wordBoundaryChanged)
                    {
                        DescribeSnippets();
                    }
                }

                if (caseSensitivityChanged)
                {
                    OnPropertyChanged(nameof(CaseSensitive));
                }

                if (wordBoundaryChanged)
                {
                    OnPropertyChanged(nameof(RequireWordBoundary));
                }

                OnPropertyChanged(string.Empty);
            }
            catch (Exception ex) when (IsFileError(ex) || ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                Logger.LogError("Could not reload Text Expander editor settings.", ex);
                _snippetError = ex.Message;
                _unavailableFolder = null;
                RaiseSnippetListChanged();
            }
        }

        public void Dispose()
        {
            Dispatch = null;
            _settingsWatch?.Dispose();
            _settingsWatch = null;
            GC.SuppressFinalize(this);
        }
    }
}
