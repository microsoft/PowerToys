// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Cli;

public sealed class CliApplication(string workerPath, TextWriter output, PolicyProfileStore? profileStore = null)
{
    private static readonly string[] HelpCommands =
    [
        "policies list",
        "policies show <id>",
        "run --policy <id> --revision <sha256> --file <absolute Windows file> [--input <path>] [--arg <literal>] [--output <existing directory>]",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PolicyProfileStore profiles = profileStore ?? new PolicyProfileStore();
    private readonly object outputLock = new();

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = CliArguments.Parse(args);
            switch (command.Command)
            {
                case CliCommand.Help:
                    Write(new
                    {
                        kind = "help",
                        schemaVersion = 1,
                        commands = HelpCommands,
                        notes = "JSON lines are written to stdout. Review policies show, then pin its revision. --input copies files or folders into a fresh workspace; arguments never grant host access. Repeat --input and --arg as needed. EXEs use their installed location. The $app token resolves to the EXE's containing folder; the default read-only rule grants read access to that folder. Scripts run from a copy, without automatically granting their original parent folder. Outputs are reviewed and copied to a new results folder. Ctrl+C stops the worker. Linux workloads currently use the Try Run application.",
                    });
                    return 0;
                case CliCommand.ListPolicies:
                    Write(new { kind = "policies", schemaVersion = 1, profiles = profiles.List().Select(profile => new { profile.Id, profile.Name, profile.Revision, profile.Linux }) });
                    return 0;
                case CliCommand.ShowPolicy:
                    Write(new { kind = "policy", schemaVersion = 1, profile = profiles.Load(command.PolicyId!) });
                    return 0;
                case CliCommand.Run:
                    return await RunAsync(command, cancellationToken).ConfigureAwait(false);
                default:
                    throw new ArgumentException("Unknown command.");
            }
        }
        catch (OperationCanceledException)
        {
            Write(new { kind = "cancelled", schemaVersion = 1, message = "Execution was cancelled; the worker has been stopped." });
            return 130;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException or System.ComponentModel.Win32Exception)
        {
            Write(new { kind = "error", schemaVersion = 1, message = exception.Message });
            return exception is ArgumentException ? 2 : 1;
        }
    }

    private async Task<int> RunAsync(CliArguments command, CancellationToken cancellationToken)
    {
        var profile = profiles.Load(command.PolicyId!, command.Revision!);
        if (profile.Linux)
        {
            throw new ArgumentException("The CLI currently runs Windows workloads only. Choose a Windows policy, or use the Try Run application for Linux workloads.");
        }

        if (command.Output is not null && !Directory.Exists(command.Output))
        {
            throw new DirectoryNotFoundException("The --output directory must already exist.");
        }

        if (command.Output is not null)
        {
            _ = PolicyPaths.ResolveExisting(command.Output);
        }

        var kind = CliArguments.WindowsKind(command.File!);
        var configuration = new RunConfiguration
        {
            Kind = kind,
            ApplicationPath = kind == WorkloadKind.WindowsApplication ? command.File : null,
            WorkloadFile = kind != WorkloadKind.WindowsApplication ? command.File : null,
            InputPaths = command.Inputs,
            Arguments = command.Arguments,
            Profile = new PolicyProfileReference(profile.Id, profile.Revision),
        };
        Write(new { kind = "preparing", schemaVersion = 1, profileId = profile.Id, revision = profile.Revision });
        using var run = await new RunCoordinator(workerPath, profiles).PrepareAsync(configuration, cancellationToken).ConfigureAwait(false);
        Write(new
        {
            kind = "prepared",
            schemaVersion = 1,
            profileId = profile.Id,
            revision = profile.Revision,
            profileName = profile.Name,
            request = run.Request,
            note = "The submitted policy is shown here; successful enforcement depends on the backend accepting the request. Unsupported policies fail instead of falling back to host execution.",
        });
        RunEnvironment? environment = null;
        var result = await run.RunAsync(
            new MessageProgress(message =>
            {
                environment = message.Environment ?? environment;
                Write(new { kind = "progress", schemaVersion = 1, message });
            }),
            cancellationToken).ConfigureAwait(false);
        var review = await run.ReviewAsync(cancellationToken).ConfigureAwait(false);
        var exported = RunResultExport.Export(run.Workspace, review.Changes, command.Output, cancellationToken);
        Write(new
        {
            kind = "completed",
            schemaVersion = 1,
            profileId = profile.Id,
            revision = profile.Revision,
            submittedPolicy = run.Request.Policy,
            environment,
            result,
            review.Originals,
            changes = review.Changes.Select(change => new { change.RelativePath, change.Kind, before = change.Before?.Hash, after = change.After?.Hash }),
            resultsDirectory = exported,
        });
        return result.TimedOut ? 124 : result.ExitCode == 0 ? 0 : 1;
    }

    private void Write<T>(T message)
    {
        lock (outputLock)
        {
            output.WriteLine(JsonSerializer.Serialize(message, JsonOptions));
            output.Flush();
        }
    }

    private sealed class MessageProgress(Action<WorkerMessage> report) : IProgress<WorkerMessage>
    {
        public void Report(WorkerMessage value) => report(value);
    }
}
