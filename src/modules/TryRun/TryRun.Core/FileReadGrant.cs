// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace PowerToys.TryRun.Core;

// A reviewed, exact-file policy addition. Hold the file and its ancestors against
// replacement until the retry finishes; a display string is never a grant target.
public sealed class FileReadGrant : IDisposable
{
    private readonly WorkspaceFileSystem.DirectoryLease parent;
    private readonly FileStream file;
    private readonly PolicySettings policy;

    private FileReadGrant(string path, PolicySettings policy, WorkspaceFileSystem.DirectoryLease parent, FileStream file, bool fromNativeDenial)
    {
        Path = path;
        this.policy = policy;
        this.parent = parent;
        this.file = file;
        FromNativeDenial = fromNativeDenial;
    }

    public string Path { get; }

    public PolicySettings Policy => policy.Clone();

    public bool FromNativeDenial { get; }

    public static bool CanReview(IsolationEvent observation) => observation is
    {
        Source: "MXC denial capture (block)",
        Outcome: "Blocked",
        NativeDenial: { ResourceType: "file", Access: "read" },
    };

    public static FileReadGrant Create(IsolationEvent observation, ExecutionRequest request, RunEnvironment environment)
    {
        if (!CanReview(observation) || request.IsLinux || request.Policy is not { } original || !original.Enabled("captureEnabled") || original.Get("captureMode") != "Block")
        {
            throw new ArgumentException("Only native blocked file-read records from this Windows run can be reviewed.");
        }

        return CreatePath(observation.NativeDenial!.Resource, request, environment, fromNativeDenial: true);
    }

    public static FileReadGrant CreateSelectedFile(string path, ExecutionRequest request, RunEnvironment environment)
    {
        return CreatePath(path, request, environment, fromNativeDenial: false);
    }

    private static FileReadGrant CreatePath(string path, ExecutionRequest request, RunEnvironment environment, bool fromNativeDenial)
    {
        if (request.IsLinux || request.Policy is not { } original || original.Get("captureMode") != "Block")
        {
            throw new ArgumentException("File access review requires a Windows run without permissive capture.");
        }

        path = ValidatePath(path);
        if (WorkspacePath.IsWithin(path, System.IO.Path.GetDirectoryName(request.WorkingDirectory)!) ||
            environment.ReadOnlyFolders.Concat(environment.WritableFolders).Any(grant => WorkspacePath.IsWithin(path, grant)))
        {
            throw new ArgumentException("This path is already covered by this run's grants or belongs to its temporary workspace. Adding it would not resolve the failure.");
        }

        if (original.Lines("deniedPaths").Any(denied => WorkspacePath.IsWithin(path, PolicyPaths.Normalize(denied))))
        {
            throw new ArgumentException("This file is explicitly denied. Review that restriction in Run permissions; it will not be removed automatically.");
        }

        var updated = original.Clone();
        updated.Values["readonlyPaths"] = string.Join('\n', original.Lines("readonlyPaths").Append(path));
        updated.Validate(false);
        var parent = new WorkspaceFileSystem.DirectoryLease(System.IO.Path.GetDirectoryName(path)!);
        FileStream? file = null;
        try
        {
            file = WorkspaceFileSystem.OpenRead(path);
            if (!string.Equals(path, PhysicalDirectory.Resolve(file.SafeFileHandle), StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The file path is redirected. Select the intended file in configuration instead.");
            }

            return new FileReadGrant(path, updated, parent, file, fromNativeDenial);
        }
        catch
        {
            file?.Dispose();
            parent.Dispose();
            throw;
        }
    }

    public static string ValidatePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length > 16000 || path.Any(character => char.IsControl(character) || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format))
        {
            throw new ArgumentException("This path cannot be used for a precise file grant.");
        }

        var normalized = WorkspacePath.LocalPath(path);
        if (!string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase) || path.Length <= 3)
        {
            throw new ArgumentException("The path must identify one absolute local file without aliases or traversal.");
        }

        var parts = path[3..].Split('\\');
        if (parts.Length > 64)
        {
            throw new ArgumentException("The file path is too deeply nested.");
        }

        foreach (var part in parts)
        {
            WorkspacePath.ValidateRelative(part);
        }

        if (new DriveInfo(path[..3]).DriveType is not DriveType.Fixed and not DriveType.Removable)
        {
            throw new ArgumentException("Only local files are supported by this action. Network and device paths require separate configuration.");
        }

        return normalized;
    }

    public void Dispose()
    {
        file.Dispose();
        parent.Dispose();
    }
}
