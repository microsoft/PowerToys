// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed class RunExecution : IDisposable
{
    private readonly object gate = new();
    private readonly string workerPath;
    private readonly ExecutionRequest request;
    private readonly string[] inputPaths;
    private readonly bool verifyEntryKind;
    private readonly CancellationTokenSource lifetime = new();
    private bool started;
    private bool active;
    private bool disposed;
    private bool cleaned;

    internal RunExecution(string workerPath, RunSession session, FileWorkspace workspace, ExecutionRequest request, string[] inputPaths, PolicyProfileReference? profile, bool verifyEntryKind)
    {
        this.workerPath = workerPath;
        this.request = Copy(request);
        this.inputPaths = inputPaths.ToArray();
        this.verifyEntryKind = verifyEntryKind;
        Session = session;
        Workspace = workspace;
        Profile = profile;
    }

    public RunSession Session { get; }

    public FileWorkspace Workspace { get; }

    public ExecutionRequest Request => Copy(request);

    public string[] InputPaths => inputPaths.ToArray();

    public PolicyProfileReference? Profile { get; }

    public Task<WorkerMessage> RunAsync(IProgress<WorkerMessage> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (started || active)
            {
                throw new InvalidOperationException("A prepared workspace can run only once. Prepare a fresh execution to retry.");
            }

            started = true;
            active = true;
        }

        return RunCoreAsync(progress, cancellationToken);
    }

    public Task<RunReview> ReviewAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (active)
            {
                throw new InvalidOperationException("Wait for the current run or review to stop before reviewing files.");
            }

            active = true;
        }

        return ReviewCoreAsync(cancellationToken);
    }

    public void Dispose()
    {
        bool cleanNow;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cleanNow = !active;
        }

        try
        {
            // WorkerClient closes its input and waits for worker shutdown. Its
            // task retains ownership until the native run has stopped.
            lifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrently completing operation already cleaned this run.
        }
        finally
        {
            if (cleanNow)
            {
                Cleanup();
            }
        }
    }

    private static ExecutionRequest Copy(ExecutionRequest value) => value with { Arguments = value.Arguments.ToArray(), Policy = value.Policy?.Clone() };

    private async Task<WorkerMessage> RunCoreAsync(IProgress<WorkerMessage> progress, CancellationToken cancellationToken)
    {
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Run(
                () =>
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    RunCoordinator.ValidateWorkspace(request, verifyEntryKind);
                    if (Workspace.Review(cancellation.Token).Any(change => change.Kind != FileChangeKind.Unchanged))
                    {
                        throw new IOException("The prepared workspace changed before execution. Prepare a fresh run.");
                    }

                    if (request.ApplicationPath is { } application && !string.Equals(RuntimeFile.Resolve(application), application, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("The installed application was redirected. Select the application again.");
                    }
                },
                cancellation.Token).ConfigureAwait(false);
            return await new WorkerClient(workerPath).RunAsync(request, progress, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            CompleteOperation();
        }
    }

    private async Task<RunReview> ReviewCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            return await Task.Run(
                () =>
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var originals = Workspace.CheckOriginals(cancellation.Token);
                    var changes = Workspace.Review(cancellation.Token);
                    return new RunReview(originals, changes);
                },
                cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            CompleteOperation();
        }
    }

    private void CompleteOperation()
    {
        bool cleanNow;
        lock (gate)
        {
            active = false;
            cleanNow = disposed;
        }

        if (cleanNow)
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        lock (gate)
        {
            if (cleaned)
            {
                return;
            }

            cleaned = true;
        }

        lifetime.Dispose();
        Session.Dispose();
    }
}
