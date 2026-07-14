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

    private static readonly string[] HeaderScriptExtensions = { ".ps1", ".py" };

    /// <summary>
    /// Rebuilds the in-memory catalogue by scanning <see cref="Root"/> for scripts, each of which is a
    /// single self-contained file carrying its metadata in a leading <c>@powerscript.*</c> comment
    /// header (see <see cref="ScriptHeaderParser"/>). Scripts are discovered either as a loose file
    /// directly under the root (e.g. <c>whats-my-ip.ps1</c>) or as the one header script inside a
    /// sub-folder (which lets a script keep companion assets). Surfaces are inferred from the script
    /// kind when none are declared. Bad scripts are recorded in <see cref="Errors"/> and skipped.
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

        void TryAdd(PowerScriptManifest manifest, string folder, string context)
        {
            manifest.FolderPath = folder;

            // Authors don't hand-list surfaces; the ingestion side infers them from the kind.
            SurfaceInference.ApplyDefaults(manifest);

            var validationErrors = ManifestValidator.Validate(manifest, new DirectoryInfo(folder).Name);
            if (validationErrors.Count > 0)
            {
                _errors.Add(new ScriptLoadError(context, string.Join(" ", validationErrors)));
                return;
            }

            // Ids are the portable identity and must be unique across the catalogue, since every
            // surface resolves a script by id. A collision (e.g. two adopted scripts sharing an id)
            // is reported and the duplicate skipped rather than silently shadowed.
            if (!seenIds.Add(manifest.Id))
            {
                _errors.Add(new ScriptLoadError(context, $"duplicate id '{manifest.Id}' - already defined by another script; skipped."));
                return;
            }

            _scripts.Add(manifest);
        }

        // Loose header script files placed directly under the root (single-file scripts).
        foreach (var file in EnumerateHeaderCandidates(Root))
        {
            var manifest = ScriptHeaderParser.TryParseFile(file);
            if (manifest is not null)
            {
                TryAdd(manifest, Root, file);
            }
        }

        // A header script inside a sub-folder (keeps companion assets next to the script).
        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            var file = FindHeaderScript(folder);
            if (file is not null)
            {
                var manifest = ScriptHeaderParser.TryParseFile(file);
                if (manifest is not null)
                {
                    TryAdd(manifest, folder, file);
                }
            }
        }

        _scripts.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the path of the first script file inside <paramref name="folder"/> that carries embedded
    /// <c>@powerscript.*</c> metadata, or null when none qualifies.
    /// </summary>
    private static string? FindHeaderScript(string folder)
    {
        foreach (var file in EnumerateHeaderCandidates(folder))
        {
            if (ScriptHeaderParser.TryParseFile(file) is not null)
            {
                return file;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateHeaderCandidates(string directory) =>
        Directory.EnumerateFiles(directory)
            .Where(f => HeaderScriptExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

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
