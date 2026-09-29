// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Reads the settings file a host writes for us, and keeps watching it.
///
/// <para>
/// PowerToys modules are configured through the Settings app, which writes
/// <c>%LOCALAPPDATA%\Microsoft\PowerToys\&lt;module&gt;\settings.json</c>. Without this the module's
/// settings page would be a facade: the shim's <c>set_config</c> would dutifully save the file
/// and the engine would carry on reading environment variables, so every control would appear to
/// work and change nothing.
/// </para>
///
/// <para>
/// Entirely opt-in, via <c>--settings:&lt;path&gt;</c>. No switch means no file is read and the
/// engine keeps running even when no host settings file is provided.
/// </para>
///
/// <para>
/// Parsed with <see cref="JsonDocument"/> rather than the serializer on purpose: the engine is
/// published with <c>PublishTrimmed</c>, and reflection-based deserialization is precisely what
/// trimming breaks. A DOM read of four known keys needs no reflection and no generated context.
/// </para>
/// </summary>
internal sealed class HostSettings : IDisposable
{
    /// <summary>Points the engine at a host-written settings file.</summary>
    public const string SettingsSwitch = "--settings:";

    /// <summary>Coalesces the burst of events a single save produces, as the snippet store does.</summary>
    private const int DebounceMs = 250;

    private readonly string _path;
    private readonly object _debounceLock = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Action? _onChanged;

    public string FilePath => _path;

    private HostSettings(string path) => _path = path;

    /// <summary>
    /// The values a host can set. Null means "not specified", so an absent key leaves whatever
    /// the engine already had rather than resetting it to a default the user never chose.
    /// </summary>
    internal readonly record struct Values(string? SnippetsPath, string? InjectionBackend, int? ClipboardThresholdChars, bool? TreatRemapsAsTyping, bool? RequireWordBoundary, bool? WordBoundaryKeepsSpace);

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
    /// Reads the file. Returns an all-null <see cref="Values"/> when it is missing or unreadable,
    /// which leaves current settings alone — a settings file that has not been written yet, or is
    /// mid-save, must not reset how the engine injects text.
    /// </summary>
    public Values Read()
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
            return default;
        }

        return Parse(json);
    }

    /// <summary>
    /// Parses the PowerToys settings shape. <c>StringProperty</c> and <c>IntProperty</c> serialize
    /// as <c>{"value": x}</c> while a plain bool serializes bare, so every lookup accepts both.
    /// </summary>
    internal static Values Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            if (!doc.RootElement.TryGetProperty("properties", out JsonElement props) || props.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            return new Values(ReadString(props, "snippets_path"), ReadString(props, "injection_backend"), ReadInt(props, "clipboard_threshold_chars"), ReadBool(props, "treat_remaps_as_typing"), ReadBool(props, "require_word_boundary"), ReadBool(props, "word_boundary_keeps_space"));
        }
        catch (JsonException)
        {
            // A half-written or hand-corrupted file must not take the engine down, and must not
            // silently reset the user's choices either.
            return default;
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
    /// module. <paramref name="onChanged"/> runs on a timer thread.
    /// </summary>
    public void StartWatching(Action onChanged)
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
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => ScheduleReload();

    private void ScheduleReload()
    {
        lock (_debounceLock)
        {
            _debounce ??= new Timer(
                _ =>
            {
                try
                {
                    _onChanged?.Invoke();
                }
                catch
                { /* a bad settings file must never take the engine down */
                }
            },
                null,
                Timeout.Infinite,
                Timeout.Infinite);

            _debounce.Change(DebounceMs, Timeout.Infinite);
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

    /// <summary>
    /// The folder a host owns for this module's data, or null when no host settings path is provided.
    ///
    /// <para>
    /// Derived from the settings file the host already told us about rather than from a new
    /// switch. The host picked that location; putting snippets beside its settings keeps a
    /// module's state in one directory the host can back up, migrate or delete as a unit, and
    /// means the host's settings UI can find the snippet file without being told twice.
    /// </para>
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
