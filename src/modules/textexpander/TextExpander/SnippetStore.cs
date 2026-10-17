// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Loads and watches snippets.txt using the same grammar as the Settings editor.
/// </summary>
internal sealed class SnippetStore : IDisposable
{
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

    public string FilePath { get; private set; }

    public Snapshot Current => _current;

    public bool IsWatching => Volatile.Read(ref _watcher)?.EnableRaisingEvents == true;

    /// <summary>Raised after a successful reload. Fires on a thread pool thread.</summary>
    public event Action<int>? Reloaded;

    public SnippetStore(string filePath) => FilePath = filePath;

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
    {
        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            return string.IsNullOrWhiteSpace(hostDataFolder)
                ? ResolveDefaultPath()
                : ResolveHostPath(hostDataFolder!);
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

                // Accept a file too: someone will inevitably paste the path to snippets.txt itself.
                if (File.Exists(full))
                {
                    return full;
                }

                Directory.CreateDirectory(full);
                return Path.Combine(full, "snippets.txt");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            // An unusable setting must not prevent the host/default library from loading.
        }

        return string.IsNullOrWhiteSpace(hostDataFolder)
            ? ResolveDefaultPath()
            : ResolveHostPath(hostDataFolder!);
    }

    /// <summary>
    /// snippets.txt inside the host data folder, falling back to the PowerToys per-user folder if that folder cannot be created.
    /// </summary>
    private static string ResolveHostPath(string hostDataFolder)
    {
        try
        {
            string full = Path.GetFullPath(hostDataFolder);
            Directory.CreateDirectory(full);
            return Path.Combine(full, "snippets.txt");
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
        return Path.Combine(appData, "snippets.txt");
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
            Dictionary<string, string> map = Parse(lines);

            var index = TriggerIndex.Build(map.Keys);

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
    internal static Dictionary<string, string> Parse(string[] lines)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
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
                    result[trigger] = string.Join("\n", body);
                }
            }
            else
            {
                int eq = stripped.IndexOf('=');
                if (eq > 0)
                {
                    int rawEq = line.IndexOf('=');
                    result[stripped[..eq]] = line[(rawEq + 1)..];
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
                Filter = Path.GetFileName(FilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };

            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
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
    public int Repoint(string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        if (string.Equals(newPath, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        // Stop the old watcher before the path moves, so a late event cannot reload the new
        // file through the old filter.
        _watcher?.Dispose();
        _watcher = null;

        FilePath = newPath;
        int count = Load();
        StartWatching();
        return count;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => ScheduleReload();

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
                int count = Load();
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
    private const string DefaultSnippets = """
        // ── Text Expander snippets ───────────────────────
        // One snippet per line:  trigger=replacement
        //
        // Dynamic variables (evaluated when the snippet fires):
        //   {{date}}      current date  (mm/dd/yy)
        //   {{time}}      current time  (hh:mm AM/PM)
        //   {{datetime}}  date + time   (mm/dd/yy hh:mm AM/PM)
        //   {{isodate}}   ISO date      (yyyy-mm-dd)
        //   {{prompt:Label}}  popup asks you to type a value
        //
        // Escape sequences:
        //   \n           newline (for single-line snippets)
        //   \t           tab
        //
        // Multi-line snippets:
        //   trigger<<<
        //   First line
        //   Second line
        //   >>>
        //
        // Tips:
        // - Start triggers with #
        // - Comments start with //
        // - This file auto-reloads whenever you save it
        // ──────────────────────────────────────────────────────

        #ddate={{date}}
        #ttime={{time}}
        #dt={{datetime}}
        #iso={{isodate}}
        #sig=Best regards,\nYour Name
        #email=you@example.com

        """;
}
