// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Registry;

/// <summary>
/// A manifest that failed to load or validate, kept so the UI can surface problems.
/// </summary>
public sealed record ScriptLoadError(string FolderPath, string Message);

/// <summary>
/// The single source of truth for installed PowerScripts. Every surface (context menu, Keyboard
/// Manager editor, Command Palette, agents) reads from this registry rather than defining scripts
/// of its own. The registry only reads the filesystem; it never executes anything.
/// </summary>
public sealed class ScriptRegistry
{
    private readonly List<PowerScriptManifest> _scripts = new();
    private readonly List<ScriptLoadError> _errors = new();
    private readonly List<PowerScriptManifest> _unconfiguredScripts = new();
    private readonly ScriptSettingsStore _settings;

    public ScriptRegistry(string? root = null, ScriptSettingsStore? settings = null)
    {
        Root = Path.GetFullPath(PowerScriptsPaths.ResolveScriptsRoot(root));
        _settings = settings ?? new ScriptSettingsStore();
    }

    /// <summary>Absolute path to the scanned scripts root.</summary>
    public string Root { get; }

    public IReadOnlyList<PowerScriptManifest> Scripts => _scripts;

    public IReadOnlyList<ScriptLoadError> Errors => _errors;

    public IReadOnlyList<PowerScriptManifest> UnconfiguredScripts => _unconfiguredScripts;

    /// <summary>
    /// Rebuilds the catalogue from <c>*.tool.json</c> descriptors directly under <see cref="Root"/>
    /// or one sub-folder below it. A descriptor identifies its sibling script and declares its I/O.
    /// Invalid scripts are recorded in <see cref="Errors"/> and skipped.
    /// </summary>
    public void Load()
    {
        _scripts.Clear();
        _errors.Clear();
        _unconfiguredScripts.Clear();

        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(Root))
        {
            AddDescriptorEntries(Root, entries);
            foreach (var folder in Directory.EnumerateDirectories(Root))
            {
                AddDescriptorEntries(folder, entries);
            }
        }

        foreach (var entry in _settings.GetRegisteredEntries(Root, (path, error) => _errors.Add(new ScriptLoadError(path, error))))
        {
            entries.Add(entry);
        }

        foreach (var entry in entries.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            PowerScriptManifest manifest;
            try
            {
                manifest = _settings.Get(Root, entry).Manifest;
            }
            catch (Exception ex) when (ScriptSettingsStore.IsSettingsError(ex))
            {
                _errors.Add(new ScriptLoadError(entry, ex.Message));
                _unconfiguredScripts.Add(new PowerScriptManifest
                {
                    Id = "unconfigured-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(entry.ToUpperInvariant())))[..24].ToLowerInvariant(),
                    Name = Path.GetFileName(entry),
                    Entry = Path.GetFileName(entry),
                    FolderPath = Path.GetDirectoryName(entry)!,
                    ScriptsRoot = Root,
                    HasDescriptor = File.Exists(entry + ToolDescriptorParser.DescriptorSuffix),
                    HasLocalSettings = File.Exists(_settings.GetSettingsPath(Root, entry)),
                    ConfigurationErrors = new[] { ex.Message },
                });
                continue;
            }

            if (manifest.ConfigurationErrors.Count > 0)
            {
                _errors.Add(new ScriptLoadError(entry, string.Join(" ", manifest.ConfigurationErrors)));
                _unconfiguredScripts.Add(manifest);
                continue;
            }

            try
            {
                manifest.IconPath = ResolveIcon(manifest);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
            {
                manifest.IconWarning = $"Script icon unavailable: {ex.Message}";
                _errors.Add(new ScriptLoadError(entry, manifest.IconWarning));
            }

            _scripts.Add(manifest);
        }

        // Never let a new installation inherit another source's identity/bindings by winning scan
        // order. All members of a collision remain visible to Settings, but none can execute.
        var duplicates = _scripts.Concat(_unconfiguredScripts)
            .GroupBy(script => script.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1).ToList();
        foreach (var duplicate in duplicates)
        {
            foreach (var script in duplicate)
            {
                var error = $"duplicate id '{script.Id}' - choose a unique action id in script settings.";
                script.ConfigurationErrors = script.ConfigurationErrors.Append(error).ToList();
                _errors.Add(new ScriptLoadError(script.EntryFullPath, error));
                _scripts.Remove(script);
                if (!_unconfiguredScripts.Contains(script))
                {
                    _unconfiguredScripts.Add(script);
                }
            }
        }

        _scripts.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Discovers PowerScripts exclusively through their <c>*.tool.json</c> descriptors.
    /// </summary>
    private static void AddDescriptorEntries(string directory, HashSet<string> entries)
    {
        foreach (var descriptor in EnumerateDescriptorCandidates(directory))
        {
            entries.Add(descriptor[..^ToolDescriptorParser.DescriptorSuffix.Length]);
        }
    }

    private static IEnumerable<string> EnumerateDescriptorCandidates(string directory) =>
        Directory.EnumerateFiles(directory, "*" + ToolDescriptorParser.DescriptorSuffix)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

    public PowerScriptManifest? Get(string id) =>
        _scripts.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveIcon(PowerScriptManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Icon))
        {
            return null;
        }

        if (Path.IsPathRooted(manifest.Icon) ||
            !string.Equals(Path.GetExtension(manifest.Icon), ".ico", StringComparison.OrdinalIgnoreCase) ||
            manifest.Icon.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidDataException("Use a package-relative .ico file, not an absolute path or URL.");
        }

        var folder = Path.GetFullPath(manifest.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(folder, manifest.Icon));
        if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The icon must remain inside the script package.");
        }

        FileSystemInfo current = new FileInfo(path);
        while (true)
        {
            if (!current.Exists)
            {
                throw new FileNotFoundException("The icon or its parent directory is missing or inaccessible.", current.FullName);
            }

            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Script icons cannot use symbolic links or junctions.");
            }

            if (string.Equals(current.FullName, folder, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = new DirectoryInfo(Path.GetDirectoryName(current.FullName)!);
        }

        using var stream = File.OpenRead(path);
        if (stream.Length > 1024 * 1024)
        {
            throw new InvalidDataException("Script icons must not exceed 1 MiB.");
        }

        Span<byte> header = stackalloc byte[6];
        stream.ReadExactly(header);
        if (header[0] != 0 || header[1] != 0 || header[2] != 1 || header[3] != 0 ||
            (header[4] == 0 && header[5] == 0))
        {
            throw new InvalidDataException("The file does not contain an ICO header.");
        }

        return path;
    }

    /// <summary>Scripts with no file input (resolved input shape is not Files) — candidates for Keyboard Manager / Command Palette.</summary>
    public IEnumerable<PowerScriptManifest> SystemScripts =>
        _scripts.Where(s => s.InputFormat != PowerScriptDataFormat.Files);

    /// <summary>
    /// File scripts whose declared input extensions match the given file extension (e.g. ".png").
    /// A declared extension of "*" matches anything. Used to build the right-click submenu.
    /// </summary>
    public IEnumerable<PowerScriptManifest> FileScriptsFor(string extension)
    {
        var ext = NormalizeExtension(extension);
        return _scripts.Where(s =>
            s.InputFormat == PowerScriptDataFormat.Files &&
            s.Input is not null &&
            s.Input.Extensions.Any(e => MatchesExtension(e, ext)));
    }

    /// <summary>
    /// File scripts that accept <em>all</em> of the given files (every extension matches and the
    /// count is within the declared min/max). Used when a multi-file selection is right-clicked.
    /// </summary>
    public IEnumerable<PowerScriptManifest> FileScriptsForSelection(IReadOnlyCollection<string> files)
    {
        var extensions = files.Select(f => NormalizeExtension(Path.GetExtension(f))).Distinct().ToList();
        return _scripts.Where(s =>
            s.InputFormat == PowerScriptDataFormat.Files &&
            s.Input is not null &&
            extensions.All(ext => s.Input.Extensions.Any(e => MatchesExtension(e, ext))) &&
            files.Count >= s.Input.MinFiles &&
            (s.Input.MaxFiles == 0 || files.Count <= s.Input.MaxFiles));
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return string.Empty;
        }

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }

    private static bool MatchesExtension(string declared, string normalizedTarget)
    {
        if (declared == "*")
        {
            return true;
        }

        return string.Equals(NormalizeExtension(declared), normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }
}
