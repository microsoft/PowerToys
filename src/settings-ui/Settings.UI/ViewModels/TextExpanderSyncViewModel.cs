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
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// The guided "Sync snippets across PCs" setup, which keeps the snippet library in OneDrive.
    ///
    /// <para>
    /// One rule runs through every change: a snippet file must exist in the destination before the
    /// snippet folder setting points there. The engine writes starter snippets into a folder that
    /// has none, and in OneDrive those would sync to every PC.
    /// </para>
    ///
    /// <para>
    /// A snippet file is found under either name (<see cref="TextExpanderSnippetFileNames"/>), so
    /// a OneDrive folder that an earlier version filled keeps working everywhere. New files get the
    /// current name; an existing file is never renamed.
    /// </para>
    ///
    /// <para>
    /// When the snippets leave OneDrive, the folder they were in is saved as
    /// <see cref="TextExpanderViewModel.LastSyncFolder"/>, so turning sync back on uses that folder
    /// again instead of the standard one.
    /// </para>
    ///
    /// <para>
    /// Nothing here runs while Settings is elevated: no environment lookups, file access,
    /// watchers or launches. Every entry point checks <see cref="TextExpanderViewModel.CanEditSnippets"/> first.
    /// </para>
    /// </summary>
    public sealed partial class TextExpanderSyncViewModel : Observable, IDisposable
    {
        public const int FirstPcIndex = 0;
        public const int AnotherPcIndex = 1;

        public const int UseOneDriveChoice = 0;
        public const int ReplaceOneDriveChoice = 1;
        public const int KeepBothChoice = 2;

        internal const FileAttributes PinnedAttribute = (FileAttributes)0x00080000;
        internal const FileAttributes UnpinnedAttribute = (FileAttributes)0x00100000;
        internal const FileAttributes RecallOnDataAccessAttribute = (FileAttributes)0x00400000;

        private const string ConsumerVariable = "OneDriveConsumer";
        private const string CommercialVariable = "OneDriveCommercial";
        private const string GenericVariable = "OneDrive";
        private const string WorkFolderPrefix = "OneDrive - ";

        // Between the folders of a location, as in File Explorer's address bar: "OneDrive - Contoso › PowerToys".
        private const string LocationSeparator = " \u203A ";

        // How many of the files a OneDrive search finds the "another PC" setup offers.
        private const int MaxCandidates = 5;

        // Only attributes SetFileAttributes accepts; the rest are owned by the file system or the sync provider.
        private const FileAttributes SettableAttributes = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System
            | FileAttributes.Archive | FileAttributes.Temporary | FileAttributes.NotContentIndexed | PinnedAttribute | UnpinnedAttribute;

        private static readonly string[] SnippetFileNames = { TextExpanderSnippetFileNames.Current, TextExpanderSnippetFileNames.Legacy };

        private readonly TextExpanderViewModel _owner;
        private readonly ITextExpanderSyncEnvironment _environment;
        private readonly Func<string, string> _getResourceString;
        private readonly Func<string, Task<string>> _pickFolder;

        private IDisposable _watch;
        private CancellationTokenSource _searchCancellation;
        private TextExpanderSyncAccount _waitingAccount;
        private string _waitingFolder;
        private TextExpanderSyncAccount _foundAccount;
        private string _foundFolder;
        private TextExpanderSyncAccount _customAccount;
        private string _customFolder;
        private TextExpanderSyncAccount _rememberedAccount;
        private string _rememberedFolder;
        private TextExpanderSyncAccount _syncedAccount;
        private string _lastResolvedFile;
        private bool _hasRefreshed;
        private bool _statusLoaded;
        private bool _suppressEvaluate;
        private bool _changingFolder;

        private int _selectedAccountIndex = -1;
        private int _setupKindIndex = FirstPcIndex;
        private int _conflictChoiceIndex = UseOneDriveChoice;
        private bool _isOneDriveMissing;
        private bool _canOpenOneDrive;
        private bool _isSynced;
        private string _syncedStatusText = string.Empty;
        private string _syncedFilePath = string.Empty;
        private bool _isOnlineOnly;
        private bool _showKeepCopyOption;
        private bool _keepCopyOnThisPc;
        private string _firstPcDescription = string.Empty;
        private bool _hasConflict;
        private string _conflictTitle = string.Empty;
        private string _useOneDriveChoiceText = string.Empty;
        private string _replaceChoiceText = string.Empty;
        private string _keepBothDescription = string.Empty;
        private bool _isFound;
        private string _foundText = string.Empty;
        private string _foundLocationText = string.Empty;
        private string _foundFilePath = string.Empty;
        private bool _isWaiting;
        private bool _isSearching;
        private int _searchVersion;
        private int _selectedCandidateIndex = -1;
        private string _notFoundMessage = string.Empty;
        private string _waitingLocationText = string.Empty;
        private string _waitingFilePath = string.Empty;
        private string _syncError;
        private bool _isSetupRequested;

        public TextExpanderSyncViewModel(TextExpanderViewModel owner, ITextExpanderSyncEnvironment environment, Func<string, string> getResourceString, Func<string, Task<string>> pickFolder)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _getResourceString = getResourceString ?? throw new ArgumentNullException(nameof(getResourceString));
            _pickFolder = pickFolder;
            _owner.PropertyChanged += Owner_PropertyChanged;
        }

        /// <summary>Raised with text that should be announced to screen readers.</summary>
        public event EventHandler<string> Announcement;

        /// <summary>Gets or sets how folder-watcher callbacks reach the UI thread.</summary>
        public Action<Action> Dispatch { get; set; } = action => action();

        /// <summary>Gets or sets how the OneDrive search runs off the UI thread. Tests run it in place.</summary>
        public Func<Action, Task> RunInBackground { get; set; } = work => Task.Run(work);

        /// <summary>Gets the snippet files the "another PC" setup found elsewhere in OneDrive, newest first. Shown only when there are several.</summary>
        public ObservableCollection<TextExpanderSyncCandidate> Candidates { get; } = new ObservableCollection<TextExpanderSyncCandidate>();

        public bool HasCandidates => Candidates.Count > 0;

        public int SelectedCandidateIndex
        {
            get => _selectedCandidateIndex;
            set
            {
                if (Set(ref _selectedCandidateIndex, value))
                {
                    OnPropertyChanged(nameof(CanUseCandidate));
                }
            }
        }

        public bool CanUseCandidate => AreStepsEnabled && _selectedCandidateIndex >= 0 && _selectedCandidateIndex < Candidates.Count;

        /// <summary>Gets a value indicating whether the "another PC" setup is looking for the snippets in OneDrive, which is the only time it shows a spinner.</summary>
        public bool IsSearching
        {
            get => _isSearching;
            private set => Set(ref _isSearching, value);
        }

        public ObservableCollection<TextExpanderSyncAccount> Accounts { get; } = new ObservableCollection<TextExpanderSyncAccount>();

        public int SelectedAccountIndex
        {
            get => _selectedAccountIndex;
            set
            {
                if (Set(ref _selectedAccountIndex, value) && !_suppressEvaluate)
                {
                    OnPropertyChanged(nameof(CanCopy));
                    Evaluate();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether there's an account to choose. With only one, setup
        /// doesn't ask, and the summary names it once syncing.
        /// </summary>
        public bool HasMultipleAccounts => Accounts.Count > 1;

        public bool IsOneDriveMissing
        {
            get => _isOneDriveMissing;
            private set
            {
                if (Set(ref _isOneDriveMissing, value))
                {
                    OnPropertyChanged(nameof(AreStepsEnabled));
                    OnPropertyChanged(nameof(CanCopy));
                    RaiseToggleState();
                }
            }
        }

        public bool CanOpenOneDrive
        {
            get => _canOpenOneDrive;
            private set => Set(ref _canOpenOneDrive, value);
        }

        /// <summary>Gets a value indicating whether the setup steps can be used.</summary>
        public bool AreStepsEnabled => !_isOneDriveMissing;

        public bool IsSynced
        {
            get => _isSynced;
            private set
            {
                if (Set(ref _isSynced, value))
                {
                    RaiseToggleState();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Sync across PCs toggle is on: the snippet folder is in
        /// OneDrive, or the user turned it on and the setup isn't finished yet, including while
        /// waiting for OneDrive to deliver the file.
        /// </summary>
        public bool IsSyncOn => _isSynced || _isSetupRequested;

        /// <summary>Gets a value indicating whether the toggle can be used: not while elevated, and only with a OneDrive account.</summary>
        public bool CanToggle => _owner.CanEditSnippets && !_isOneDriveMissing && (_isSynced || Accounts.Count > 0);

        /// <summary>Gets a value indicating whether the setup shows under the toggle, which it does only after the user turns it on.</summary>
        public bool IsSetupVisible => _isSetupRequested && !_isSynced;

        /// <summary>
        /// Gets a value indicating whether anything shows under the toggle, so an empty row is never
        /// left behind: only what needs attention, or the setup. A finished change is announced to
        /// screen readers (<see cref="Announcement"/>) and leaves nothing behind; the expander's
        /// summary already says what is in use.
        /// </summary>
        public bool ShowDetails => _isOneDriveMissing
            || HasSyncError
            || IsSetupVisible
            || (_isSynced && (_isOnlineOnly || _showKeepCopyOption));

        public string SyncedStatusText
        {
            get => _syncedStatusText;
            private set => Set(ref _syncedStatusText, value);
        }

        public string SyncedFilePath
        {
            get => _syncedFilePath;
            private set => Set(ref _syncedFilePath, value);
        }

        /// <summary>Gets a value indicating whether the snippet file has no local copy and is not pinned.</summary>
        public bool IsOnlineOnly
        {
            get => _isOnlineOnly;
            private set
            {
                if (Set(ref _isOnlineOnly, value))
                {
                    OnPropertyChanged(nameof(ShowDetails));
                }
            }
        }

        public bool ShowKeepCopyOption
        {
            get => _showKeepCopyOption;
            private set
            {
                if (Set(ref _showKeepCopyOption, value))
                {
                    OnPropertyChanged(nameof(ShowDetails));
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether the snippet file is pinned, as "Always keep on this device" does.</summary>
        public bool KeepCopyOnThisPc
        {
            get => _keepCopyOnThisPc;
            set => ApplyKeepCopy(value);
        }

        public int SetupKindIndex
        {
            get => _setupKindIndex;
            set
            {
                if (Set(ref _setupKindIndex, value))
                {
                    OnPropertyChanged(nameof(IsFirstPcSelected));
                    OnPropertyChanged(nameof(IsAnotherPcSelected));
                    if (value != AnotherPcIndex)
                    {
                        _customFolder = null;
                        _customAccount = null;
                    }

                    Evaluate();
                }
            }
        }

        public bool IsFirstPcSelected => _setupKindIndex == FirstPcIndex;

        public bool IsAnotherPcSelected => _setupKindIndex == AnotherPcIndex;

        public string FirstPcDescription
        {
            get => _firstPcDescription;
            private set => Set(ref _firstPcDescription, value);
        }

        public bool HasConflict
        {
            get => _hasConflict;
            private set
            {
                if (Set(ref _hasConflict, value))
                {
                    OnPropertyChanged(nameof(FirstPcActionText));
                    OnPropertyChanged(nameof(CanCopy));
                }
            }
        }

        public string ConflictTitle
        {
            get => _conflictTitle;
            private set => Set(ref _conflictTitle, value);
        }

        public int ConflictChoiceIndex
        {
            get => _conflictChoiceIndex;
            set
            {
                if (Set(ref _conflictChoiceIndex, value))
                {
                    OnPropertyChanged(nameof(CanCopy));
                }
            }
        }

        public string UseOneDriveChoiceText
        {
            get => _useOneDriveChoiceText;
            private set => Set(ref _useOneDriveChoiceText, value);
        }

        public string ReplaceChoiceText
        {
            get => _replaceChoiceText;
            private set => Set(ref _replaceChoiceText, value);
        }

        public string KeepBothDescription
        {
            get => _keepBothDescription;
            private set => Set(ref _keepBothDescription, value);
        }

        public string FirstPcActionText => _getResourceString(_hasConflict ? "TextExpander_Sync_ContinueButton" : "TextExpander_Sync_CopyButton");

        public bool CanCopy => AreStepsEnabled && SelectedAccount != null && (!_hasConflict || _conflictChoiceIndex >= 0);

        public bool IsFound
        {
            get => _isFound;
            private set => Set(ref _isFound, value);
        }

        public string FoundText
        {
            get => _foundText;
            private set => Set(ref _foundText, value);
        }

        /// <summary>
        /// Gets where the found file is, from the account's folder down to the file's own name, so
        /// a wrong folder is easy to spot: "Location: OneDrive - Contoso › Snippets › snippets.txt".
        /// </summary>
        public string FoundLocationText
        {
            get => _foundLocationText;
            private set => Set(ref _foundLocationText, value);
        }

        /// <summary>Gets the found file's full path, the location's tooltip.</summary>
        public string FoundFilePath
        {
            get => _foundFilePath;
            private set => Set(ref _foundFilePath, value);
        }

        /// <summary>
        /// Gets a value indicating whether nothing was found and the setup keeps watching the folder
        /// the snippets belong in, while the page is open, in case OneDrive is still downloading them.
        /// </summary>
        public bool IsWaiting
        {
            get => _isWaiting;
            private set => Set(ref _isWaiting, value);
        }

        /// <summary>Gets what was checked when nothing was found: the default location and the rest of OneDrive, or only the folder the user chose.</summary>
        public string NotFoundMessage
        {
            get => _notFoundMessage;
            private set => Set(ref _notFoundMessage, value);
        }

        /// <summary>
        /// Gets the folder the setup watches, from the account's folder down, as File Explorer names
        /// the folders: "Default location: OneDrive - Contoso › PowerToys › TextExpander".
        /// </summary>
        public string WaitingLocationText
        {
            get => _waitingLocationText;
            private set => Set(ref _waitingLocationText, value);
        }

        /// <summary>Gets the full path of the snippet file the setup watches for, the location's tooltip.</summary>
        public string WaitingFilePath
        {
            get => _waitingFilePath;
            private set => Set(ref _waitingFilePath, value);
        }

        public string SyncError
        {
            get => _syncError;
            private set
            {
                if (Set(ref _syncError, value))
                {
                    OnPropertyChanged(nameof(HasSyncError));
                    OnPropertyChanged(nameof(ShowDetails));
                }
            }
        }

        public bool HasSyncError => !string.IsNullOrEmpty(_syncError);

        private TextExpanderSyncAccount SelectedAccount
            => _selectedAccountIndex >= 0 && _selectedAccountIndex < Accounts.Count ? Accounts[_selectedAccountIndex] : null;

        /// <summary>Gets a value indicating whether a file has data only in the cloud.</summary>
        public static bool IsOnlineOnlyFile(FileAttributes attributes)
            => HasCloudOnlyContent(attributes) && (attributes & PinnedAttribute) == 0;

        /// <summary>
        /// Re-reads OneDrive and the current snippet folder. While the setup is showing, this also
        /// checks the destination, which reads the snippet file in OneDrive.
        /// </summary>
        public void Refresh()
        {
            if (!_owner.CanEditSnippets)
            {
                ResetForElevated();
                return;
            }

            _hasRefreshed = true;
            SyncError = null;
            RefreshStatus();
            Evaluate();
        }

        /// <summary>
        /// Handles the user flipping the Sync across PCs toggle, and says what the page does next.
        ///
        /// <para>
        /// A change that only mirrors <see cref="IsSyncOn"/> does nothing. That is what the toggle
        /// reports when the page shows the state after a setup finishes or a stop is cancelled, so
        /// no flow ever starts again by itself.
        /// </para>
        /// </summary>
        public TextExpanderSyncToggleAction UserToggled(bool isOn)
        {
            if (!_owner.CanEditSnippets || isOn == IsSyncOn)
            {
                return TextExpanderSyncToggleAction.None;
            }

            if (isOn)
            {
                // Accounts or the folder may have changed since the page opened.
                RefreshStatus();
                if (!CanToggle || _isSynced)
                {
                    return TextExpanderSyncToggleAction.None;
                }

                FindRememberedFolder();
                IsSetupRequested = true;
                Refresh();
                return IsSetupVisible ? TextExpanderSyncToggleAction.ShowSetup : TextExpanderSyncToggleAction.None;
            }

            if (_isSynced)
            {
                // StopSyncing runs only once the user confirms.
                return TextExpanderSyncToggleAction.ConfirmStop;
            }

            CancelSetup();
            return TextExpanderSyncToggleAction.None;
        }

        /// <summary>Cancels an unfinished setup, including a search or a wait for OneDrive, so the toggle goes back to Off.</summary>
        public void CancelSetup()
        {
            if (!_owner.CanEditSnippets || !_isSetupRequested)
            {
                return;
            }

            bool wasLooking = _isWaiting || _isSearching;
            EndSetup();
            if (wasLooking)
            {
                Announce(_getResourceString("TextExpander_Sync_AnnounceWaitingCancelled"));
            }
        }

        /// <summary>Another PC found nothing: sets this PC up as the first one instead, keeping the account chosen.</summary>
        public void SetUpAsFirstPc()
        {
            if (_owner.CanEditSnippets && _isSetupRequested)
            {
                SetupKindIndex = FirstPcIndex;
            }
        }

        /// <summary>
        /// Called when the setup is left without finishing it: the box collapses, or the page
        /// closes. Collapsing keeps watching for the snippets after a search found none, as the
        /// toggle still says On; closing the page ends everything, including a search.
        /// </summary>
        public void LeaveSetup(bool pageClosing)
        {
            if (pageClosing)
            {
                StopWaiting();
            }

            if (_isSetupRequested && !_isWaiting)
            {
                EndSetup();
            }
        }

        /// <summary>
        /// Updates the account list and whether the snippet folder is in OneDrive, for the summary
        /// shown while the setup is collapsed. Unlike <see cref="Refresh"/>, this reads no file
        /// content in OneDrive, so it cannot start downloading an online-only file.
        /// </summary>
        public void RefreshStatus()
        {
            if (!_owner.CanEditSnippets)
            {
                ResetForElevated();
                return;
            }

            _statusLoaded = true;
            DiscoverAccounts();
            RefreshSyncedState();
            UpdateCounts();
        }

        /// <summary>
        /// First PC: copies this PC's snippets to OneDrive, or applies the chosen conflict
        /// resolution, then points Text Expander at OneDrive. Returns null or the reason it failed.
        /// </summary>
        public string CopyToOneDrive()
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            SyncError = null;
            TextExpanderSyncAccount account = SelectedAccount;
            if (!_hasRefreshed || _isOneDriveMissing || account == null)
            {
                return Fail("TextExpander_Sync_NoAccount");
            }

            string folder = DestinationFolder(account);
            string source = _owner.SnippetsFilePath;

            try
            {
                // OneDrive's file, under either name.
                string existing = FindSnippetFile(folder);
                if (existing != null && !_hasConflict)
                {
                    // It appeared after the page last looked. Never overwrite without a choice.
                    ShowConflict(existing);
                    return Fail("TextExpander_Sync_ChooseWhichToKeep");
                }

                _environment.CreateDirectory(folder);
                if (existing == null)
                {
                    WriteAtomically(Path.Combine(folder, TextExpanderSnippetFileNames.Current), ReadSnippetsOrEmpty(source), backup: null, replace: false);
                }
                else
                {
                    switch (_conflictChoiceIndex)
                    {
                        case UseOneDriveChoice:
                            break;
                        case ReplaceOneDriveChoice:
                            // Replaced in place, under the name it has, so PCs on an earlier version still find it.
                            WriteAtomically(existing, ReadSnippetsOrEmpty(source), BackupPath(existing), replace: true);
                            break;
                        case KeepBothChoice:
                            WriteAtomically(UniqueFilePath(folder, PcCopyBaseName(existing), ".txt"), ReadSnippetsOrEmpty(source), backup: null, replace: false);
                            break;
                        default:
                            return Fail("TextExpander_Sync_ChooseWhichToKeep");
                    }
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                SyncError = ex.Message;
                return ex.Message;
            }

            string error = SwitchTo(account, folder);
            if (error != null)
            {
                SyncError = error;
                return error;
            }

            Announce(Format("TextExpander_Sync_AnnounceCopied", account.DisplayName));
            return null;
        }

        /// <summary>Another PC: switches to the snippet file found in OneDrive.</summary>
        public string UseFoundSnippets()
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            SyncError = null;
            if (!_isFound || _foundAccount == null)
            {
                return null;
            }

            TextExpanderSyncAccount account = _foundAccount;
            string error = SwitchTo(account, _foundFolder);
            if (error != null)
            {
                SyncError = error;
                return error;
            }

            Announce(Format("TextExpander_Sync_AnnounceUsing", account.DisplayName));
            return null;
        }

        /// <summary>Another PC: switches to the snippet file chosen from those found in OneDrive.</summary>
        public string UseSelectedCandidate()
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            SyncError = null;
            if (!CanUseCandidate)
            {
                return null;
            }

            TextExpanderSyncCandidate candidate = Candidates[_selectedCandidateIndex];
            string error = SwitchTo(candidate.Account, Path.GetDirectoryName(candidate.FilePath));
            if (error != null)
            {
                SyncError = error;
                return error;
            }

            Announce(Format("TextExpander_Sync_AnnounceUsing", candidate.Account.DisplayName));
            return null;
        }

        /// <summary>Stops waiting for the snippet file. The setup ends too, so the toggle goes back to Off.</summary>
        public void CancelWaiting() => CancelSetup();

        /// <summary>Stops the folder watcher without changing any choice, for example when the page closes.</summary>
        public void StopWaiting()
        {
            _watch?.Dispose();
            _watch = null;
            _waitingAccount = null;
            _waitingFolder = null;
            IsWaiting = false;
            NotFoundMessage = string.Empty;
            WaitingLocationText = string.Empty;
            WaitingFilePath = string.Empty;
        }

        /// <summary>Another PC: lets the user pick the OneDrive folder used on their first PC, starting in the selected account's folder.</summary>
        public async Task<string> ChooseDifferentFolderAsync()
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            if (_pickFolder is null)
            {
                return null;
            }

            string picked;
            try
            {
                picked = await _pickFolder(SelectedAccount?.Root);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking the Text Expander OneDrive folder.", ex);
                return null;
            }

            return string.IsNullOrWhiteSpace(picked) ? null : ApplyFolderChoice(picked);
        }

        /// <summary>Uses <paramref name="folder"/> for "another PC" if it is inside a OneDrive account.</summary>
        public string ApplyFolderChoice(string folder)
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            if (!_hasRefreshed)
            {
                Refresh();
            }

            SyncError = null;
            TextExpanderSyncAccount account = FindAccountContaining(folder);
            if (account == null)
            {
                return Fail("TextExpander_Sync_FolderOutsideOneDrive");
            }

            // Choosing the folder used on another PC is part of setting sync up.
            if (!_isSynced)
            {
                IsSetupRequested = true;
            }

            _customFolder = NormalizeFolder(folder);
            _customAccount = account;
            if (_setupKindIndex == AnotherPcIndex)
            {
                Evaluate();
            }
            else
            {
                SetupKindIndex = AnotherPcIndex;
            }

            return null;
        }

        /// <summary>
        /// Copies the OneDrive snippet file back to this PC under the current name, keeping the
        /// local file it replaces, under either name, as a backup named after it. Then clears the
        /// snippet folder, and remembers the OneDrive folder for turning sync back on. The OneDrive
        /// copy is never deleted.
        /// </summary>
        public string StopSyncing()
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            SyncError = null;
            if (!_isSynced)
            {
                return null;
            }

            string oneDriveFile = _owner.SnippetsFilePath;
            string localFolder = _environment.DefaultSnippetsFolder;
            string localFile = Path.Combine(localFolder, TextExpanderSnippetFileNames.Current);

            try
            {
                if (!_environment.FileExists(oneDriveFile))
                {
                    string message = _getResourceString("TextExpander_Sync_StopMissingFile");
                    SyncError = message;
                    return message;
                }

                byte[] bytes = _environment.ReadAllBytes(oneDriveFile);
                _environment.CreateDirectory(localFolder);
                string existing = FindSnippetFile(localFolder);
                if (existing == null || string.Equals(existing, localFile, StringComparison.OrdinalIgnoreCase))
                {
                    WriteAtomically(localFile, bytes, existing == null ? null : BackupPath(existing), replace: existing != null);
                }
                else
                {
                    // A file with the earlier name: write the copy first, so this PC is never
                    // without a snippet file, then keep the old one as its backup.
                    WriteAtomically(localFile, bytes, backup: null, replace: false);
                    _environment.MoveFile(existing, BackupPath(existing));
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                SyncError = ex.Message;
                return ex.Message;
            }

            _changingFolder = true;
            try
            {
                _owner.SnippetsPath = string.Empty;
            }
            finally
            {
                _changingFolder = false;
            }

            Refresh();
            Announce(_getResourceString("TextExpander_Sync_AnnounceStopped"));
            return null;
        }

        /// <summary>
        /// Gets what Stop syncing does, for its confirmation. When this PC already has a snippet
        /// file of its own, names the backup <see cref="StopSyncing"/> keeps it as.
        /// </summary>
        public string DescribeStopSyncing()
        {
            string backup = null;
            if (_owner.CanEditSnippets)
            {
                try
                {
                    string existing = FindSnippetFile(_environment.DefaultSnippetsFolder);
                    backup = existing == null ? null : Path.GetFileName(BackupPath(existing));
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    backup = null;
                }
            }

            return backup == null
                ? _getResourceString("TextExpander_Sync_StopDialog_Content")
                : Format("TextExpander_Sync_StopDialog_ContentBackup", backup);
        }

        /// <summary>Starts the OneDrive app so the user can sign in.</summary>
        public string OpenOneDrive()
        {
            if (!_owner.CanEditSnippets)
            {
                return _getResourceString("TextExpander_ElevatedSnippetAccessDenied");
            }

            string executable = _environment.FindOneDriveExecutable();
            if (string.IsNullOrEmpty(executable))
            {
                CanOpenOneDrive = false;
                return null;
            }

            try
            {
                _environment.StartProcess(executable);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                SyncError = ex.Message;
                return ex.Message;
            }

            return null;
        }

        public void Dispose()
        {
            _owner.PropertyChanged -= Owner_PropertyChanged;
            StopWaiting();
            CancelSearch();
        }

        internal static bool HasCloudOnlyContent(FileAttributes attributes)
            => (attributes & (RecallOnDataAccessAttribute | FileAttributes.Offline)) != 0;

        /// <summary>The attributes File Explorer's "Always keep on this device" would leave on the file.</summary>
        internal static FileAttributes WithPinState(FileAttributes current, bool keepOnThisPc)
        {
            FileAttributes settable = current & SettableAttributes;
            settable = keepOnThisPc ? (settable | PinnedAttribute) & ~UnpinnedAttribute : settable & ~PinnedAttribute;
            return settable == 0 ? FileAttributes.Normal : settable;
        }

        /// <summary>Called by the folder watcher, on the UI thread.</summary>
        internal void CheckWaitingTarget()
        {
            if (!_isWaiting || !_owner.CanEditSnippets)
            {
                return;
            }

            TextExpanderSyncAccount account = _waitingAccount;
            string folder = _waitingFolder;

            try
            {
                if (FindSnippetFile(folder) == null)
                {
                    return;
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return;
            }

            StopWaiting();
            string error = SwitchTo(account, folder);
            if (error != null)
            {
                SyncError = error;
                return;
            }

            Announce(Format("TextExpander_Sync_AnnounceArrived", account.DisplayName));
        }

        private static bool IsFileError(Exception ex)
            => ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

        private static string StandardFolder(TextExpanderSyncAccount account) => Path.Combine(account.Root, "PowerToys", "TextExpander");

        private static string NormalizeFolder(string folder)
        {
            try
            {
                return Path.IsPathFullyQualified(folder) ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static string ToSettingValue(TextExpanderSyncAccount account, string folder)
        {
            string full = NormalizeFolder(folder);
            string relative = full.Length > account.Root.Length ? full[(account.Root.Length + 1)..] : string.Empty;
            return relative.Length == 0 ? account.VariablePath : account.VariablePath + Path.DirectorySeparatorChar + relative;
        }

        private static bool SameAccount(TextExpanderSyncAccount first, TextExpanderSyncAccount second)
            => first != null && second != null
                && first.VariableName == second.VariableName
                && string.Equals(first.Root, second.Root, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the way to <paramref name="file"/> from its account's folder, as File Explorer names
        /// the folders: "OneDrive - Contoso › PowerToys › TextExpander › PowerToysTextExpanderSnippets.txt".
        /// </summary>
        private static string LocationOf(TextExpanderSyncAccount account, string file)
        {
            string rootName = Path.GetFileName(account.Root);
            string relative = file.Length > account.Root.Length ? file[(account.Root.Length + 1)..] : string.Empty;
            IEnumerable<string> parts = new[] { string.IsNullOrEmpty(rootName) ? account.Root : rootName }
                .Concat(relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
            return string.Join(LocationSeparator, parts);
        }

        private void Owner_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_changingFolder || !(_hasRefreshed || _statusLoaded) || e.PropertyName != nameof(TextExpanderViewModel.SnippetsFilePath) || !_owner.CanEditSnippets)
            {
                return;
            }

            // A different folder can change everything; an edit to the same file only changes counts.
            if (!string.Equals(_lastResolvedFile, _owner.SnippetsFilePath, StringComparison.OrdinalIgnoreCase))
            {
                if (_hasRefreshed)
                {
                    Refresh();
                }
                else
                {
                    RefreshStatus();
                }
            }
            else
            {
                UpdateCounts();
            }
        }

        private void DiscoverAccounts()
        {
            var found = new List<TextExpanderSyncAccount>();
            AddAccount(found, ConsumerVariable);
            AddAccount(found, CommercialVariable);
            if (found.Count == 0)
            {
                AddAccount(found, GenericVariable);
            }

            string previous = SelectedAccount?.VariableName;
            bool changed = found.Count != Accounts.Count
                || found.Where((account, i) => account.VariableName != Accounts[i].VariableName || !string.Equals(account.Root, Accounts[i].Root, StringComparison.OrdinalIgnoreCase)).Any();

            _suppressEvaluate = true;
            try
            {
                if (changed)
                {
                    Accounts.Clear();
                    foreach (TextExpanderSyncAccount account in found)
                    {
                        Accounts.Add(account);
                    }
                }

                int index = previous == null ? -1 : found.FindIndex(account => account.VariableName == previous);
                SelectedAccountIndex = index >= 0 ? index : (found.Count > 0 ? 0 : -1);
            }
            finally
            {
                _suppressEvaluate = false;
            }

            OnPropertyChanged(nameof(HasMultipleAccounts));
            OnPropertyChanged(nameof(CanCopy));

            IsOneDriveMissing = found.Count == 0;
            CanOpenOneDrive = found.Count == 0 && !string.IsNullOrEmpty(_environment.FindOneDriveExecutable());
            OnPropertyChanged(nameof(CanToggle));
        }

        private void AddAccount(List<TextExpanderSyncAccount> accounts, string variable)
        {
            string value = _environment.GetEnvironmentVariable(variable)?.Trim().Trim('"');
            string root = string.IsNullOrEmpty(value) ? null : NormalizeFolder(value);
            if (root == null
                || accounts.Any(account => string.Equals(account.Root, root, StringComparison.OrdinalIgnoreCase))
                || !_environment.DirectoryExists(root))
            {
                return;
            }

            accounts.Add(new TextExpanderSyncAccount(variable, root, DescribeAccount(variable, root)));
        }

        private string DescribeAccount(string variable, string root)
        {
            if (variable == ConsumerVariable)
            {
                return _getResourceString("TextExpander_Sync_AccountPersonal");
            }

            // Work and school folders are named "OneDrive - <organization>".
            string leaf = Path.GetFileName(root);
            if (leaf.StartsWith(WorkFolderPrefix, StringComparison.OrdinalIgnoreCase) && leaf.Length > WorkFolderPrefix.Length)
            {
                return Format("TextExpander_Sync_AccountWork", leaf[WorkFolderPrefix.Length..].Trim());
            }

            return _getResourceString(variable == CommercialVariable ? "TextExpander_Sync_AccountWorkUnnamed" : "TextExpander_Sync_AccountGeneric");
        }

        private TextExpanderSyncAccount FindAccountContaining(string folder)
        {
            string full = string.IsNullOrEmpty(folder) ? null : NormalizeFolder(folder);
            if (full == null)
            {
                return null;
            }

            return Accounts.FirstOrDefault(account =>
                string.Equals(full, account.Root, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(account.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshSyncedState()
        {
            string file = _owner.SnippetsFilePath;
            _lastResolvedFile = file;
            TextExpanderSyncAccount account = string.IsNullOrEmpty(file) ? null : FindAccountContaining(Path.GetDirectoryName(file));

            // The snippets just left OneDrive: Stop syncing, or the Folder row was cleared or changed.
            // Remember where they were, so turning sync back on finds them there.
            if (account == null && _syncedAccount != null && !string.IsNullOrEmpty(_syncedFilePath))
            {
                RememberSyncFolder(_syncedAccount, Path.GetDirectoryName(_syncedFilePath));
            }

            _syncedAccount = account;
            IsSynced = account != null;
            if (account == null)
            {
                SyncedFilePath = string.Empty;
                SyncedStatusText = string.Empty;
                IsOnlineOnly = false;
                ShowKeepCopyOption = false;
                SetKeepCopyField(false);
                return;
            }

            StopWaiting();

            // The folder has switched, so the setup is finished and the toggle settles On.
            IsSetupRequested = false;
            SyncedFilePath = file;
            RefreshPinState(file);
        }

        private void UpdateCounts()
        {
            int count = _owner.Snippets.Count;
            FirstPcDescription = Plural("TextExpander_Sync_FirstPcDescription", count, count);
            if (_syncedAccount != null)
            {
                // A file that can't be read has no meaningful count.
                SyncedStatusText = _owner.HasSnippetError
                    ? Format("TextExpander_Sync_SyncedCountUnavailable", _syncedAccount.DisplayName)
                    : Plural("TextExpander_Sync_Synced", count, _syncedAccount.DisplayName, count);
            }

            if (_hasConflict)
            {
                ReplaceChoiceText = Plural("TextExpander_Sync_ConflictReplace", count, count);
            }
        }

        private void RefreshPinState(string file)
        {
            FileAttributes attributes = 0;
            bool exists;
            try
            {
                exists = _environment.FileExists(file);
                if (exists)
                {
                    attributes = _environment.GetAttributes(file);
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                exists = false;
            }

            bool pinned = exists && (attributes & PinnedAttribute) != 0;
            bool cloudOnly = exists && HasCloudOnlyContent(attributes);
            IsOnlineOnly = cloudOnly && !pinned;
            ShowKeepCopyOption = cloudOnly || pinned;
            SetKeepCopyField(pinned);
        }

        private void ApplyKeepCopy(bool keep)
        {
            if (!_owner.CanEditSnippets || !_isSynced || keep == _keepCopyOnThisPc)
            {
                OnPropertyChanged(nameof(KeepCopyOnThisPc));
                return;
            }

            string file = _syncedFilePath;
            try
            {
                FileAttributes current = _environment.GetAttributes(file);
                _environment.SetAttributes(file, WithPinState(current, keep));
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                SyncError = ex.Message;
                OnPropertyChanged(nameof(KeepCopyOnThisPc));
                return;
            }

            RefreshPinState(file);
            if (keep)
            {
                Announce(_getResourceString("TextExpander_Sync_AnnounceKeepCopy"));
            }
        }

        private void SetKeepCopyField(bool value) => Set(ref _keepCopyOnThisPc, value, nameof(KeepCopyOnThisPc));

        private void Evaluate()
        {
            if (_suppressEvaluate)
            {
                return;
            }

            StopWaiting();
            ClearSetupResults();

            // Only once the user has turned the toggle on: checking the destination reads
            // the snippet file in OneDrive, which downloads it if it is online-only.
            if (!_owner.CanEditSnippets || !_hasRefreshed || !_isSetupRequested || _isSynced || _isOneDriveMissing)
            {
                return;
            }

            try
            {
                if (_setupKindIndex == FirstPcIndex)
                {
                    TextExpanderSyncAccount account = SelectedAccount;
                    string target = account == null ? null : FindSnippetFile(DestinationFolder(account));
                    if (target != null)
                    {
                        ShowConflict(target);
                    }
                }
                else if (_setupKindIndex == AnotherPcIndex)
                {
                    LookForSnippets();
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                SyncError = ex.Message;
            }
        }

        private void ClearSetupResults()
        {
            HasConflict = false;
            ConflictTitle = string.Empty;
            IsFound = false;
            FoundText = string.Empty;
            FoundLocationText = string.Empty;
            FoundFilePath = string.Empty;
            _foundAccount = null;
            _foundFolder = null;

            // A search still running belongs to choices that no longer apply.
            CancelSearch();
            if (Candidates.Count > 0)
            {
                Candidates.Clear();
                OnPropertyChanged(nameof(HasCandidates));
            }

            SelectedCandidateIndex = -1;
        }

        private void CancelSearch()
        {
            _searchVersion++;
            if (_searchCancellation != null)
            {
                _searchCancellation.Cancel();
                _searchCancellation.Dispose();
                _searchCancellation = null;
            }

            IsSearching = false;
        }

        private void ShowConflict(string target)
        {
            int oneDriveCount = CountSnippets(target);
            ConflictTitle = Plural("TextExpander_Sync_ConflictTitle", oneDriveCount, oneDriveCount);
            UseOneDriveChoiceText = Plural("TextExpander_Sync_ConflictUse", oneDriveCount, oneDriveCount);
            ReplaceChoiceText = Plural("TextExpander_Sync_ConflictReplace", _owner.Snippets.Count, _owner.Snippets.Count);
            KeepBothDescription = Format("TextExpander_Sync_ConflictKeepBothDescription", PcCopyBaseName(target) + ".txt");
            ConflictChoiceIndex = UseOneDriveChoice;
            HasConflict = true;
        }

        /// <summary>
        /// Another PC: looks where a setup puts the snippets, first the folder they were last synced
        /// in, then the standard folder, and if neither has them, searches OneDrive. A folder the
        /// user chose is the answer on its own: if it has no snippets, nothing else is searched.
        /// It all runs off the UI thread, with the spinner showing, because counting a file in
        /// OneDrive reads it, which downloads it if it's online-only. Changing a choice, Cancel,
        /// turning the toggle off and leaving the page all cancel it.
        /// </summary>
        private void LookForSnippets()
        {
            TextExpanderSyncAccount account = _customAccount ?? SelectedAccount;
            if (account == null)
            {
                return;
            }

            CancelSearch();
            int version = _searchVersion;
            var cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            IsSearching = true;

            string chosen = _customFolder;
            var folders = new List<string>();
            var roots = new List<string>();
            var accounts = Accounts.ToList();
            if (chosen != null)
            {
                folders.Add(chosen);
            }
            else
            {
                if (_rememberedFolder != null && SameAccount(account, _rememberedAccount))
                {
                    folders.Add(_rememberedFolder);
                }

                folders.Add(StandardFolder(account));

                // The selected account first, then the others.
                roots.Add(account.Root);
                roots.AddRange(accounts.Where(other => !SameAccount(other, account)).Select(other => other.Root));
            }

            CancellationToken token = cancellation.Token;
            LookOutcome outcome = null;
            RunInBackground(() => outcome = Look(account.Root, folders, roots, token)).ContinueWith(
                task => Dispatch(() => FinishLooking(version, account, accounts, chosen, task.IsCompletedSuccessfully ? outcome : null)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Runs off the UI thread. Checks <paramref name="folders"/> in order, then searches
        /// <paramref name="roots"/>, which reads folder listings only. Then only the file offered
        /// first is read, for its count; if it can't be read, the next newest is offered first.
        /// </summary>
        private LookOutcome Look(string accountRoot, IReadOnlyList<string> folders, IReadOnlyList<string> roots, CancellationToken cancellation)
        {
            var nothing = new LookOutcome(new List<TextExpanderSnippetMatch>(), 0, null);
            try
            {
                foreach (string folder in folders)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return nothing;
                    }

                    string file = FindSnippetFile(folder);
                    if (file != null)
                    {
                        var match = new TextExpanderSnippetMatch(accountRoot, file, _environment.GetLastWriteTime(file));
                        return new LookOutcome(new List<TextExpanderSnippetMatch> { match }, CountSnippets(file), null);
                    }
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return new LookOutcome(new List<TextExpanderSnippetMatch>(), 0, ex.Message);
            }

            if (roots.Count == 0 || cancellation.IsCancellationRequested)
            {
                return nothing;
            }

            var clock = Stopwatch.StartNew();
            TextExpanderSnippetSearchResult result = TextExpanderSnippetSearch.Search(
                roots,
                folder => _environment.ListFolder(folder, SnippetFileNames),
                () => clock.Elapsed,
                cancellation);

            var matches = result.Matches.ToList();
            while (matches.Count > 0 && !cancellation.IsCancellationRequested)
            {
                try
                {
                    int count = CountSnippets(matches[0].FilePath);
                    return new LookOutcome(matches.Take(MaxCandidates).ToList(), count, null);
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    // Gone or unreadable since it was listed.
                    matches.RemoveAt(0);
                }
            }

            return nothing;
        }

        private void ShowFound(TextExpanderSyncAccount account, string file, int count, DateTime changed)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            FoundText = Plural(
                "TextExpander_Sync_Found",
                count,
                account.DisplayName,
                count,
                changed.ToString("M", culture),
                changed.ToString("t", culture));
            FoundLocationText = Format("TextExpander_Sync_FoundLocation", LocationOf(account, file));
            FoundFilePath = file;
            _foundAccount = account;
            _foundFolder = Path.GetDirectoryName(file);
            IsFound = true;
        }

        /// <summary>
        /// Nothing found: says so, and keeps watching <paramref name="folder"/> while the page is open,
        /// in case OneDrive is still downloading the snippets. The folder doesn't need to exist yet.
        /// </summary>
        private void StartWaiting(TextExpanderSyncAccount account, string folder, bool chosen)
        {
            _waitingAccount = account;
            _waitingFolder = folder;
            string full = NormalizeFolder(folder) ?? folder;
            WaitingLocationText = Format(chosen ? "TextExpander_Sync_WaitingLocation" : "TextExpander_Sync_WaitingDefaultLocation", LocationOf(account, full));
            WaitingFilePath = Path.Combine(full, TextExpanderSnippetFileNames.Current);
            NotFoundMessage = _getResourceString(chosen ? "TextExpander_Sync_NotFoundChosenMessage" : "TextExpander_Sync_NotFoundMessage");
            IsWaiting = true;
            _watch = _environment.WatchForFiles(folder, SnippetFileNames, () => Dispatch(CheckWaitingTarget));
            Announcement?.Invoke(this, _getResourceString("TextExpander_Sync_AnnounceNotFound"));
        }

        /// <summary>Shows what was found: one file as found, several as a list with the newest selected, none as not found. Each is announced.</summary>
        private void FinishLooking(int version, TextExpanderSyncAccount account, IReadOnlyList<TextExpanderSyncAccount> accounts, string chosen, LookOutcome outcome)
        {
            if (version != _searchVersion || !_owner.CanEditSnippets || !_isSetupRequested || _isSynced || _setupKindIndex != AnotherPcIndex)
            {
                return;
            }

            CancelSearch();
            if (outcome?.Error != null)
            {
                SyncError = outcome.Error;
                return;
            }

            List<TextExpanderSnippetMatch> matches = outcome?.Matches ?? new List<TextExpanderSnippetMatch>();
            if (matches.Count == 0)
            {
                StartWaiting(account, chosen ?? DestinationFolder(account), chosen != null);
                return;
            }

            TextExpanderSyncAccount AccountOf(TextExpanderSnippetMatch match)
                => accounts.FirstOrDefault(candidate => string.Equals(candidate.Root, match.Root, StringComparison.OrdinalIgnoreCase)) ?? account;

            if (matches.Count == 1)
            {
                ShowFound(AccountOf(matches[0]), matches[0].FilePath, outcome.Count, matches[0].LastWriteTime);
                Announcement?.Invoke(this, FoundText);
                return;
            }

            CultureInfo culture = CultureInfo.CurrentCulture;
            for (int i = 0; i < matches.Count; i++)
            {
                TextExpanderSnippetMatch match = matches[i];
                TextExpanderSyncAccount matchAccount = AccountOf(match);
                string changed = match.LastWriteTime.ToString("M", culture);

                // Only the first was read, so only it has a count.
                string details = i == 0 ? Plural("TextExpander_Sync_CandidateDetails", outcome.Count, outcome.Count, changed) : changed;
                string location = LocationOf(matchAccount, match.FilePath);
                string folderName = Path.GetFileName(Path.GetDirectoryName(match.FilePath));
                string automationName = string.Join(
                    ", ",
                    new[] { folderName, details.Replace(" \u00B7 ", ", ", StringComparison.Ordinal), location.Replace(LocationSeparator, ", ", StringComparison.Ordinal) });
                Candidates.Add(new TextExpanderSyncCandidate(matchAccount, match.FilePath, folderName, location, details, automationName));
            }

            OnPropertyChanged(nameof(HasCandidates));
            SelectedCandidateIndex = 0;

            // Only announced: the list itself is the visible result.
            Announcement?.Invoke(this, Plural("TextExpander_Sync_AnnounceSearchFound", matches.Count, matches.Count));
        }

        private string SwitchTo(TextExpanderSyncAccount account, string folder)
        {
            // The engine writes starter snippets into a folder without a snippet file, so the file
            // must be there before the setting points at it.
            if (FindSnippetFile(folder) == null)
            {
                return _getResourceString("TextExpander_Sync_FileMissing");
            }

            _changingFolder = true;
            try
            {
                _owner.SnippetsPath = ToSettingValue(account, folder);
            }
            finally
            {
                _changingFolder = false;
            }

            _customFolder = null;
            _customAccount = null;
            _rememberedFolder = null;
            _rememberedAccount = null;
            Refresh();
            return null;
        }

        /// <summary>
        /// Returns the folder the setup uses in <paramref name="account"/>: the one the snippets were
        /// last synced in, when it's in that account and still has them, otherwise the standard
        /// PowerToys\TextExpander folder.
        /// </summary>
        private string DestinationFolder(TextExpanderSyncAccount account)
            => _rememberedFolder != null && SameAccount(account, _rememberedAccount) ? _rememberedFolder : StandardFolder(account);

        /// <summary>
        /// Looks for the OneDrive folder the snippets were last synced in. If it's in an account on
        /// this PC and still has a snippet file, under either name, the setup uses it in place of the
        /// standard folder for both choices, and selects its account. This only checks that the file
        /// exists, which doesn't download it.
        /// </summary>
        private void FindRememberedFolder()
        {
            _rememberedAccount = null;
            _rememberedFolder = null;

            string folder = ResolveSettingFolder(_owner.LastSyncFolder);
            TextExpanderSyncAccount account = folder == null ? null : FindAccountContaining(folder);
            try
            {
                if (account == null || FindSnippetFile(folder) == null)
                {
                    return;
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return;
            }

            _rememberedAccount = account;
            _rememberedFolder = folder;

            _suppressEvaluate = true;
            try
            {
                SelectedAccountIndex = Accounts.IndexOf(account);
            }
            finally
            {
                _suppressEvaluate = false;
            }
        }

        /// <summary>Saves <paramref name="folder"/> as the OneDrive folder the snippets were last synced in, written the way <see cref="SwitchTo"/> writes a folder.</summary>
        private void RememberSyncFolder(TextExpanderSyncAccount account, string folder)
        {
            if (NormalizeFolder(folder) != null)
            {
                _owner.LastSyncFolder = ToSettingValue(account, folder);
            }
        }

        /// <summary>
        /// Resolves a setting written like the snippet folder: quotes trimmed, environment variables
        /// expanded, and the folder of a file if it names one. Null when it can't be used, for
        /// example a relative path.
        /// </summary>
        private string ResolveSettingFolder(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                string full = NormalizeFolder(_environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
                return full != null && _environment.FileExists(full) ? Path.GetDirectoryName(full) : full;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return null;
            }
        }

        /// <summary>Counts the snippets in <paramref name="file"/>, read the way the page reads it: any byte order mark, and CR, LF or CRLF line breaks.</summary>
        private int CountSnippets(string file)
        {
            string text = TextExpanderSnippetEncoding.Decode(_environment.ReadAllBytes(file), out _);
            return TextExpanderSnippetFile.Parse(TextExpanderViewModel.SplitLines(text)).Snippets.Count;
        }

        private byte[] ReadSnippetsOrEmpty(string source)
            => !string.IsNullOrEmpty(source) && _environment.FileExists(source) ? _environment.ReadAllBytes(source) : Array.Empty<byte>();

        /// <summary>The name, without extension, of this PC's copy kept next to <paramref name="file"/>, such as "snippets (LAPTOP)".</summary>
        private string PcCopyBaseName(string file)
            => string.Format(CultureInfo.InvariantCulture, "{0} ({1})", Path.GetFileNameWithoutExtension(file), _environment.MachineName);

        /// <summary>A free backup name next to <paramref name="file"/>, such as "snippets.backup.txt".</summary>
        private string BackupPath(string file)
            => UniqueFilePath(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".backup", ".txt");

        /// <summary>The snippet file in <paramref name="folder"/> under either name, or null.</summary>
        private string FindSnippetFile(string folder) => TextExpanderSnippetFileNames.FindInFolder(folder, _environment.FileExists);

        private string UniqueFilePath(string folder, string baseName, string extension)
        {
            string candidate = Path.Combine(folder, baseName + extension);
            for (int n = 2; _environment.FileExists(candidate); n++)
            {
                candidate = Path.Combine(folder, string.Format(CultureInfo.InvariantCulture, "{0} ({1}){2}", baseName, n, extension));
            }

            return candidate;
        }

        /// <summary>Writes a temporary file next to <paramref name="target"/> and swaps it in, so readers never see a partial file.</summary>
        private void WriteAtomically(string target, byte[] bytes, string backup, bool replace)
        {
            string temp = Path.Combine(
                Path.GetDirectoryName(target),
                string.Format(CultureInfo.InvariantCulture, ".{0}.{1:N}.tmp", Path.GetFileName(target), Guid.NewGuid()));

            _environment.WriteNewFile(temp, bytes);
            try
            {
                if (replace)
                {
                    _environment.ReplaceFile(temp, target, backup);
                }
                else
                {
                    _environment.MoveFile(temp, target);
                }
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                if (_environment.FileExists(path))
                {
                    _environment.DeleteFile(path);
                }
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                // A leftover temporary file is harmless; the original error matters more.
            }
        }

        private void ResetForElevated()
        {
            StopWaiting();
            _rememberedFolder = null;
            _rememberedAccount = null;
            _suppressEvaluate = true;
            try
            {
                Accounts.Clear();
                SelectedAccountIndex = -1;
            }
            finally
            {
                _suppressEvaluate = false;
            }

            IsSynced = false;
            IsOneDriveMissing = false;
            CanOpenOneDrive = false;
            IsSetupRequested = false;
            ClearSetupResults();
            OnPropertyChanged(nameof(CanToggle));
        }

        /// <summary>Gets or sets a value indicating whether the user turned the toggle on and the setup isn't finished.</summary>
        private bool IsSetupRequested
        {
            get => _isSetupRequested;
            set
            {
                if (_isSetupRequested != value)
                {
                    _isSetupRequested = value;
                    RaiseToggleState();
                }
            }
        }

        /// <summary>Ends the setup and forgets its choices, so turning the toggle on again starts afresh.</summary>
        private void EndSetup()
        {
            StopWaiting();
            _customFolder = null;
            _customAccount = null;
            _rememberedFolder = null;
            _rememberedAccount = null;
            IsSetupRequested = false;
            ClearSetupResults();
            SyncError = null;

            _suppressEvaluate = true;
            try
            {
                SetupKindIndex = FirstPcIndex;
            }
            finally
            {
                _suppressEvaluate = false;
            }
        }

        private void RaiseToggleState()
        {
            OnPropertyChanged(nameof(IsSyncOn));
            OnPropertyChanged(nameof(CanToggle));
            OnPropertyChanged(nameof(IsSetupVisible));
            OnPropertyChanged(nameof(ShowDetails));
        }

        private string Fail(string resourceKey)
        {
            string message = _getResourceString(resourceKey);
            SyncError = message;
            return message;
        }

        private string Format(string resourceKey, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, _getResourceString(resourceKey), args);

        private string Plural(string resourceKey, int count, params object[] args)
            => Format(count == 1 ? resourceKey + "_One" : resourceKey, args);

        // Announced only: nothing stays under the toggle, whose summary already says what's in use.
        private void Announce(string message) => Announcement?.Invoke(this, message);

        /// <summary>What looking for the snippets found, newest first, the snippet count of the first, the only one read, or why it couldn't look.</summary>
        private sealed record LookOutcome(List<TextExpanderSnippetMatch> Matches, int Count, string Error);
    }
}
