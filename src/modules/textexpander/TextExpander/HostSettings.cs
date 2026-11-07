// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Reads the settings file a host writes for us (opt-in via <c>--settings:&lt;path&gt;</c>) and keeps watching
/// it. Parsed with <see cref="JsonDocument"/>, which needs no reflection.
/// </summary>
internal sealed class HostSettings : IDisposable
{
    /// <summary>Points the engine at a host-written settings file.</summary>
    public const string SettingsSwitch = "--settings:";

    /// <summary>Coalesces the burst of events a single save produces, as the snippet store does.</summary>
    internal const int DebounceMs = 250;

    /// <summary>
    /// How many more times a change is read, <see cref="DebounceMs"/> apart, when the file can't be read yet. A save in
    /// progress has usually finished by then.
    /// </summary>
    internal const int MaxRetries = 4;

    private readonly string _path;
    private readonly object _debounceLock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Func<bool>? _onChanged;

    // Guarded by _debounceLock.
    private int _retries;
    private bool _disposed;

    public string FilePath => _path;

    private HostSettings(string path) => _path = path;

    /// <summary>
    /// The values a host can set. Null means "not specified", so an absent key leaves whatever
    /// the engine already had rather than resetting it to a default the user never chose.
    /// </summary>
    internal readonly record struct Values(string? SnippetsPath, string? InjectionBackend, int? ClipboardThresholdChars, bool? TreatRemapsAsTyping, bool? RequireWordBoundary, bool? WordBoundaryKeepsSpace, bool? CaseSensitive);

    public static HostSettings? Create(string[] args)
    {
        string? path = ParseSettingsPath(args);
        return path is null ? null : new HostSettings(path);
    }

    internal static string? ParseSettingsPath(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (string arg in args)
        {
            if (!arg.StartsWith(SettingsSwitch, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string raw = arg[SettingsSwitch.Length..].Trim().Trim('"');
            if (raw.Length == 0)
            {
                continue;
            }

            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the file. Null when it can't be read or isn't settings: missing, locked, caught mid-save or
    /// hand-corrupted. That's not a request for the defaults, so callers keep the settings they have.
    /// </summary>
    public Values? Read()
    {
        string json;
        try
        {
            // Permissive sharing so the Settings app writing the file does not make us fail.
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        return Parse(json);
    }

    /// <summary>
    /// Parses the PowerToys settings shape, or returns null when <paramref name="json"/> isn't it: not JSON, or without a
    /// <c>properties</c> object. <c>StringProperty</c> and <c>IntProperty</c> serialize as <c>{"value": x}</c> while a
    /// plain bool serializes bare, so every lookup accepts both.
    /// </summary>
    internal static Values? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("properties", out JsonElement props) || props.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new Values(ReadString(props, "snippets_path"), ReadString(props, "injection_backend"), ReadInt(props, "clipboard_threshold_chars"), ReadBool(props, "treat_remaps_as_typing"), ReadBool(props, "require_word_boundary"), ReadBool(props, "word_boundary_keeps_space"), ReadBool(props, "case_sensitive"));
        }
        catch (JsonException)
        {
            // A half-written or hand-corrupted file must not take the engine down, nor be taken for
            // one that asks for the defaults.
            return null;
        }
    }

    /// <summary>Unwraps <c>{"value": x}</c>, or returns the element itself when it is bare.</summary>
    private static bool TryGetValue(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out JsonElement inner))
        {
            value = inner;
            return true;
        }

        value = element;
        return true;
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!TryGetValue(parent, name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? ReadInt(JsonElement parent, string name)
    {
        if (!TryGetValue(parent, name, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number;
        }

        // Tolerated because a hand-edited file, or a settings model that changes a property's
        // type, should not silently drop the user's value.
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? ReadBool(JsonElement parent, string name)
    {
        if (!TryGetValue(parent, name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    /// <summary>
    /// Watches the file so a change in the Settings app takes effect without restarting the
    /// module. <paramref name="onChanged"/> runs on a timer thread, and returns false when it couldn't read the file:
    /// it's then called again, up to <see cref="MaxRetries"/> more times, before waiting for the next change. With
    /// <paramref name="readAgain"/>, for a file that couldn't be read at startup, it's called once soon without a change too.
    /// </summary>
    public void StartWatching(Func<bool> onChanged, bool readAgain = false)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        _onChanged = onChanged;

        string? dir = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(dir)
            {
                // Watch the directory rather than the file: the Settings app saves by writing a
                // temporary file and renaming over the original, which destroys a file-level watch.
                Filter = Path.GetFileName(_path),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Settings that only apply at startup are much better than refusing to start.
            _watcher = null;
        }

        if (readAgain)
        {
            _ = ScheduleReload(retry: false);
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => ScheduleReload(retry: false);

    /// <returns>False when the retries are used up.</returns>
    private bool ScheduleReload(bool retry)
    {
        lock (_debounceLock)
        {
            if (_disposed || (retry && _retries >= MaxRetries))
            {
                return false;
            }

            // Each change gets the full number of tries.
            _retries = retry ? _retries + 1 : 0;
            _debounce ??= new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(DebounceMs, Timeout.Infinite);
            return true;
        }
    }

    private void Reload()
    {
        bool read;
        try
        {
            read = _onChanged?.Invoke() ?? true;
        }
        catch
        {
            // A bad settings file must never take the engine down.
            return;
        }

        if (!read)
        {
            // The engine keeps no log file; a debugger or DebugView shows this.
            Trace.TraceWarning($"Text Expander couldn't read its settings file '{_path}', so it kept the settings it had. "
                + (ScheduleReload(retry: true) ? $"Trying again in {DebounceMs} ms." : "It reads the file again when it changes."));
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        lock (_debounceLock)
        {
            _disposed = true;
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    /// <summary>
    /// The folder a host owns for this module's data (the settings file's folder), or null when no host
    /// settings path is provided.
    /// </summary>
    public static string? DataFolder(string? settingsFilePath)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
        {
            return null;
        }

        try
        {
            // HostSettings normalizes --settings before this call. Reject partial paths here
            // rather than making this directory depend on whichever process asks for it.
            return Path.IsPathFullyQualified(settingsFilePath)
                ? Path.GetDirectoryName(Path.GetFullPath(settingsFilePath))
                : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
