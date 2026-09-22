// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.CmdPal;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Each execution owns and disposes its cancellation source in finally. Active pages remain reachable for cancellation.")]
internal sealed partial class SavedPolicyFilePage : DynamicListPage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string workerPath;
    private readonly PolicyProfileStore profiles;
    private readonly PolicyProfile profile;
    private readonly string policyJson;
    private readonly object stateLock = new();
    private OutputBuffer output = new();
    private CancellationTokenSource? cancellation;
    private string status = "Review the policy, paste a full Windows file path, and choose Run";
    private string? resultsDirectory;
    private string? metadata;
    private RunEnvironment? environment;

    public SavedPolicyFilePage(string workerPath, PolicyProfileStore profiles, PolicyProfile profile)
    {
        this.workerPath = workerPath;
        this.profiles = profiles;
        this.profile = profile;
        policyJson = JsonSerializer.Serialize(profile, JsonOptions);
        Id = "com.microsoft.powertoys.tryrun.saved." + profile.Id + "." + profile.Revision;
        Name = "Review policy";
        Title = "Run with " + profile.Name;
        PlaceholderText = "Paste a full .exe, .ps1, .cmd, or .bat path";
        Icon = new IconInfo("\uE72E");
        ShowDetails = true;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        lock (stateLock)
        {
            var items = new List<IListItem>();
            if (cancellation is not null)
            {
                items.Add(new ListItem(new AnonymousCommand(CancelRun) { Name = "Stop", Result = CommandResult.KeepOpen() }) { Title = "Stop this run", Subtitle = status });
            }
            else
            {
                try
                {
                    var selected = CmdPalLaunch.ParseQuery(SearchText);
                    if (selected.Length == 1)
                    {
                        var file = selected[0];
                        _ = WindowsKind(file);
                        items.Add(new ListItem(new AnonymousCommand(() => StartRun(file)) { Name = "Run", Result = CommandResult.KeepOpen() })
                        {
                            Title = "Run with " + profile.Name,
                            Subtitle = file,
                            Details = PolicyDetails(),
                        });
                    }
                    else
                    {
                        items.Add(new ListItem(new NoOpCommand()) { Title = "Paste a Windows executable or script path", Subtitle = "Selecting a policy does not start a program", Details = PolicyDetails() });
                    }
                }
                catch (ArgumentException exception)
                {
                    items.Add(new ListItem(new NoOpCommand()) { Title = "Choose a supported Windows file", Subtitle = exception.Message, Details = PolicyDetails() });
                }
            }

            items.Add(new ListItem(new CopyTextCommand(policyJson) { Name = "Copy policy JSON" }) { Title = "Review and copy the pinned policy", Subtitle = $"{profile.Id} · {profile.Revision}", Details = PolicyDetails() });
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = status,
                Subtitle = "Select this row to inspect output. Results and errors stay available on this page.",
                Details = new Details { Title = "Run output", Body = "```text\n" + output.ToString().Replace("```", "'''", StringComparison.Ordinal) + "\n```" },
            });
            if (resultsDirectory is not null)
            {
                items.Add(new ListItem(new OpenFileCommand(resultsDirectory)) { Title = "Open reviewed results", Subtitle = resultsDirectory });
            }

            if (metadata is not null)
            {
                items.Add(new ListItem(new CopyTextCommand(metadata) { Name = "Copy run metadata" }) { Title = "Copy run metadata", Subtitle = "Policy ID, revision, submitted policy, execution result, and file review" });
            }

            return items.ToArray();
        }
    }

    internal static WorkloadKind WindowsKind(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".exe" => WorkloadKind.WindowsApplication,
        ".ps1" => WorkloadKind.WindowsPowerShell,
        ".cmd" or ".bat" => WorkloadKind.WindowsBatch,
        _ => throw new ArgumentException("This entry supports Windows .exe, .ps1, .cmd, and .bat files. Linux workloads use the Try Run application."),
    };

    private Details PolicyDetails()
    {
        var permissive = profile.Policy.Get("captureMode") == "Allow" && profile.Policy.Enabled("captureEnabled")
            ? "**Capture Allow permits ungranted access. This is a permissive authoring policy.**\n\n"
            : string.Empty;
        return new Details
        {
            Title = "Pinned policy: " + profile.Name,
            Body = permissive + "This action uses the exact saved revision below. EXEs run from their installed location. The $app token resolves to the EXE's containing folder; the default read-only rule grants read access to that folder. Scripts run from a fresh copy; their original parent folder and argument paths do not automatically receive host access. Explicit writable host paths can change originals. This entry accepts one file and no arguments; use the CLI for additional inputs and literal arguments.\n\n```json\n" + policyJson + "\n```",
        };
    }

    private void StartRun(string file)
    {
        lock (stateLock)
        {
            if (cancellation is not null)
            {
                return;
            }

            cancellation = new CancellationTokenSource();
            output = new OutputBuffer();
            metadata = null;
            environment = null;
            resultsDirectory = null;
            status = "Preparing the pinned policy and a fresh workspace";
            IsLoading = true;
            _ = ExecuteAsync(file, cancellation);
        }

        RaiseItemsChanged();
    }

    private void CancelRun()
    {
        lock (stateLock)
        {
            cancellation?.Cancel();
            status = "Stopping the execution worker";
        }

        RaiseItemsChanged();
    }

    private async Task ExecuteAsync(string file, CancellationTokenSource stopping)
    {
        try
        {
            var kind = WindowsKind(file);
            using var run = await new RunCoordinator(workerPath, profiles).PrepareAsync(
                new RunConfiguration
                {
                    Kind = kind,
                    ApplicationPath = kind == WorkloadKind.WindowsApplication ? file : null,
                    WorkloadFile = kind != WorkloadKind.WindowsApplication ? file : null,
                    Profile = new PolicyProfileReference(profile.Id, profile.Revision),
                },
                stopping.Token).ConfigureAwait(false);
            lock (stateLock)
            {
                status = "Running with " + profile.Name + " · " + profile.Revision[..12];
                metadata = JsonSerializer.Serialize(new { profile.Id, profile.Revision, request = run.Request }, JsonOptions);
            }

            var result = await run.RunAsync(new MessageProgress(Receive), stopping.Token).ConfigureAwait(false);
            var review = await run.ReviewAsync(stopping.Token).ConfigureAwait(false);
            var exported = RunResultExport.Export(run.Workspace, review.Changes, null, stopping.Token);

            lock (stateLock)
            {
                resultsDirectory = exported;
                status = result.TimedOut ? "Time limit reached" : result.ExitCode == 0 ? "Completed successfully" : $"Application failed · exit code {result.ExitCode}";
                output.Append("\n" + review.Originals + "\n");
                metadata = JsonSerializer.Serialize(
                    new
                    {
                        profile.Id,
                        profile.Revision,
                        submittedPolicy = run.Request.Policy,
                        environment,
                        result,
                        review.Originals,
                        changes = review.Changes.Select(change => new { change.RelativePath, change.Kind }),
                        resultsDirectory = exported,
                    },
                    JsonOptions);
            }
        }
        catch (OperationCanceledException)
        {
            lock (stateLock)
            {
                status = "Cancelled · the execution worker has stopped";
            }
        }
        catch (Exception exception)
        {
            lock (stateLock)
            {
                status = "Run failed: " + exception.Message;
                output.Append("\n" + exception.Message);
            }
        }
        finally
        {
            lock (stateLock)
            {
                cancellation = null;
                stopping.Dispose();
                IsLoading = false;
            }

            RaiseItemsChanged();
        }
    }

    private void Receive(WorkerMessage message)
    {
        lock (stateLock)
        {
            environment = message.Environment ?? environment;
            if (!string.IsNullOrEmpty(message.Text))
            {
                output.Append(message.Text);
            }
        }

        RaiseItemsChanged();
    }

    private sealed class MessageProgress(Action<WorkerMessage> receive) : IProgress<WorkerMessage>
    {
        public void Report(WorkerMessage value) => receive(value);
    }
}
