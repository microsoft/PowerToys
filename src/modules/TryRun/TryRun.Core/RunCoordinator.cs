// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed class RunCoordinator
{
    private readonly string workerPath;
    private readonly PolicyProfileStore profileStore;

    public RunCoordinator(string workerPath, PolicyProfileStore? profileStore = null)
    {
        this.workerPath = WorkspacePath.LocalPath(workerPath);
        this.profileStore = profileStore ?? new PolicyProfileStore();
    }

    public Task<RunExecution> PrepareAsync(RunConfiguration configuration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.InputPaths);
        ArgumentNullException.ThrowIfNull(configuration.Arguments);
        cancellationToken.ThrowIfCancellationRequested();

        // Callers own their configuration objects. Snapshot mutable collections
        // before scheduling work so later UI edits cannot alter this execution.
        var snapshot = configuration with
        {
            InputPaths = configuration.InputPaths.ToArray(),
            Arguments = configuration.Arguments.ToArray(),
            Policy = configuration.Policy?.Clone(),
        };
        return Task.Run(() => Prepare(snapshot, cancellationToken), cancellationToken);
    }

    private RunExecution Prepare(RunConfiguration configuration, CancellationToken cancellationToken)
    {
        if (configuration.WorkloadFile is not null && (configuration.ApplicationPath is not null || configuration.FileRelativePath is not null || configuration.Kind == WorkloadKind.WindowsApplication))
        {
            throw new ArgumentException("Choose a copied workload file or an installed Windows application, not both.");
        }

        var policy = configuration.Policy;
        PolicyProfileReference? profile = null;
        if (configuration.Profile is { } selected)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selected.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(selected.Revision);
            if (policy is not null)
            {
                throw new ArgumentException("A saved policy cannot be combined with inline policy overrides. Save a new profile revision instead.");
            }

            if (configuration.PrepareImage)
            {
                throw new ArgumentException("Image preparation is a separate setup operation and cannot use a workload policy profile.");
            }

            var saved = profileStore.Load(selected.Id, selected.Revision);
            var linux = configuration.Kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython or WorkloadKind.LinuxApplication;
            if (saved.Linux != linux)
            {
                throw new ArgumentException("The saved policy belongs to a different execution backend. Select a policy for this workload's backend.");
            }

            policy = saved.Policy.Clone();
            profile = new PolicyProfileReference(saved.Id, saved.Revision);
        }

        var selectedPaths = TaskBundle.ParseLaunchArguments(configuration.InputPaths);
        if (configuration.WorkloadFile is { } workloadFile)
        {
            selectedPaths = TaskBundle.ParseLaunchArguments(selectedPaths.Append(workloadFile));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var bundle = TaskBundle.Inspect(selectedPaths, cancellationToken);
        var relativeFile = configuration.FileRelativePath is { } selectedFile ? WorkspacePath.ValidateRelative(selectedFile) : configuration.WorkloadFile is { } manualFile ? bundle.GetRelativePath(manualFile) : null;
        var selectedEntry = configuration.FileRelativePath is null ? null : bundle.EntryPoints.SingleOrDefault(entry => entry.RelativePath.Equals(relativeFile, StringComparison.OrdinalIgnoreCase) && entry.Kind == configuration.Kind);
        if (configuration.FileRelativePath is not null && selectedEntry is null)
        {
            throw new IOException("The selected entry point changed or was removed. Select the files again.");
        }

        var session = new RunSession();
        try
        {
            var request = new ExecutionRequest(configuration.Script, session.WorkingDirectory, session.TemporaryDirectory, configuration.TimeoutSeconds)
            {
                Kind = configuration.Kind,
                ApplicationPath = configuration.ApplicationPath,
                FileRelativePath = relativeFile,
                WorkingSubdirectory = configuration.WorkingSubdirectory ?? selectedEntry?.WorkingSubdirectory,
                Arguments = configuration.Arguments,
                Policy = policy,
                Image = configuration.Image,
                ImageTarPath = configuration.ImageTarPath,
                Interpreter = configuration.Interpreter,
                CaptureDenials = policy?.Enabled("captureEnabled") ?? configuration.CaptureDenials,
                IsolationDemo = configuration.IsolationDemo,
                PrepareImage = configuration.PrepareImage,
            };
            request.Validate();

            // Installed applications intentionally stay in their installation
            // directory, which may be protected and contain hard-linked files.
            // Selected inputs instead pass through the stricter copy scanner.
            if (request.ApplicationPath is { } application)
            {
                request = request with { ApplicationPath = RuntimeFile.Resolve(application) };
            }

            var workspace = new FileWorkspace(session);
            workspace.Import(bundle.Inputs, cancellationToken);
            ValidateWorkspace(request, configuration.FileRelativePath is not null);
            cancellationToken.ThrowIfCancellationRequested();
            return new RunExecution(workerPath, session, workspace, request, bundle.Inputs.ToArray(), profile, configuration.FileRelativePath is not null);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal static void ValidateWorkspace(ExecutionRequest request, bool verifyEntryKind)
    {
        request.Validate();
        RunSession.ValidateDirectories(request.WorkingDirectory, request.TemporaryDirectory);
        var working = request.WorkingSubdirectory is null ? request.WorkingDirectory : Path.Combine(request.WorkingDirectory, WorkspacePath.ValidateRelative(request.WorkingSubdirectory));
        RunSession.ValidateWorkspacePath(working, request.WorkingDirectory);
        if (request.FileRelativePath is { } relative)
        {
            var path = Path.Combine(request.WorkingDirectory, WorkspacePath.ValidateRelative(relative));
            using var parent = new WorkspaceFileSystem.DirectoryLease(Path.GetDirectoryName(path)!);
            using var file = WorkspaceFileSystem.OpenRead(Path.Combine(parent.Path, Path.GetFileName(path)));
            if (!string.Equals(PhysicalDirectory.Resolve(file.SafeFileHandle), path, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The selected workload file is missing or redirected.");
            }

            if (verifyEntryKind)
            {
                var header = new byte[(int)Math.Min(file.Length, EntryPointDetector.HeaderBytes)];
                file.ReadExactly(header);
                if (EntryPointDetector.Detect(relative, header)?.Kind != request.Kind)
                {
                    throw new IOException("The selected entry point changed while being copied. Select the files again.");
                }
            }
        }
    }
}
