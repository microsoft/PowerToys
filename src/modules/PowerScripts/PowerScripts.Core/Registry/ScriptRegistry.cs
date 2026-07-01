// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Manifest;

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

    public ScriptRegistry(string? root = null)
    {
        Root = PowerScriptsPaths.ResolveScriptsRoot(root);
    }

    /// <summary>Absolute path to the scanned scripts root.</summary>
    public string Root { get; }

    public IReadOnlyList<PowerScriptManifest> Scripts => _scripts;

    public IReadOnlyList<ScriptLoadError> Errors => _errors;

    /// <summary>
    /// Scans <see cref="Root"/> for <c>&lt;id&gt;/manifest.json</c> folders, parses and validates each,
    /// and rebuilds the in-memory catalogue. Bad scripts are recorded in <see cref="Errors"/> and skipped.
    /// </summary>
    public void Load()
    {
        _scripts.Clear();
        _errors.Clear();

        if (!Directory.Exists(Root))
        {
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            var manifestPath = Path.Combine(folder, PowerScriptsPaths.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            PowerScriptManifest? manifest;
            try
            {
                manifest = ManifestSerializer.Deserialize(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                _errors.Add(new ScriptLoadError(folder, $"failed to parse manifest.json: {ex.Message}"));
                continue;
            }

            if (manifest is null)
            {
                _errors.Add(new ScriptLoadError(folder, "manifest.json deserialized to null."));
                continue;
            }

            manifest.FolderPath = folder;

            var folderName = new DirectoryInfo(folder).Name;
            var validationErrors = ManifestValidator.Validate(manifest, folderName);
            if (validationErrors.Count > 0)
            {
                _errors.Add(new ScriptLoadError(folder, string.Join(" ", validationErrors)));
                continue;
            }

            // Ids are the portable identity and must be unique across the catalogue, since every
            // surface resolves a script by id. A collision (e.g. two adopted scripts sharing an id)
            // is reported and the duplicate skipped rather than silently shadowed.
            if (!seenIds.Add(manifest.Id))
            {
                _errors.Add(new ScriptLoadError(folder, $"duplicate id '{manifest.Id}' - already defined by another script; skipped."));
                continue;
            }

            _scripts.Add(manifest);
        }

        _scripts.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    public PowerScriptManifest? Get(string id) =>
        _scripts.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>System scripts (no file input) — candidates for Keyboard Manager / Command Palette.</summary>
    public IEnumerable<PowerScriptManifest> SystemScripts =>
        _scripts.Where(s => s.Kind == ScriptKind.System);

    /// <summary>
    /// File scripts whose declared input extensions match the given file extension (e.g. ".png").
    /// A declared extension of "*" matches anything. Used to build the right-click submenu.
    /// </summary>
    public IEnumerable<PowerScriptManifest> FileScriptsFor(string extension)
    {
        var ext = NormalizeExtension(extension);
        return _scripts.Where(s =>
            s.Kind == ScriptKind.File &&
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
            s.Kind == ScriptKind.File &&
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
