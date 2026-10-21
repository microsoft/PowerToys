// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Loads and watches the snippet file using the same grammar as the Settings editor.
/// </summary>
internal sealed class SnippetStore : IDisposable
{
    /// <summary>
    /// The name a new snippet file gets. Settings has the same name in
    /// <c>TextExpanderSnippetFileNames.Current</c>, and a test checks that they match.
    /// </summary>
    internal const string FileName = "PowerToysTextExpanderSnippets.txt";

    /// <summary>
    /// The name earlier versions used. It is still read, and never renamed: a folder sync service
    /// could propagate the rename while another PC still runs an earlier version.
    /// </summary>
    internal const string LegacyFileName = "snippets.txt";

    /// <summary>Immutable snapshot so the keyboard hook can read without locking.</summary>
    public sealed class Snapshot
    {
        public required Dictionary<string, string> Map { get; init; }

        /// <summary>
        /// Gets suffix index over the triggers. Replaces the old longest-first array, which the hook
        /// had to scan in full on every keystroke; see <see cref="TriggerIndex"/> for why that
        /// was a correctness problem and not only a slow one.
        /// </summary>
        public required TriggerIndex Triggers { get; init; }

        public required int MaxTriggerLength { get; init; }

        public static readonly Snapshot Empty = new()
        {
            Map = new Dictionary<string, string>(StringComparer.Ordinal),
            Triggers = TriggerIndex.Empty,
            MaxTriggerLength = 0,
        };
    }

    private readonly object _debounceLock = new();
    private volatile Snapshot _current = Snapshot.Empty;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _resolveFolderNames;
    private bool _caseSensitive;

    public string FilePath { get; private set; }

    public Snapshot Current => _current;

    public bool IsWatching => Volatile.Read(ref _watcher)?.EnableRaisingEvents == true;

    /// <summary>Raised after a successful reload. Fires on a thread pool thread.</summary>
    public event Action<int>? Reloaded;

    public SnippetStore(string filePath, bool resolveFolderNames = false, bool caseSensitive = true)
    {
        FilePath = filePath;
        _resolveFolderNames = resolveFolderNames;
        _caseSensitive = caseSensitive;
    }

    // ── Path resolution ──────────────────────────────────────

    /// <summary>
    /// Resolves the snippet file for a configured folder, or falls back to
    /// <see cref="ResolveDefaultPath"/> when nothing was configured or the value is unusable,
    /// including paths that are not fully qualified after environment variable expansion.
    ///
    /// <para>
    /// A folder is accepted rather than a file path because that is what the setting asks for,
    /// and because pointing the engine at an arbitrary file makes the "reloads on save" promise
    /// harder to keep. A path that cannot be resolved falls back silently: the alternative is an
    /// engine that refuses to expand anything because a setting has a typo in it.
    /// </para>
    /// </summary>
    public static string ResolveConfiguredPath(string? configuredFolder)
        => ResolveConfiguredPath(configuredFolder, hostDataFolder: null);

    /// <summary>
    /// As above, but falls back to <paramref name="hostDataFolder"/> instead of
    /// <see cref="ResolveDefaultPath"/> when nothing is configured.
    ///
    /// <para>
    /// Under a host such as PowerToys the default must not be "beside the executable". That
    /// directory belongs to the host's installer: on a per-machine install it is not writable,
    /// and on a per-user one the file would be destroyed by the next upgrade. It is also not
    /// somewhere the host's own settings UI can predict, and a snippet editor that cannot find
    /// the file it is editing is no editor at all.
    /// </para>
    /// </summary>
    public static string ResolveConfiguredPath(string? configuredFolder, string? hostDataFolder)
        => ResolveConfiguredPathInfo(configuredFolder, hostDataFolder).FilePath;

    internal static SnippetPathResolution ResolveConfiguredPathInfo(string? configuredFolder, string? hostDataFolder)
    {
        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            string fallback = string.IsNullOrWhiteSpace(hostDataFolder)
                ? ResolveDefaultPath()
                : ResolveHostPath(hostDataFolder!);
            return new SnippetPathResolution(fallback, ResolveFolderNames: true);
        }

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(configuredFolder!.Trim().Trim('"'));

            // The engine and its host's editor run in different processes. Their working
            // directories cannot be a shared base for this setting, including drive-relative
            // (C:snips) and root-relative (\snips) paths on Windows.
            if (Path.IsPathFullyQualified(expanded))
            {
                string full = Path.GetFullPath(expanded);

                // Accept a file too: someone will inevitably paste the path to the snippet file itself.
                if (File.Exists(full))
                {
                    return new SnippetPathResolution(full, ResolveFolderNames: false);
                }

                Directory.CreateDirectory(full);
                return new SnippetPathResolution(ResolveInFolder(full), ResolveFolderNames: true);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            // An unusable setting must not prevent the host/default library from loading.
        }

        string fallbackPath = string.IsNullOrWhiteSpace(hostDataFolder)
            ? ResolveDefaultPath()
            : ResolveHostPath(hostDataFolder!);
        return new SnippetPathResolution(fallbackPath, ResolveFolderNames: true);
    }

    /// <summary>
    /// The snippet file inside the host data folder, falling back to the PowerToys per-user folder if that folder cannot be created.
    /// </summary>
    private static string ResolveHostPath(string hostDataFolder)
    {
        try
        {
            string full = Path.GetFullPath(hostDataFolder);
            Directory.CreateDirectory(full);
            return ResolveInFolder(full);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return ResolveDefaultPath();
        }
    }

    /// <summary>
    /// Resolves the PowerToys per-user snippet file used when no settings path is available.
    /// </summary>
    public static string ResolveDefaultPath()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TextExpander");
        Directory.CreateDirectory(appData);
        return ResolveInFolder(appData);
    }

    /// <summary>
    /// Returns the snippet file in <paramref name="folder"/>: <see cref="FileName"/> if it exists,
    /// otherwise <see cref="LegacyFileName"/> if that exists, otherwise <see cref="FileName"/>,
    /// which <see cref="Load"/> then creates with the starter snippets. Settings resolves a folder
    /// with the same rule.
    /// </summary>
    internal static string ResolveInFolder(string folder)
    {
        string current = Path.Combine(folder, FileName);
        if (File.Exists(current))
        {
            return current;
        }

        string legacy = Path.Combine(folder, LegacyFileName);
        return File.Exists(legacy) ? legacy : current;
    }

    // ── Loading ──────────────────────────────────────────────
    public int Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, DefaultSnippets, System.Text.Encoding.UTF8);
            }

            string[] lines = ReadAllLinesShared(FilePath);
            Dictionary<string, string> map = Parse(lines, _caseSensitive);

            var index = TriggerIndex.Build(map.Keys, _caseSensitive);

            _current = new Snapshot
            {
                Map = map,
                Triggers = index,
                MaxTriggerLength = index.MaxTriggerLength,
            };
            return map.Count;
        }
        catch (IOException)
        {
            // Editor still holds the file mid-save; the debounced watcher will retry.
            return _current.Map.Count;
        }
        catch (UnauthorizedAccessException)
        {
            return _current.Map.Count;
        }
    }

    /// <summary>Reads with permissive sharing so an open editor does not block us.</summary>
    private static string[] ReadAllLinesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);

        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    /// <summary>
    /// Parses the snippet file format: "//" comments, "trigger=replacement", and
    /// "trigger&lt;&lt;&lt; ... &gt;&gt;&gt;" multi-line blocks. Replacement text is taken from the
    /// raw line so trailing spaces survive.
    /// </summary>
    internal static Dictionary<string, string> Parse(string[] lines, bool caseSensitive = true)
    {
        var result = new Dictionary<string, string>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];
            string stripped = line.Trim();
            i++;

            if (stripped.Length == 0 || stripped.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (stripped.EndsWith("<<<", StringComparison.Ordinal))
            {
                string trigger = stripped[..^3];
                var body = new List<string>();
                while (i < lines.Length)
                {
                    string bodyLine = lines[i];
                    i++;
                    if (bodyLine.Trim() == ">>>")
                    {
                        break;
                    }

                    body.Add(bodyLine);
                }

                if (trigger.Length > 0)
                {
                    result.Remove(trigger);
                    result[trigger] = string.Join("\n", body);
                }
            }
            else
            {
                int eq = stripped.IndexOf('=');
                if (eq > 0)
                {
                    int rawEq = line.IndexOf('=');
                    string trigger = stripped[..eq];
                    result.Remove(trigger);
                    result[trigger] = line[(rawEq + 1)..];
                }
            }
        }

        return result;
    }

    // ── Matching ─────────────────────────────────────────────

    /// <summary>
    /// Returns the longest trigger that is a suffix of <paramref name="tail"/>, or null.
    ///
    /// Runs inside the low-level keyboard hook on every keystroke, so it must not depend on how
    /// many snippets exist: the index makes this a walk of at most <c>MaxTriggerLength</c> steps
    /// whether the user has ten triggers or ten thousand.
    /// </summary>
    public string? Match(ReadOnlySpan<char> tail) => _current.Triggers.MatchLongestSuffix(tail);

    public bool TryGetReplacement(string trigger, out string replacement)
        => _current.Map.TryGetValue(trigger, out replacement!);

    /// <summary>
    /// Changes whether trigger matching distinguishes capitalization, then rebuilds the immutable
    /// lookup from the active file. Returns the snippet count, or -1 when nothing changed.
    /// </summary>
    public int SetCaseSensitive(bool caseSensitive)
    {
        if (_caseSensitive == caseSensitive)
        {
            return -1;
        }

        _caseSensitive = caseSensitive;
        return Load();
    }

    // ── Watching ─────────────────────────────────────────────
    public void StartWatching()
    {
        if (IsWatching)
        {
            return;
        }

        FileSystemWatcher? watcher = null;
        try
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            watcher = new FileSystemWatcher(dir)
            {
                // Watch the directory rather than the file: many editors save by writing a
                // temporary file and renaming over the original, which destroys a file-level watch.
                // A folder setting watches both supported names, so a later preferred file can
                // replace a legacy one without Settings and the engine splitting onto two files.
                Filter = _resolveFolderNames ? "*" : Path.GetFileName(FilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };

            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileEvent;
            watcher.Error += OnWatcherError;
            Volatile.Write(ref _watcher, watcher);
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
        {
            // A library can still expand from its last snapshot without a watch. Diagnostics
            // must report that state instead of letting a directory race take the engine down.
            watcher?.Dispose();
            if (watcher is not null)
            {
                Interlocked.CompareExchange(ref _watcher, null, watcher);
            }
        }
    }

    /// <summary>
    /// Points this store at a different snippet file, reloading and re-watching.
    ///
    /// <para>
    /// Deliberately mutates this instance rather than handing back a new one: the keyboard hook
    /// captures the store at construction, so replacing the object would leave the hook reading
    /// the old file forever. The snapshot swap inside <see cref="Load"/> is already atomic, so
    /// the hook sees either the old snippets or the new ones and never a half-built state.
    /// </para>
    ///
    /// <para>Returns the number of snippets loaded, or -1 when the path did not change.</para>
    /// </summary>
    public int Repoint(string newPath, bool resolveFolderNames = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        if (string.Equals(newPath, FilePath, StringComparison.OrdinalIgnoreCase)
            && resolveFolderNames == _resolveFolderNames)
        {
            return -1;
        }

        // Stop the old watcher before the path moves, so a late event cannot reload the new
        // file through the old filter.
        _watcher?.Dispose();
        _watcher = null;

        FilePath = newPath;
        _resolveFolderNames = resolveFolderNames;
        int count = Load();
        StartWatching();
        return count;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (IsRelevantFileName(e.Name)
            || (e is RenamedEventArgs renamed && IsRelevantFileName(renamed.OldName)))
        {
            ScheduleReload();
        }
    }

    private bool IsRelevantFileName(string? name)
    {
        if (!_resolveFolderNames)
        {
            return string.Equals(name, Path.GetFileName(FilePath), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(name, FileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, LegacyFileName, StringComparison.OrdinalIgnoreCase);
    }

    internal int ReloadPreferredPath()
    {
        if (_resolveFolderNames)
        {
            string? folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                string preferred = ResolveInFolder(folder);
                if (!string.Equals(preferred, FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Repoint(preferred, resolveFolderNames: true);
                }
            }
        }

        return Load();
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Even a buffer overflow means changes may have been missed. Retire the watch so
        // diagnostics cannot promise live reload until it has been explicitly started again.
        // A late error from the previous path must never stop the new path's watcher.
        if (sender is FileSystemWatcher watcher)
        {
            watcher.Dispose();
            Interlocked.CompareExchange(ref _watcher, null, watcher);
        }
    }

    /// <summary>Coalesces the burst of events a single save produces.</summary>
    private void ScheduleReload()
    {
        lock (_debounceLock)
        {
            _debounce ??= new Timer(
                _ =>
            {
                int count = ReloadPreferredPath();
                Reloaded?.Invoke(count);
            },
                null,
                Timeout.Infinite,
                Timeout.Infinite);

            _debounce.Change(250, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        lock (_debounceLock)
        {
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    // ── Default file ─────────────────────────────────────────
    // Written only when the snippet file is missing; an existing file is never replaced.
    private const string DefaultSnippets = """
        // ── PowerToys Text Expander ─────────────────────────────
        // Built by Gary Hsu · https://github.com/howdygary
        // Project: https://github.com/howdygary/text-expander
        // Website: https://howdygary.github.io/text-expander/
        //
        // Type a trigger, such as !ddate, and Text Expander replaces it with its
        // snippet. The snippets at the bottom of this file work right away.
        //
        // ── How to write a snippet ──────────────────────────────
        // One line:       trigger=replacement
        // Several lines:  trigger<<<
        //                 first line
        //                 second line
        //                 >>>
        // Lines that start with //, like this one, are notes and are skipped.
        // Changes work as soon as you save this file.
        //
        // ── Variables, filled in each time a snippet expands ────
        // {{date}}             today's date, like 06/01/26
        // {{time}}             the time, like 02:30 PM
        // {{datetime}}         both, like 06/01/26 02:30 PM
        // {{isodate}}          the date year first, like 2026-06-01
        // {{date:FORMAT}}      a date your way: {{date:dddd, MMMM d}} gives Monday, June 1
        //                      (dddd weekday, MMMM month, d day, yyyy year, HH:mm 24-hour time)
        //                      Put words in single quotes, or they're read as codes:
        //                      {{date:'Today is' dddd}} gives Today is Monday
        // {{clipboard}}        the text you copied last, such as a link
        // {{prompt:Question}}  asks you a question and requires an answer,
        //                      like {{prompt:First name}}, then types what you enter
        // {{prompt?:Question}} asks the same way but lets you continue with no answer
        // $|$                  where the cursor ends up: Dear $|$, leaves it after "Dear "
        // \n and \t            a new line and a tab
        //
        // ── Tips ────────────────────────────────────────────────
        // - Start triggers with ! so they don't fire in the middle of a word.
        // - A trigger fires as soon as you type it, so one trigger can't be the start
        //   of another: !sig would fire before you could finish typing !signature.
        // - To sync snippets across devices, choose a folder managed by OneDrive or
        //   another sync service in PowerToys Settings > Text Expander.
        //
        // ── Examples: remove the // and the space after it to use one ──
        // !hello=Hi {{prompt:First name}}, thanks for reaching out!
        // !mdlink=[{{prompt:Link text}}]({{clipboard}})
        // !longdate={{date:dddd, MMMM d, yyyy}}
        // !time24={{date:HH:mm}}
        // !todo=// TODO: $|$ ({{isodate}})
        // !myaddr=123 Example Street\nSpringfield, ZZ 12345
        // !tabhead=Name\tEmail\tTeam
        // For !meeting, do that on all five of its lines:
        // !meeting<<<
        // Meeting notes · {{date:dddd, MMMM d, yyyy}}
        // Attendees: $|$
        // Next steps:
        // >>>
        //
        // ── Your snippets ───────────────────────────────────────

        !ddate={{date}}
        !ttime={{time}}
        !ddt={{datetime}}
        !isodate={{isodate}}
        !mysig=Best regards,\nYour Name
        !myemail=you@example.com

        """;
}
