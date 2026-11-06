// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerScripts.Core.AgentSkills;

/// <summary>
/// Installs the portable skill without adopting or replacing user-authored copies.
/// All operations reload state under an exclusive lock; callers should surface I/O failures.
/// </summary>
public sealed class AgentSkillManager
{
    private const string SkillDirectoryName = "powertoys-powerscripts";
    private static readonly AgentSkillTarget[] PersonalTargets = [AgentSkillTarget.Copilot, AgentSkillTarget.ClaudeCode, AgentSkillTarget.Codex];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private readonly string bundleDirectory;
    private readonly string stateFilePath;
    private readonly string userProfileDirectory;

    public AgentSkillManager(string bundleDirectory, string stateFilePath, string userProfileDirectory)
    {
        this.bundleDirectory = Normalize(bundleDirectory);
        this.stateFilePath = Normalize(stateFilePath);
        this.userProfileDirectory = Normalize(userProfileDirectory);
        RejectOverlap(this.bundleDirectory, this.stateFilePath);
        RejectOverlap(this.bundleDirectory, this.stateFilePath + ".lock");
        foreach (var target in PersonalTargets)
        {
            var destination = Destination(target);
            RejectOverlap(destination, this.bundleDirectory);
            RejectOverlap(destination, this.stateFilePath);
            RejectOverlap(destination, this.stateFilePath + ".lock");
        }
    }

    public IReadOnlyList<AgentSkillStatus> GetStatuses()
    {
        using var operationLock = AcquireLock();
        var state = LoadState();
        var bundle = ReadBundle();
        return Enum.GetValues<AgentSkillTarget>().Select(target => Status(target, state, bundle.Version)).ToArray();
    }

    public AgentSkillStatus Install(AgentSkillTarget target, bool keepUpdated)
    {
        using var operationLock = AcquireLock();
        var state = LoadState();
        var bundle = ReadBundle();
        return InstallCore(target, keepUpdated, state, bundle, allowReinstall: true);
    }

    public AgentSkillStatus Update(AgentSkillTarget target)
    {
        using var operationLock = AcquireLock();
        var state = LoadState();
        var bundle = ReadBundle();
        var status = Status(target, state, bundle.Version);
        return InstallCore(target, status.KeepUpdated, state, bundle, allowReinstall: false);
    }

    public void SetCustomDirectory(string skillsDirectory)
    {
        using var operationLock = AcquireLock();
        var state = LoadState();
        var parent = Normalize(skillsDirectory);
        if (!Directory.Exists(parent))
        {
            throw new DirectoryNotFoundException("Choose an existing skills folder.");
        }

        ValidateCustomDestination(Child(parent, SkillDirectoryName), state);
        SaveState(state with { CustomDirectory = parent });
    }

    public void SetAutomaticUpdates(AgentSkillTarget target, bool enabled)
    {
        using var operationLock = AcquireLock();
        var state = LoadState();
        var status = Status(target, state, ReadBundle().Version);
        var copy = GetCopy(state, status.DestinationDirectory);
        if (copy == null ||
            (enabled && status.State is not (AgentSkillInstallState.Installed or AgentSkillInstallState.UpdateAvailable)))
        {
            throw new InvalidOperationException("Install an intact managed skill before enabling automatic updates.");
        }

        SetCopy(state, status.DestinationDirectory, copy with { KeepUpdated = enabled });
        SaveState(state);
    }

    public void SynchronizeAutomaticUpdates()
    {
        using var operationLock = AcquireLock();
        var state = LoadState();
        var bundle = ReadBundle();
        foreach (var target in PersonalTargets)
        {
            var status = Status(target, state, bundle.Version);
            if (status.KeepUpdated && status.State == AgentSkillInstallState.UpdateAvailable)
            {
                InstallCore(target, true, state, bundle, allowReinstall: false);
            }
        }

        foreach (var destination in state.CustomCopies.Keys.ToArray())
        {
            var status = Status(AgentSkillTarget.Custom, state, bundle.Version, destination);
            if (status.KeepUpdated && status.State == AgentSkillInstallState.UpdateAvailable)
            {
                InstallCore(AgentSkillTarget.Custom, true, state, bundle, allowReinstall: false, destination);
            }
        }
    }

    /// <summary>
    /// Creates an unmanaged copy in a new powertoys-powerscripts child directory.
    /// </summary>
    public void Export(string destinationParentDirectory)
    {
        using var operationLock = AcquireLock();
        var parent = Normalize(destinationParentDirectory);
        var destination = Child(parent, SkillDirectoryName);
        RejectOverlap(destination, bundleDirectory);
        RejectOverlap(destination, stateFilePath);
        RejectOverlap(destination, stateFilePath + ".lock");
        var bundle = ReadBundle();
        EnsureMissing(destination);
        Directory.CreateDirectory(parent);
        CheckPath(parent);
        var stage = CreateStage(parent);
        try
        {
            CopyBundle(stage, bundle);
            EnsureMissing(destination);
            Directory.Move(stage, destination);
        }
        finally
        {
            DeleteOwnedTree(stage);
        }
    }

    private AgentSkillStatus InstallCore(AgentSkillTarget target, bool keepUpdated, InstallationState state, Bundle bundle, bool allowReinstall, string? customDestination = null)
    {
        var status = Status(target, state, bundle.Version, customDestination);
        if (string.IsNullOrEmpty(status.DestinationDirectory))
        {
            throw new InvalidOperationException("Choose a custom skills folder before installing.");
        }

        if (status.State is AgentSkillInstallState.Conflict or AgentSkillInstallState.Modified)
        {
            throw new IOException("The skill directory is unowned or modified. Preserve it and choose a different location.");
        }

        if (!allowReinstall && status.State == AgentSkillInstallState.NotInstalled)
        {
            throw new InvalidOperationException("The skill directory is missing. Reinstall it explicitly.");
        }

        var destination = status.DestinationDirectory;
        var parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        CheckPath(parent);
        var stage = CreateStage(parent);
        var backup = Child(parent, "." + SkillDirectoryName + ".backup-" + Guid.NewGuid().ToString("N"));
        var movedOld = false;
        var movedNew = false;
        var committed = false;
        try
        {
            CopyBundle(stage, bundle);
            var current = Status(target, state, bundle.Version, customDestination);
            if (current != status)
            {
                throw new IOException("The skill changed while installation was being prepared.");
            }

            if (status.State != AgentSkillInstallState.NotInstalled)
            {
                EnsureMissing(backup);
                Directory.Move(destination, backup);
                movedOld = true;
            }

            EnsureMissing(destination);
            Directory.Move(stage, destination);
            movedNew = true;
            SetCopy(state, destination, new InstalledCopy(bundle.Version, keepUpdated, bundle.Tree.Files, bundle.Tree.Directories));
            SaveState(state);
            committed = true;
        }
        catch (Exception installationError)
        {
            try
            {
                if (movedNew)
                {
                    CheckPath(destination);
                    Directory.Move(destination, stage);
                }

                if (movedOld)
                {
                    CheckPath(backup);
                    EnsureMissing(destination);
                    Directory.Move(backup, destination);
                }
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Installation and rollback failed. The backup has been preserved at " + backup, installationError, rollbackError);
            }

            throw;
        }
        finally
        {
            DeleteOwnedTree(stage);
        }

        if (committed && movedOld)
        {
            DeleteOwnedTree(backup);
        }

        return Status(target, state, bundle.Version, customDestination);
    }

    private AgentSkillStatus Status(AgentSkillTarget target, InstallationState state, string availableVersion, string? customDestination = null)
    {
        var destination = target == AgentSkillTarget.Custom
            ? customDestination ?? (state.CustomDirectory == null ? null : Child(state.CustomDirectory, SkillDirectoryName))
            : Destination(target);
        if (destination == null)
        {
            return new(target, string.Empty, availableVersion, null, AgentSkillInstallState.NotInstalled, false);
        }

        var attributes = Attributes(destination);
        var copy = GetCopy(state, destination);
        AgentSkillInstallState result;
        if (attributes == null)
        {
            result = AgentSkillInstallState.NotInstalled;
        }
        else if (copy == null || !attributes.Value.HasFlag(FileAttributes.Directory))
        {
            result = AgentSkillInstallState.Conflict;
        }
        else if (!Matches(ReadTree(destination), new Tree(copy.Files, copy.Directories)))
        {
            result = AgentSkillInstallState.Modified;
        }
        else
        {
            result = copy.Version == availableVersion ? AgentSkillInstallState.Installed : AgentSkillInstallState.UpdateAvailable;
        }

        // A removed directory is never silently recreated by automatic synchronization.
        return new(target, destination, availableVersion, copy?.Version, result, attributes != null && (copy?.KeepUpdated ?? false));
    }

    private InstalledCopy? GetCopy(InstallationState state, string destination)
    {
        foreach (var target in PersonalTargets)
        {
            if (string.Equals(destination, Destination(target), StringComparison.OrdinalIgnoreCase))
            {
                return state.Targets.GetValueOrDefault(target);
            }
        }

        return state.CustomCopies.GetValueOrDefault(destination);
    }

    private void SetCopy(InstallationState state, string destination, InstalledCopy copy)
    {
        foreach (var target in PersonalTargets)
        {
            if (string.Equals(destination, Destination(target), StringComparison.OrdinalIgnoreCase))
            {
                state.Targets[target] = copy;
                return;
            }
        }

        state.CustomCopies[destination] = copy;
    }

    private void ValidateCustomDestination(string destination, InstallationState state)
    {
        RejectOverlap(destination, bundleDirectory);
        RejectOverlap(destination, stateFilePath);
        RejectOverlap(destination, stateFilePath + ".lock");
        foreach (var managed in PersonalTargets.Select(Destination).Concat(state.CustomCopies.Keys))
        {
            if (!string.Equals(destination, managed, StringComparison.OrdinalIgnoreCase))
            {
                RejectOverlap(destination, managed);
            }
        }
    }

    private string Destination(AgentSkillTarget target)
    {
        var agent = target switch
        {
            AgentSkillTarget.Copilot => ".copilot",
            AgentSkillTarget.ClaudeCode => ".claude",
            AgentSkillTarget.Codex => ".agents",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        return Child(userProfileDirectory, Path.Combine(agent, "skills", SkillDirectoryName));
    }

    private FileStream AcquireLock()
    {
        CheckPath(bundleDirectory);
        CheckPath(userProfileDirectory);
        CheckPath(stateFilePath);
        var lockPath = stateFilePath + ".lock";
        CheckPath(lockPath);
        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
        CheckPath(lockPath);
        // FileShare.None rejects simultaneous operations, including in another process.
        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private InstallationState LoadState()
    {
        if (Attributes(stateFilePath) == null)
        {
            return new InstallationState(1, userProfileDirectory, new());
        }

        InstallationState state;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(stateFilePath));
            RejectDuplicateProperties(document.RootElement);
            state = document.RootElement.Deserialize<InstallationState>(JsonOptions)
                ?? throw new InvalidDataException("The agent skill state is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The agent skill state is malformed.", error);
        }

        if (state.FormatVersion != 1 || state.Targets == null || state.CustomCopies == null ||
            !string.Equals(state.UserProfileDirectory, userProfileDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The agent skill state has an unsupported format or belongs to another profile.");
        }

        foreach (var (target, copy) in state.Targets)
        {
            if (!PersonalTargets.Contains(target))
            {
                throw new InvalidDataException("The agent skill state contains an invalid personal target.");
            }

            ValidateCopy(copy);
        }

        state = state with { CustomCopies = new(state.CustomCopies, StringComparer.OrdinalIgnoreCase) };
        foreach (var (destination, copy) in state.CustomCopies)
        {
            if (!string.Equals(destination, Normalize(destination), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(destination), SkillDirectoryName, StringComparison.OrdinalIgnoreCase) ||
                PersonalTargets.Any(target => string.Equals(destination, Destination(target), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("The custom skill ownership path is invalid.");
            }

            ValidateCustomDestination(destination, state);
            ValidateCopy(copy);
        }

        if (state.CustomDirectory != null)
        {
            if (!string.Equals(state.CustomDirectory, Normalize(state.CustomDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The custom skills folder path is invalid.");
            }

            ValidateCustomDestination(Child(state.CustomDirectory, SkillDirectoryName), state);
        }

        return state;
    }

    private static void ValidateCopy(InstalledCopy copy)
    {
        if (copy == null || string.IsNullOrWhiteSpace(copy.Version) ||
            copy.Files == null || copy.Directories == null || !copy.Files.ContainsKey("SKILL.md") || !copy.Files.ContainsKey("bundle.json"))
        {
            throw new InvalidDataException("The agent skill ownership record is incomplete.");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in copy.Directories.Concat(copy.Files.Keys))
        {
            ValidateRelativePath(path);
            if (!paths.Add(path))
            {
                throw new InvalidDataException("The agent skill state contains duplicate paths.");
            }
        }

        foreach (var hash in copy.Files.Values)
        {
            if (hash == null || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("The agent skill state contains an invalid SHA256 hash.");
            }
        }
    }

    private void SaveState(InstallationState state)
    {
        CheckPath(stateFilePath);
        var pending = stateFilePath + ".write-" + Guid.NewGuid().ToString("N");
        CheckPath(pending);
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            CheckPath(stateFilePath);
            File.Move(pending, stateFilePath, overwrite: true);
        }
        finally
        {
            CheckPath(pending);
            File.Delete(pending);
        }
    }

    private Bundle ReadBundle()
    {
        var tree = ReadTree(bundleDirectory);
        if (!tree.Files.ContainsKey("SKILL.md") || !tree.Files.ContainsKey("bundle.json"))
        {
            throw new InvalidDataException("The skill bundle must contain SKILL.md and bundle.json.");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Child(bundleDirectory, "bundle.json")));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("version", out var value) ||
                value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new InvalidDataException("The skill bundle must declare a version.");
            }

            return new Bundle(value.GetString()!, tree);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The skill bundle metadata is malformed.", error);
        }
    }

    private void CopyBundle(string stage, Bundle bundle)
    {
        foreach (var directory in bundle.Tree.Directories)
        {
            Directory.CreateDirectory(Child(stage, directory));
        }

        foreach (var file in bundle.Tree.Files.Keys)
        {
            var source = Child(bundleDirectory, file);
            var destination = Child(stage, file);
            CheckPath(source);
            CheckPath(destination);
            File.Copy(source, destination, overwrite: false);
            File.SetAttributes(destination, FileAttributes.Normal);
        }

        if (!Matches(ReadTree(stage), bundle.Tree) || !Matches(ReadTree(bundleDirectory), bundle.Tree))
        {
            throw new IOException("The bundle changed while the skill was being copied.");
        }
    }

    private static Tree ReadTree(string root)
    {
        CheckPath(root);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directories = new List<string>();
        Visit(root);
        return new Tree(files, directories.ToArray());

        void Visit(string directory)
        {
            CheckPath(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                CheckPath(entry);
                var relative = Path.GetRelativePath(root, entry);
                ValidateRelativePath(relative);
                if (File.GetAttributes(entry).HasFlag(FileAttributes.Directory))
                {
                    directories.Add(relative);
                    Visit(entry);
                }
                else
                {
                    using var stream = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read);
                    files.Add(relative, Convert.ToHexString(SHA256.HashData(stream)));
                }
            }
        }
    }

    private static bool Matches(Tree left, Tree right)
    {
        return left.Files.Count == right.Files.Count &&
            left.Files.All(pair => right.Files.TryGetValue(pair.Key, out var hash) && string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase)) &&
            new HashSet<string>(left.Directories, StringComparer.OrdinalIgnoreCase).SetEquals(right.Directories);
    }

    private static string CreateStage(string parent)
    {
        var stage = Child(parent, "." + SkillDirectoryName + ".stage-" + Guid.NewGuid().ToString("N"));
        EnsureMissing(stage);
        Directory.CreateDirectory(stage);
        CheckPath(stage);
        return stage;
    }

    private static void DeleteOwnedTree(string root)
    {
        if (Attributes(root) == null)
        {
            return;
        }

        // Only generated staging/backup roots reach here. Never use recursive Delete:
        // enumerate and reject reparse points before deleting any owned content.
        var name = Path.GetFileName(root);
        if (!name.StartsWith("." + SkillDirectoryName + ".stage-", StringComparison.Ordinal) &&
            !name.StartsWith("." + SkillDirectoryName + ".backup-", StringComparison.Ordinal))
        {
            throw new IOException("Refusing to delete a non-staging directory.");
        }

        var tree = ReadTree(root);
        foreach (var file in tree.Files.Keys)
        {
            var path = Child(root, file);
            CheckPath(path);
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }

        foreach (var directory in tree.Directories.OrderByDescending(path => path.Length))
        {
            var path = Child(root, directory);
            CheckPath(path);
            Directory.Delete(path);
        }

        CheckPath(root);
        Directory.Delete(root);
    }

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full)!;
        if (root.Length != 3 || root[1] != ':' || full.Length <= root.Length)
        {
            throw new ArgumentException("A non-root local Windows path is required.", nameof(path));
        }

        ValidateRelativePath(Path.GetRelativePath(root, full));
        CheckPath(full);
        return full;
    }

    private static string Child(string root, string relative)
    {
        ValidateRelativePath(relative);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A skill path escapes its containing directory.");
        }

        CheckPath(full);
        return full;
    }

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('/'))
        {
            throw new InvalidDataException("A skill path must be a canonical relative Windows path.");
        }

        foreach (var part in path.Split('\\'))
        {
            var stem = part.Split('.')[0];
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && char.IsDigit(stem[3])))
            {
                throw new InvalidDataException("The skill contains an unsafe path.");
            }
        }
    }

    private static FileAttributes? Attributes(string path)
    {
        CheckPath(path);
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static void CheckPath(string path)
    {
        var current = Path.GetPathRoot(path)!;
        foreach (var part in path[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            try
            {
                if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new IOException("Agent skill paths cannot contain reparse points: " + current);
                }
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
        }
    }

    private static void EnsureMissing(string path)
    {
        if (Attributes(path) != null)
        {
            throw new IOException("The destination already exists: " + path);
        }
    }

    private static void RejectOverlap(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase) ||
            first.StartsWith(second + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            second.StartsWith(first + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Bundle, managed destinations, export and state paths must not overlap.");
        }

    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("The agent skill state contains duplicate JSON properties.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }

    private sealed record Tree(Dictionary<string, string> Files, string[] Directories);

    private sealed record Bundle(string Version, Tree Tree);

    private sealed record InstalledCopy(string Version, bool KeepUpdated, Dictionary<string, string> Files, string[] Directories);

    private sealed record InstallationState(int FormatVersion, string UserProfileDirectory, Dictionary<AgentSkillTarget, InstalledCopy> Targets)
    {
        public string? CustomDirectory { get; init; }

        public Dictionary<string, InstalledCopy> CustomCopies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
