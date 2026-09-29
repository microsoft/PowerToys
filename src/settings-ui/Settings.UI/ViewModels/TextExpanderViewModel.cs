// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class TextExpanderViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private const string SnippetsFileName = "snippets.txt";

        /// <summary>
        /// The variables the engine implements in <c>Variables.Expand</c>. The sample is what the
        /// value looks like now, so the menu answers "what will I get?" rather than only naming
        /// a token.
        /// </summary>
        private static readonly TextExpanderVariable[] VariableDefinitions =
        {
            new TextExpanderVariable("Cursor position", "$|$", null),
            new TextExpanderVariable("Date", "{{date}}", "MM/dd/yy"),
            new TextExpanderVariable("Time", "{{time}}", "hh:mm tt"),
            new TextExpanderVariable("Date and time", "{{datetime}}", "MM/dd/yy hh:mm tt"),
            new TextExpanderVariable("ISO date", "{{isodate}}", "yyyy-MM-dd"),
            new TextExpanderVariable("Custom date format", "{{date:dddd}}", "dddd"),
            new TextExpanderVariable("Clipboard contents", "{{clipboard}}", null),
            new TextExpanderVariable("Ask me when it runs", "{{prompt:Label}}", null),
        };

        private readonly TextExpanderSettings _settings;
        private readonly SettingsUtils _settingsUtils;
        private readonly ISettingsRepository<GeneralSettings> _settingsRepository;
        private readonly bool _enabledStateIsGPOConfigured;
        private readonly Func<Task<string>> _pickSingleFolderDialog;

        private Func<string, int> SendConfigMSG { get; }

        private TextExpanderSnippetFile _file;
        private string _snippetError;
        private byte[] _loadedBytes;
        private string _newline = Environment.NewLine;
        private bool _hadBom;
        private bool _hadFinalNewline = true;
        private bool _isEnabled;

        /// <summary>
        /// Opens a folder browser for the snippet folder.
        ///
        /// <para>
        /// A picker rather than an upload: the setting points the engine at a folder it keeps
        /// watching, so the file has to stay where it is. Copying it in would sever it from
        /// whatever is syncing it — a OneDrive folder being the obvious case — and leave the
        /// user editing a copy while expecting the original.
        /// </para>
        /// </summary>
        public ButtonClickCommand BrowseForSnippetsFolderCommand { get; private set; }

        public TextExpanderViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc, Func<Task<string>> pickSingleFolderDialog = null)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _settingsRepository = settingsRepository;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            SendConfigMSG = ipcMSGCallBackFunc;
            _pickSingleFolderDialog = pickSingleFolderDialog;
            BrowseForSnippetsFolderCommand = new ButtonClickCommand(BrowseForSnippetsFolder);

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

        public bool IsEnabledGpoConfigured => _enabledStateIsGPOConfigured;

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
                    Save();
                }
            }
        }

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
                string path = value ?? string.Empty;
                if (_settings.Properties.SnippetsPath.Value != path)
                {
                    _settings.Properties.SnippetsPath.Value = path;
                    OnPropertyChanged(nameof(SnippetsPath));
                    Save();

                    // The folder moved, so the list on screen is now showing the wrong file.
                    LoadSnippets();
                }
            }
        }

        /// <summary>
        /// Sets <see cref="SnippetsPath"/> from a folder the user picks.
        ///
        /// <para>
        /// Silently does nothing when no picker was supplied or the dialog is cancelled, so the
        /// existing path survives a dismissed dialog rather than being blanked.
        /// </para>
        /// </summary>
        private async void BrowseForSnippetsFolder()
        {
            if (_pickSingleFolderDialog is null)
            {
                return;
            }

            try
            {
                string picked = await _pickSingleFolderDialog();
                if (!string.IsNullOrEmpty(picked))
                {
                    SnippetsPath = picked;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error picking the Text Expander snippet folder.", ex);
            }
        }

        /// <summary>Gets the snippets shown in the page, in file order.</summary>
        public ObservableCollection<TextExpanderSnippetViewModel> Snippets { get; } = new ObservableCollection<TextExpanderSnippetViewModel>();

        /// <summary>Gets a value indicating whether the snippet file could not be read or written.</summary>
        public bool HasSnippetError => !string.IsNullOrEmpty(_snippetError);

        /// <summary>Gets the reason the snippet file could not be read or written.</summary>
        public string SnippetError => _snippetError ?? string.Empty;

        /// <summary>Gets a value indicating whether there are no snippets to show.</summary>
        public bool HasNoSnippets => Snippets.Count == 0 && !HasSnippetError;

        /// <summary>Gets the resolved snippet file, shown so the user can find it outside the app.</summary>
        public string SnippetsFilePath => ResolveSnippetsFile();

        /// <summary>
        /// Gets the variables the engine can expand, for the Insert variable menu.
        ///
        /// <para>
        /// Deliberately only the ones <c>Variables.Expand</c> actually implements, so the UI does not advertise variables the engine would ignore.
        /// </para>
        /// </summary>
        public IReadOnlyList<TextExpanderVariable> Variables => VariableDefinitions;

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
            string configured = _settings.Properties.SnippetsPath.Value;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                try
                {
                    string expanded = Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));

                    // Must match the engine's rule exactly. A relative path is resolved against
                    // the *process* working directory, and the engine runs in a different process
                    // from this one -- so "snips" would mean two different folders, and the user
                    // would edit one file while the engine expanded from another with nothing on
                    // screen to say so. Neither side rebases it; both fall back.
                    if (Path.IsPathFullyQualified(expanded))
                    {
                        string full = Path.GetFullPath(expanded);

                        // Accept a file too: someone will paste the path of snippets.txt itself.
                        return File.Exists(full) ? full : Path.Combine(full, SnippetsFileName);
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
            // both sides land on one file without a second setting to disagree about.
            return Path.Combine(
                Helper.LocalApplicationDataFolder(),
                "Microsoft",
                "PowerToys",
                TextExpanderSettings.ModuleName,
                SnippetsFileName);
        }

        /// <summary>Reads the snippet file into <see cref="Snippets"/>.</summary>
        public void LoadSnippets()
        {
            _snippetError = null;
            Snippets.Clear();

            try
            {
                string path = ResolveSnippetsFile();

                if (!File.Exists(path))
                {
                    // Not an error: nothing has been written yet. The engine creates the file
                    // with its own header on first run, and so does the first save here.
                    _file = TextExpanderSnippetFile.Parse(Array.Empty<string>());
                    _loadedBytes = null;
                    _hadBom = false;
                    _newline = Environment.NewLine;
                    _hadFinalNewline = true;
                }
                else
                {
                    // Read the raw bytes so the file's own conventions survive a save. Reading
                    // only decoded lines would silently re-encode it: an existing BOM would be
                    // dropped and LF endings rewritten to CRLF, which is whole-file churn for
                    // anyone keeping their snippets in git.
                    _loadedBytes = ReadAllBytesShared(path);
                    _hadBom = HasUtf8Bom(_loadedBytes);

                    string text = new System.Text.UTF8Encoding(false, false)
                        .GetString(_loadedBytes, _hadBom ? 3 : 0, _loadedBytes.Length - (_hadBom ? 3 : 0));

                    _newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
                        : text.Contains('\n', StringComparison.Ordinal) ? "\n"
                        : Environment.NewLine;
                    _hadFinalNewline = text.Length == 0 || text.EndsWith('\n') || text.EndsWith('\r');

                    _file = TextExpanderSnippetFile.Parse(SplitLines(text));
                }

                foreach (TextExpanderSnippetFile.Entry entry in _file.Snippets)
                {
                    Snippets.Add(new TextExpanderSnippetViewModel(entry.Id, entry.Trigger, entry.Replacement));
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
            }

            RaiseSnippetListChanged();
        }

        /// <summary>
        /// Adds or updates a snippet and writes the file. Pass null as <paramref name="id"/> to
        /// add. Returns null on success, or the reason it was refused.
        /// </summary>
        public string SaveSnippet(int? id, string trigger, string replacement)
        {
            trigger = trigger ?? string.Empty;
            replacement = replacement ?? string.Empty;

            if (!TextExpanderSnippetFile.IsValidTrigger(trigger, out string problem))
            {
                return problem;
            }

            // A block body cannot contain its own terminator, and there is no escape for it, so
            // this really is unrepresentable rather than merely awkward.
            if (!TextExpanderSnippetFile.IsValidReplacement(replacement, out string replacementProblem))
            {
                return replacementProblem;
            }

            if (_file == null)
            {
                return SnippetError;
            }

            TextExpanderSnippetFile.Entry clash = _file.FindDuplicate(trigger, ignoringId: id);
            if (clash != null)
            {
                // Last-one-wins in the engine, so letting this through would quietly kill one of
                // the two snippets with no sign anything was wrong.
                return string.Format(CultureInfo.CurrentCulture, "Another snippet already uses the trigger '{0}'.", trigger);
            }

            try
            {
                _file.Upsert(id, trigger, replacement);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // The model enforces the same rules independently; surface rather than crash.
                return ex.Message;
            }

            return WriteFile();
        }

        /// <summary>Deletes the snippet with <paramref name="id"/> and writes the file.</summary>
        public string DeleteSnippet(int id)
        {
            if (_file == null)
            {
                return SnippetError;
            }

            _file.Remove(id);
            return WriteFile();
        }

        /// <summary>
        /// Copies a snippet under a free trigger, so a variation does not have to be retyped.
        /// Returns null on success.
        /// </summary>
        public string DuplicateSnippet(int id)
        {
            if (_file == null)
            {
                return SnippetError;
            }

            TextExpanderSnippetFile.Entry source = _file.Snippets.FirstOrDefault(s => s.Id == id);
            if (source == null)
            {
                return null;
            }

            string candidate = source.Trigger + "2";
            for (int n = 2; _file.FindDuplicate(candidate) != null; n++)
            {
                candidate = source.Trigger + n.ToString(CultureInfo.InvariantCulture);
            }

            try
            {
                _file.Upsert(null, candidate, source.Replacement);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return ex.Message;
            }

            return WriteFile();
        }

        /// <summary>
        /// Renders a replacement the way the engine would, for the editor's preview.
        ///
        /// <para>
        /// Only the variables that resolve without asking anything are substituted.
        /// <c>{{prompt:}}</c> and <c>{{clipboard}}</c> are shown as a description of what will
        /// happen, because faking a value would preview a result the user will never see.
        /// </para>
        /// </summary>
        public static string PreviewReplacement(string replacement)
        {
            if (string.IsNullOrEmpty(replacement))
            {
                return string.Empty;
            }

            DateTime now = DateTime.Now;
            string text = replacement
                .Replace("{{date}}", now.ToString("MM/dd/yy", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{time}}", now.ToString("hh:mm tt", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{datetime}}", now.ToString("MM/dd/yy hh:mm tt", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{isodate}}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal);

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\{\{date:([^}]*)\}\}",
                m => FormatPreviewDate(now, m.Groups[1].Value));

            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"\{\{prompt:([^}]*)\}\}", m => "<" + m.Groups[1].Value + ">");

            text = text.Replace("{{clipboard}}", "<clipboard>", StringComparison.Ordinal);

            // The engine honors only the first marker; any others are typed literally.
            int cursor = text.IndexOf("$|$", StringComparison.Ordinal);
            if (cursor >= 0)
            {
                text = string.Concat(text.AsSpan(0, cursor), "|", text.AsSpan(cursor + 3));
            }

            return text
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal);
        }

        /// <summary>
        /// Formats a preview date. Mirrors the engine's rule that a one-character format is a
        /// field rather than a standard specifier, so {{date:d}} previews the day and not a
        /// whole short date.
        /// </summary>
        private static string FormatPreviewDate(DateTime value, string format)
        {
            if (format.Length == 0)
            {
                return string.Empty;
            }

            // A '%' is unambiguous: it is not a .NET custom specifier on its own, and every
            // strftime format has one. Returning the format verbatim here used to make
            // {{date:%Y-%m-%d}} preview as literal "%Y-%m-%d", telling the user that a syntax
            // the engine fully supports does not work.
            if (format.Contains('%', StringComparison.Ordinal))
            {
                return FormatStrftime(value, format);
            }

            try
            {
                return value.ToString(format.Length == 1 ? "%" + format : format, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>
        /// Expands strftime specifiers for the preview.
        ///
        /// <para>
        /// A deliberate second implementation of <c>Variables.FormatStrftime</c> in the engine,
        /// because the Settings project cannot reference the engine assembly. Kept as a literal
        /// transcription for that reason -- if the engine's table changes, this one has to change
        /// with it, and a preview that disagrees with what gets typed is the bug this whole
        /// method exists to fix.
        /// </para>
        /// </summary>
        private static string FormatStrftime(DateTime value, string format)
        {
            var result = new System.Text.StringBuilder(format.Length + 16);

            for (int index = 0; index < format.Length; index++)
            {
                if (format[index] != '%' || index + 1 >= format.Length)
                {
                    result.Append(format[index]);
                    continue;
                }

                char specifier = format[++index];
                result.Append(specifier switch
                {
                    '%' => "%",
                    'a' => value.ToString("ddd", CultureInfo.InvariantCulture),
                    'A' => value.ToString("dddd", CultureInfo.InvariantCulture),
                    'b' or 'h' => value.ToString("MMM", CultureInfo.InvariantCulture),
                    'B' => value.ToString("MMMM", CultureInfo.InvariantCulture),
                    'd' => value.ToString("dd", CultureInfo.InvariantCulture),
                    'e' => value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, ' '),
                    'F' => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    'H' => value.ToString("HH", CultureInfo.InvariantCulture),
                    'I' => value.ToString("hh", CultureInfo.InvariantCulture),
                    'j' => value.DayOfYear.ToString("000", CultureInfo.InvariantCulture),
                    'm' => value.ToString("MM", CultureInfo.InvariantCulture),
                    'M' => value.ToString("mm", CultureInfo.InvariantCulture),
                    'p' => value.ToString("tt", CultureInfo.InvariantCulture),
                    'R' => value.ToString("HH:mm", CultureInfo.InvariantCulture),
                    'S' => value.ToString("ss", CultureInfo.InvariantCulture),
                    'T' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    'u' => ((((int)value.DayOfWeek + 6) % 7) + 1).ToString(CultureInfo.InvariantCulture),
                    'w' => ((int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture),
                    'x' => value.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                    'X' => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    'y' => value.ToString("yy", CultureInfo.InvariantCulture),
                    'Y' => value.ToString("yyyy", CultureInfo.InvariantCulture),
                    'n' => "\n",
                    't' => "\t",
                    _ => $"%{specifier}",
                });
            }

            return result.ToString();
        }

        private string WriteFile()
        {
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
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "'{0}' already exists and was not loaded as a snippet file. Refusing to overwrite it.",
                        path);
                }

                // Someone else changed the file since it was loaded. Rewriting our snapshot would
                // silently throw their edit away, so stop and reload instead.
                if (_loadedBytes != null && File.Exists(path))
                {
                    byte[] current = ReadAllBytesShared(path);
                    if (!current.AsSpan().SequenceEqual(_loadedBytes))
                    {
                        LoadSnippets();
                        return "The snippet file changed on disk, so your edit was not applied. The list has been reloaded — please try again.";
                    }
                }

                // Preserve the file's own encoding and line endings rather than imposing ours.
                string text = string.Join(_newline ?? Environment.NewLine, _file.ToLines());
                if (_hadFinalNewline)
                {
                    text += _newline ?? Environment.NewLine;
                }

                byte[] payload = new System.Text.UTF8Encoding(_hadBom, false).GetBytes(text);

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

                _snippetError = null;
                LoadSnippets();
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

        private static bool HasUtf8Bom(byte[] bytes)
            => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        /// <summary>
        /// Splits on CRLF, LF and lone CR, matching what StreamReader.ReadLine would produce.
        /// A lone CR really is a line break on read, which is why the model refuses to emit one
        /// inside a single-line entry.
        /// </summary>
        private static string[] SplitLines(string text)
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

        private void RaiseSnippetListChanged()
        {
            OnPropertyChanged(nameof(HasNoSnippets));
            OnPropertyChanged(nameof(HasSnippetError));
            OnPropertyChanged(nameof(SnippetError));
            OnPropertyChanged(nameof(SnippetsFilePath));
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
