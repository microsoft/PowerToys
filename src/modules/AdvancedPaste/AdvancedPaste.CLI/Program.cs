// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using AdvancedPaste.Telemetry;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerToys.Telemetry;

namespace AdvancedPaste.Cli;

public static partial class Program
{
    internal const int SuccessExitCode = 0;
    internal const int RuntimeErrorExitCode = 1;
    internal const int ArgumentErrorExitCode = 2;
    internal const int MaximumInputCharacters = 16 * 1024 * 1024;
    private const string SupportedActions = "plain-text, markdown, json, fix-spelling-and-grammar, image-to-text, paste-as-txt-file, paste-as-png-file, paste-as-html-file, transcode-to-mp3, transcode-to-mp4, or paste-with-ai";
    private const string TransformUsage = "Usage: PowerToys.AdvancedPaste.CLI.exe transform (--action <name>|--custom-action <id-or-name>) [--input <path>|--stdin|--clipboard] [--output <path>|--stdout|--output-clipboard] [--prompt <text>] [--provider <id>] [--json]";
    private const string ActionsListUsage = "Usage: PowerToys.AdvancedPaste.CLI.exe actions list [--json]";
    private const string ProvidersListUsage = "Usage: PowerToys.AdvancedPaste.CLI.exe providers list [--json]";
    private const string WorkflowHelp = """
        Saved AI actions: run 'actions list' to find custom IDs/names, then use --custom-action <id-or-name>.
        AI providers: configure them in PowerToys Settings > Advanced Paste and run 'providers list' to find IDs for --provider.
        Input and output default to the clipboard. Use --stdin for piped text and --stdout to print text without changing the clipboard.
        Choose at most one input mode (--input, --stdin, --clipboard) and one output mode (--output, --stdout, --output-clipboard).
        Generated files cannot be written to stdout. Clipboard operations require an interactive user session.
        Text input limit: 16,777,216 characters. --json provides machine-readable results; Ctrl+C cancels.
        Exit codes: 0 success, 1 runtime/input/provider failure, 2 invalid arguments.

        Examples:
          PowerToys.AdvancedPaste.CLI.exe transform --action json --input people.csv --stdout
          PowerToys.AdvancedPaste.CLI.exe transform --action markdown --input article.html --output article.md
          PowerToys.AdvancedPaste.CLI.exe transform --action image-to-text --input image.png --stdout
          PowerToys.AdvancedPaste.CLI.exe transform --action paste-with-ai --prompt "Summarize in one sentence" --clipboard --stdout
          PowerToys.AdvancedPaste.CLI.exe transform --custom-action 3 --input notes.txt --stdout
          PowerToys.AdvancedPaste.CLI.exe providers list --json
        """;

    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        var exitCode = RuntimeErrorExitCode;
        var telemetryCommandName = "unknown";
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        var loggerInitialized = false;
        try
        {
            loggerInitialized = TryInitializeLogger(() => Logger.InitializeLogger("\\AdvancedPaste\\CLI\\Logs"));
            TrySetUtf8Output();
            TrySetUtf8RedirectedInput();
            telemetryCommandName = GetTelemetryCommandName(args);
            if (IsHelpRequest(args))
            {
                var root = CreateRootCommand(out _);
                exitCode = await WriteHelpAsync(root, args, Console.Out, Console.Error);
                return exitCode;
            }

            Console.CancelKeyPress += cancelHandler;
            if (TryWritePolicyDisabledError(args, Console.Error, AdvancedPastePolicy.IsAdvancedPasteEnabled))
            {
                return RuntimeErrorExitCode;
            }

            using var host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((_, services) => services.AddAdvancedPasteEngine(watchSettings: false))
                .Build();
            var runtime = new AdvancedPasteRuntime(
                host.Services.GetRequiredService<IPasteFormatExecutor>(),
                host.Services.GetRequiredService<IUserSettings>());

            exitCode = await RunAsync(args, Console.In, Console.Out, Console.Error, new SystemClipboardAdapter(), runtime, cancellationSource.Token);
            return exitCode;
        }
        catch (Exception ex)
        {
            if (loggerInitialized)
            {
                Logger.LogError("Advanced Paste CLI failed.", ex);
            }

            WriteStartupError(args, Console.Error);
            exitCode = RuntimeErrorExitCode;
            return exitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            LogCLITelemetry(telemetryCommandName, exitCode == SuccessExitCode, loggerInitialized);
        }
    }

    internal static string GetTelemetryCommandName(string[] args)
    {
        if (IsHelpRequest(args))
        {
            return "help";
        }

        if (args.Length > 1 &&
            string.Equals(args[0], "actions", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            return "actions list";
        }

        if (args.Length > 1 &&
            string.Equals(args[0], "providers", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            return "providers list";
        }

        return args.Length > 0 && string.Equals(args[0], "transform", StringComparison.OrdinalIgnoreCase)
            ? "transform"
            : "unknown";
    }

    internal static void LogCLITelemetry(string commandName, bool successful, bool loggerInitialized)
    {
        try
        {
            PowerToysTelemetry.Log.WriteEvent(CreateCLITelemetryEvent(commandName, successful));
        }
        catch (Exception ex)
        {
            if (loggerInitialized)
            {
                Logger.LogError("Failed to log Advanced Paste CLI telemetry.", ex);
            }
        }
    }

    internal static AdvancedPasteCLICommandEvent CreateCLITelemetryEvent(string commandName, bool successful)
        => new()
        {
            CommandName = commandName,
            Successful = successful,
        };

    private static RootCommand CreateRootCommand(out CliOptions options)
    {
        var actionHelp = GetActionHelp();
        var root = new RootCommand("Run Advanced Paste actions without opening the Advanced Paste UI.");
        var transform = new Command("transform", $"Transform text, HTML, images, or media using a built-in or configured action.{Environment.NewLine}{Environment.NewLine}{actionHelp}{Environment.NewLine}{Environment.NewLine}{WorkflowHelp}");
        options = new CliOptions();
        transform.Options.Add(options.Action);
        transform.Options.Add(options.CustomAction);
        transform.Options.Add(options.Prompt);
        transform.Options.Add(options.Provider);
        transform.Options.Add(options.Input);
        transform.Options.Add(options.Stdin);
        transform.Options.Add(options.Clipboard);
        transform.Options.Add(options.Output);
        transform.Options.Add(options.Stdout);
        transform.Options.Add(options.OutputClipboard);
        transform.Options.Add(options.Json);
        root.Subcommands.Add(transform);

        var actions = new Command("actions", "Inspect actions available to the command-line interface.");
        var list = new Command("list", $"List built-in and configured custom actions, descriptions, and custom IDs.{Environment.NewLine}{Environment.NewLine}{actionHelp}");
        list.Options.Add(options.ListJson);
        actions.Subcommands.Add(list);
        root.Subcommands.Add(actions);

        var providers = new Command("providers", "Inspect AI providers configured in PowerToys Settings > Advanced Paste.");
        options.ProvidersList.Options.Add(options.ProvidersJson);
        providers.Subcommands.Add(options.ProvidersList);
        root.Subcommands.Add(providers);
        return root;
    }

    private static string GetActionHelp()
        => "Built-in actions:" + Environment.NewLine +
            string.Join(Environment.NewLine, AdvancedPasteRuntime.GetBuiltInActions().Select(action => $"  {action.Name}: {action.Description}"));

    private static async Task<int> WriteHelpAsync(RootCommand root, string[] args, TextWriter stdout, TextWriter stderr)
    {
        var parseResult = root.Parse(args);
        var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });
        if (exitCode == SuccessExitCode && parseResult.CommandResult.Command is RootCommand)
        {
            stdout.WriteLine(GetActionHelp());
            stdout.WriteLine();
            stdout.WriteLine(WorkflowHelp);
        }

        return exitCode;
    }

    internal static async Task<int> RunAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr,
        IClipboardAdapter clipboard,
        IAdvancedPasteRuntime runtime,
        CancellationToken cancellationToken)
    {
        var root = CreateRootCommand(out var options);
        var parseResult = root.Parse(args);

        if (args.Length == 0 || HasHelpToken(parseResult, options))
        {
            return await WriteHelpAsync(root, args, stdout, stderr);
        }

        var json = parseResult.GetValue(options.Json) ||
            parseResult.GetValue(options.ListJson) ||
            parseResult.GetValue(options.ProvidersJson);
        if (args.SequenceEqual(["actions"], StringComparer.Ordinal))
        {
            return WriteArgumentError(stderr, json, "incomplete_command", "Specify 'actions list' to inspect available actions.", ActionsListUsage);
        }

        if (args.SequenceEqual(["providers"], StringComparer.Ordinal))
        {
            return WriteArgumentError(stderr, json, "incomplete_command", "Specify 'providers list' to inspect configured AI provider IDs.", ProvidersListUsage);
        }

        if (parseResult.Errors.Count > 0 || parseResult.CommandResult.Command is RootCommand)
        {
            var message = parseResult.Errors.Count > 0
                ? string.Join("; ", parseResult.Errors.Select(error => error.Message))
                : "Specify transform, actions list, or providers list.";
            var usage = args.Length > 0 && string.Equals(args[0], "actions", StringComparison.OrdinalIgnoreCase)
                ? ActionsListUsage
                : args.Length > 0 && string.Equals(args[0], "providers", StringComparison.OrdinalIgnoreCase)
                    ? ProvidersListUsage
                    : TransformUsage;
            WriteError(stderr, json, "invalid_arguments", message, usage);
            return ArgumentErrorExitCode;
        }

        if (TryWritePolicyDisabledError(args, stderr, runtime.IsEnabled))
        {
            return RuntimeErrorExitCode;
        }

        if (parseResult.CommandResult.Command == options.ProvidersList)
        {
            var providers = runtime.GetProviders();
            if (parseResult.GetValue(options.ProvidersJson))
            {
                stdout.WriteLine(JsonSerializer.Serialize(providers.ToArray(), CliJsonContext.Default.CliProviderDescriptorArray));
            }
            else if (providers.Count == 0)
            {
                stdout.WriteLine("No AI providers configured. Add a provider in PowerToys Settings > Advanced Paste.");
            }
            else
            {
                foreach (var provider in providers)
                {
                    stdout.WriteLine($"{provider.Id}: {provider.Name} ({provider.ServiceType}){(provider.IsDefault ? " [default]" : string.Empty)}{(provider.IsAllowedByPolicy ? string.Empty : " [disabled by policy]")}");
                }
            }

            return SuccessExitCode;
        }

        if (parseResult.CommandResult.Command.Name == "list")
        {
            var actions = runtime.GetActions();
            if (parseResult.GetValue(options.ListJson))
            {
                stdout.WriteLine(JsonSerializer.Serialize(actions.ToArray(), CliJsonContext.Default.CliActionDescriptorArray));
            }
            else
            {
                foreach (var action in actions)
                {
                    var id = action.Id is null ? string.Empty : $" ({action.Id})";
                    stdout.WriteLine($"{action.Kind}: {action.Name}{id}{(action.RequiresPrompt ? " [requires --prompt]" : string.Empty)} - {action.Description}");
                }
            }

            return SuccessExitCode;
        }

        try
        {
            var action = parseResult.GetValue(options.Action);
            var customAction = parseResult.GetValue(options.CustomAction);
            if (CountSelected(!string.IsNullOrWhiteSpace(action), !string.IsNullOrWhiteSpace(customAction)) != 1)
            {
                return WriteArgumentError(stderr, json, "invalid_action", "Specify exactly one action selector: --action or --custom-action.");
            }

            if (!string.IsNullOrWhiteSpace(action) &&
                !AdvancedPasteRuntime.BuiltInActions.ContainsKey(action) &&
                !string.Equals(action, "paste-with-ai", StringComparison.OrdinalIgnoreCase))
            {
                return WriteArgumentError(stderr, json, "unsupported_action", $"Supported actions are {SupportedActions}.");
            }

            var prompt = parseResult.GetValue(options.Prompt);
            if (string.Equals(action, "paste-with-ai", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(prompt))
            {
                return WriteArgumentError(stderr, json, "missing_prompt", "--prompt is required for paste-with-ai.");
            }

            var provider = parseResult.GetValue(options.Provider);
            if (provider is not null &&
                (string.IsNullOrWhiteSpace(provider) ||
                 (string.IsNullOrWhiteSpace(customAction) &&
                  !string.Equals(action, "paste-with-ai", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(action, "fix-spelling-and-grammar", StringComparison.OrdinalIgnoreCase))))
            {
                return WriteArgumentError(stderr, json, "invalid_provider", "--provider requires a configured AI provider ID and is only supported with paste-with-ai, fix-spelling-and-grammar, or --custom-action. Run 'providers list' to find IDs.");
            }

            var inputFile = parseResult.GetValue(options.Input);
            var stdinRequested = parseResult.GetValue(options.Stdin);
            var clipboardRequested = parseResult.GetValue(options.Clipboard);
            if (CountSelected(inputFile is not null, stdinRequested, clipboardRequested) > 1)
            {
                return WriteArgumentError(stderr, json, "invalid_input_mode", "Specify at most one input mode: --input, --stdin, or --clipboard. Clipboard is the default.");
            }

            clipboardRequested = inputFile is null && !stdinRequested;
            var outputFile = parseResult.GetValue(options.Output);
            var stdoutRequested = parseResult.GetValue(options.Stdout);
            var outputClipboardRequested = parseResult.GetValue(options.OutputClipboard);
            if (CountSelected(outputFile is not null, stdoutRequested, outputClipboardRequested) > 1)
            {
                return WriteArgumentError(stderr, json, "invalid_output_mode", "Specify at most one output mode: --output, --stdout, or --output-clipboard. Clipboard is the default.");
            }

            var input = await CliInputReader.ReadAsync(inputFile, stdinRequested, clipboard, stdin, MaximumInputCharacters, cancellationToken);
            var clipboardSnapshot = input;
            if (clipboardRequested)
            {
                input = await CliInputReader.ReadClipboardFileContentAsync(input, MaximumInputCharacters, cancellationToken);
            }

            if (!await input.HasUsableDataAsync())
            {
                WriteError(stderr, json, "empty_input", "The selected input source did not contain supported content.");
                return RuntimeErrorExitCode;
            }

            // Text-file input no longer carries StorageItems; protect paths from the original snapshot.
            if (clipboardRequested)
            {
                var protectedPaths = new List<string>();
                if (clipboardSnapshot.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                {
                    protectedPaths.AddRange((await clipboardSnapshot.GetStorageItemsAsync()).Select(item => item.Path));
                }

                if (outputFile is not null)
                {
                    protectedPaths.Add(outputFile.FullName);
                }

                AdvancedPasteTempFileManager.CleanupStaleDirectories(TimeSpan.FromDays(1), protectedPaths);
            }

            var request = new CliActionRequest(action, customAction, prompt, provider);
            var result = await runtime.ExecuteAsync(request, input, cancellationToken);
            var output = await CliOutputWriter.WriteAsync(
                result,
                outputFile,
                stdoutRequested,
                clipboard,
                json ? TextWriter.Null : stdout,
                cancellationToken);

            if (json)
            {
                WriteSuccess(stdout, new SuccessResult(
                    "success",
                    action ?? "custom-action",
                    output.ResultKind,
                    output.OutputPath,
                    output.OutputClipboard,
                    output.Text));
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException)
        {
            WriteError(stderr, json, "cancelled", "The transformation was cancelled.");
            return RuntimeErrorExitCode;
        }
        catch (InputTooLargeException)
        {
            WriteError(stderr, json, "input_too_large", "Text input exceeds the 16,777,216 character limit.");
            return RuntimeErrorExitCode;
        }
        catch (UnsupportedOutputException ex)
        {
            WriteError(stderr, json, "unsupported_output", ex.Message);
            return RuntimeErrorExitCode;
        }
        catch (CliActionResolutionException ex)
        {
            Logger.LogError("Advanced Paste CLI argument resolution failed.", ex);
            return WriteArgumentError(stderr, json, "invalid_action", ex.Message);
        }
        catch (IOException ex)
        {
            Logger.LogError("Advanced Paste CLI file I/O failed.", ex);
            WriteError(stderr, json, "io_error", "The selected input or output file could not be accessed.");
            return RuntimeErrorExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogError("Advanced Paste CLI file access was denied.", ex);
            WriteError(stderr, json, "io_error", "The selected input or output file could not be accessed.");
            return RuntimeErrorExitCode;
        }
        catch (CliActionUnavailableException ex)
        {
            Logger.LogError("Advanced Paste action is unavailable.", ex);
            WriteError(stderr, json, "action_unavailable", ex.Message);
            return RuntimeErrorExitCode;
        }
        catch (CliPolicyDisabledException ex)
        {
            Logger.LogWarning(ex.Message);
            WriteError(stderr, json, "disabled_by_policy", $"{ex.Message}.");
            return RuntimeErrorExitCode;
        }
        catch (ProviderPolicyDisabledException ex)
        {
            Logger.LogWarning(ex.Message);
            WriteError(stderr, json, "action_unavailable", ex.Message);
            return RuntimeErrorExitCode;
        }
        catch (Exception ex)
        {
            Logger.LogError("Advanced Paste transformation failed.", ex);
            WriteError(stderr, json, "transformation_error", "The transformation could not be completed.");
            return RuntimeErrorExitCode;
        }
    }

    private static int WriteArgumentError(TextWriter stderr, bool json, string code, string message, string usage = TransformUsage)
    {
        WriteError(stderr, json, code, message, usage);
        return ArgumentErrorExitCode;
    }

    internal static void WriteStartupError(string[] args, TextWriter stderr)
        => WriteError(stderr, IsJsonRequested(args), "internal_error", "Advanced Paste CLI failed.");

    internal static bool TryWritePolicyDisabledError(string[] args, TextWriter stderr, bool policyEnabled)
    {
        if (policyEnabled)
        {
            return false;
        }

        WriteError(stderr, IsJsonRequested(args), "disabled_by_policy", "Advanced Paste is disabled by policy.");
        return true;
    }

    internal static bool TryInitializeLogger(Action initializeLogger)
    {
        try
        {
            initializeLogger();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
    }

    private static bool IsJsonRequested(string[] args)
    {
        var root = CreateRootCommand(out var options);
        var parseResult = root.Parse(args);
        return parseResult.GetValue(options.Json) ||
            parseResult.GetValue(options.ListJson) ||
            parseResult.GetValue(options.ProvidersJson);
    }

    private static int CountSelected(params bool[] modes)
        => modes.Count(mode => mode);

    private static void TrySetUtf8Output()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
        }
        catch (System.Security.SecurityException)
        {
        }
    }

    private static void TrySetUtf8RedirectedInput()
    {
        if (!Console.IsInputRedirected)
        {
            return;
        }

        try
        {
            Console.SetIn(CreateUtf8InputReader(Console.OpenStandardInput()));
        }
        catch (IOException)
        {
        }
        catch (SecurityException)
        {
        }
    }

    internal static TextReader CreateUtf8InputReader(Stream stream)
        => new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);

    internal static bool IsHelpRequest(string[] args)
    {
        var root = CreateRootCommand(out var options);
        return args.Length == 0 || HasHelpToken(root.Parse(args), options);
    }

    private static bool HasHelpToken(ParseResult parseResult, CliOptions options)
        => parseResult.Tokens.Any(token =>
            !IsOptionValueToken(parseResult, options, token)
            && IsHelpArgument(token.Value));

    private static bool IsOptionValueToken(ParseResult parseResult, CliOptions options, Token token)
        => options.ValueOptions.Any(option =>
            parseResult.GetResult(option)?.Tokens.Contains(token) == true);

    private static bool IsHelpArgument(string value)
        => value is "--help" or "-h" or "-?" or "/?";

    private static void WriteError(TextWriter stderr, bool json, string code, string message, string? usage = null)
    {
        Logger.LogWarning($"Advanced Paste CLI error: {code}.");
        if (json)
        {
            stderr.WriteLine(JsonSerializer.Serialize(new ErrorResult("error", code, message, usage), CliJsonContext.Default.ErrorResult));
        }
        else
        {
            stderr.WriteLine($"Error: {message}");
            if (usage is not null)
            {
                stderr.WriteLine(usage);
            }
        }
    }

    private static void WriteSuccess(TextWriter writer, SuccessResult value)
        => writer.WriteLine(JsonSerializer.Serialize(value, CliJsonContext.Default.SuccessResult));

    private sealed record SuccessResult(
        string Status,
        string Action,
        string ResultKind,
        string? OutputPath,
        bool OutputClipboard,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Output);

    private sealed record ErrorResult(string Status, string Code, string Message, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Usage);

    private sealed class CliOptions
    {
        internal Option<string?> Action { get; } = new("--action", "--format") { Description = $"Run a built-in action. Supported values: {SupportedActions}. --format is a compatibility alias." };

        internal Option<string?> CustomAction { get; } = new("--custom-action") { Description = "Run a saved custom action by numeric ID or case-insensitive name." };

        internal Option<string?> Prompt { get; } = new("--prompt") { Description = "Instructions for the paste-with-ai action." };

        internal Option<string?> Provider { get; } = new("--provider") { Description = "AI actions only: select the service/model configured in PowerToys Settings > Advanced Paste by its ID (not its service name). Run 'providers list' to find IDs. Otherwise use the saved action's provider or the default provider. This does not configure providers or API keys." };

        internal Option<FileInfo?> Input { get; } = new("--input", "-i") { Description = "Read input from a text, image, audio, or video file." };

        internal Option<bool> Stdin { get; } = new("--stdin") { Description = "Read text input from standard input." };

        internal Option<bool> Clipboard { get; } = new("--clipboard") { Description = "Read rich content or a single copied file from the Windows clipboard. This is the default." };

        internal Option<FileInfo?> Output { get; } = new("--output", "-o") { Description = "Write transformed text or a generated file to a path." };

        internal Option<bool> Stdout { get; } = new("--stdout") { Description = "Write text output to standard output." };

        internal Option<bool> OutputClipboard { get; } = new("--output-clipboard") { Description = "Write transformed content to the Windows clipboard. This is the default." };

        internal Option<bool> Json { get; } = new("--json") { Description = "Emit a stable machine-readable result or error envelope." };

        internal Option<bool> ListJson { get; } = new("--json") { Description = "Emit the action list as JSON." };

        internal Option<bool> ProvidersJson { get; } = new("--json") { Description = "Emit configured AI provider IDs, models, default selection, and policy status as JSON. Does not expose API keys or test connectivity." };

        internal Command ProvidersList { get; } = new("list", "List configured AI provider IDs for --provider without making AI requests.");

        internal IEnumerable<Option> ValueOptions =>
        [
            Action,
            CustomAction,
            Prompt,
            Provider,
            Input,
            Output,
        ];
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SuccessResult))]
    [JsonSerializable(typeof(ErrorResult))]
    [JsonSerializable(typeof(CliActionDescriptor[]))]
    [JsonSerializable(typeof(CliProviderDescriptor[]))]
    private sealed partial class CliJsonContext : JsonSerializerContext;
}
