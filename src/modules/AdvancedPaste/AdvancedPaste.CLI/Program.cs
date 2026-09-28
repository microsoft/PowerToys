// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedPaste.Cli;

public static partial class Program
{
    internal const int SuccessExitCode = 0;
    internal const int RuntimeErrorExitCode = 1;
    internal const int ArgumentErrorExitCode = 2;
    internal const int MaximumInputCharacters = 16 * 1024 * 1024;
    private const string SupportedActions = "plain-text, markdown, json, fix-spelling-and-grammar, image-to-text, paste-as-txt-file, paste-as-png-file, paste-as-html-file, transcode-to-mp3, transcode-to-mp4, or paste-with-ai";
    private const string Usage = "Usage: PowerToys.AdvancedPaste.CLI.exe transform (--action <name>|--custom-action <id-or-name>) (--input <path>|--stdin|--clipboard) [--output <path>|--stdout|--output-clipboard] [--prompt <text>] [--provider <id>] [--json]";

    private static readonly string[] ActionAliases = ["--action", "--format"];
    private static readonly string[] CustomActionAliases = ["--custom-action"];
    private static readonly string[] PromptAliases = ["--prompt"];
    private static readonly string[] ProviderAliases = ["--provider"];
    private static readonly string[] InputAliases = ["--input", "-i"];
    private static readonly string[] StdinAliases = ["--stdin"];
    private static readonly string[] ClipboardAliases = ["--clipboard"];
    private static readonly string[] OutputAliases = ["--output", "-o"];
    private static readonly string[] StdoutAliases = ["--stdout"];
    private static readonly string[] OutputClipboardAliases = ["--output-clipboard"];
    private static readonly string[] JsonAliases = ["--json"];

    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        try
        {
            TrySetUtf8Output();
            Console.CancelKeyPress += cancelHandler;
            Logger.InitializeLogger("\\AdvancedPaste\\CLI\\Logs");
            AdvancedPasteTempFileManager.CleanupStaleDirectories(TimeSpan.FromDays(1));

            using var host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((_, services) => services.AddAdvancedPasteEngine())
                .Build();
            var runtime = new AdvancedPasteRuntime(
                host.Services.GetRequiredService<IPasteFormatExecutor>(),
                host.Services.GetRequiredService<IUserSettings>());

            return await RunAsync(args, Console.In, Console.Out, Console.Error, new SystemClipboardAdapter(), runtime, cancellationSource.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError("Advanced Paste CLI failed.", ex);
            WriteStartupError(args, Console.Error);
            return RuntimeErrorExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static RootCommand CreateRootCommand(out CliOptions options)
    {
        var root = new RootCommand("Run Advanced Paste actions without opening the Advanced Paste UI.");
        var transform = new Command("transform", "Transform text, HTML, images, or media using a built-in or configured Advanced Paste action.");
        options = new CliOptions();
        transform.AddOption(options.Action);
        transform.AddOption(options.CustomAction);
        transform.AddOption(options.Prompt);
        transform.AddOption(options.Provider);
        transform.AddOption(options.Input);
        transform.AddOption(options.Stdin);
        transform.AddOption(options.Clipboard);
        transform.AddOption(options.Output);
        transform.AddOption(options.Stdout);
        transform.AddOption(options.OutputClipboard);
        transform.AddOption(options.Json);
        root.AddCommand(transform);

        var actions = new Command("actions", "Inspect actions available to the command-line interface.");
        var list = new Command("list", "List built-in and configured custom actions.");
        list.AddOption(options.ListJson);
        actions.AddCommand(list);
        root.AddCommand(actions);
        return root;
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
        var parseResult = new Parser(root).Parse(args);

        if (args.Length == 0 || HasHelpToken(parseResult))
        {
            return await root.InvokeAsync(args);
        }

        var json = args.Contains("--json", StringComparer.Ordinal);
        if (parseResult.Errors.Count > 0 || parseResult.CommandResult.Command is RootCommand)
        {
            var message = parseResult.Errors.Count > 0
                ? string.Join("; ", parseResult.Errors.Select(error => error.Message))
                : "The transform command is required.";
            WriteError(stderr, json, "invalid_arguments", message, includeUsage: true);
            return ArgumentErrorExitCode;
        }

        if (parseResult.CommandResult.Command.Name == "list")
        {
            var actions = runtime.GetActions();
            if (parseResult.GetValueForOption(options.ListJson))
            {
                stdout.WriteLine(JsonSerializer.Serialize(actions.ToArray(), CliJsonContext.Default.CliActionDescriptorArray));
            }
            else
            {
                foreach (var action in actions)
                {
                    var id = action.Id is null ? string.Empty : $" ({action.Id})";
                    stdout.WriteLine($"{action.Kind}: {action.Name}{id}{(action.RequiresPrompt ? " [requires --prompt]" : string.Empty)}");
                }
            }

            return SuccessExitCode;
        }

        try
        {
            var action = parseResult.GetValueForOption(options.Action);
            var customAction = parseResult.GetValueForOption(options.CustomAction);
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

            var prompt = parseResult.GetValueForOption(options.Prompt);
            if (string.Equals(action, "paste-with-ai", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(prompt))
            {
                return WriteArgumentError(stderr, json, "missing_prompt", "--prompt is required for paste-with-ai.");
            }

            var inputFile = parseResult.GetValueForOption(options.Input);
            var stdinRequested = parseResult.GetValueForOption(options.Stdin);
            var clipboardRequested = parseResult.GetValueForOption(options.Clipboard);
            if (CountSelected(inputFile is not null, stdinRequested, clipboardRequested) != 1)
            {
                return WriteArgumentError(stderr, json, "invalid_input_mode", "Specify exactly one input mode: --input, --stdin, or --clipboard.");
            }

            var outputFile = parseResult.GetValueForOption(options.Output);
            var stdoutRequested = parseResult.GetValueForOption(options.Stdout);
            var outputClipboardRequested = parseResult.GetValueForOption(options.OutputClipboard);
            if (CountSelected(outputFile is not null, stdoutRequested, outputClipboardRequested) > 1)
            {
                return WriteArgumentError(stderr, json, "invalid_output_mode", "Specify at most one output mode: --output, --stdout, or --output-clipboard. Clipboard is the default.");
            }

            var input = await CliInputReader.ReadAsync(inputFile, stdinRequested, clipboard, stdin, MaximumInputCharacters, cancellationToken);
            if (!await input.HasUsableDataAsync())
            {
                WriteError(stderr, json, "empty_input", "The selected input source did not contain supported content.");
                return RuntimeErrorExitCode;
            }

            var request = new CliActionRequest(action, customAction, prompt, parseResult.GetValueForOption(options.Provider));
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
        catch (InvalidOperationException ex)
        {
            Logger.LogError("Advanced Paste action is unavailable.", ex);
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

    private static int WriteArgumentError(TextWriter stderr, bool json, string code, string message)
    {
        WriteError(stderr, json, code, message, includeUsage: true);
        return ArgumentErrorExitCode;
    }

    internal static void WriteStartupError(string[] args, TextWriter stderr)
        => WriteError(stderr, args.Contains("--json", StringComparer.Ordinal), "internal_error", "Advanced Paste CLI failed.");

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

    private static bool HasHelpToken(ParseResult parseResult)
        => parseResult.Tokens.Any(token => token.Value is "--help" or "-h" or "-?" or "/?");

    private static void WriteError(TextWriter stderr, bool json, string code, string message, bool includeUsage = false)
    {
        if (json)
        {
            stderr.WriteLine(JsonSerializer.Serialize(new ErrorResult("error", code, message, includeUsage ? Usage : null), CliJsonContext.Default.ErrorResult));
        }
        else
        {
            stderr.WriteLine($"Error: {message}");
            if (includeUsage)
            {
                stderr.WriteLine(Usage);
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
        internal Option<string?> Action { get; } = new(ActionAliases, $"Run a built-in action. Supported values: {SupportedActions}. --format is a compatibility alias.");

        internal Option<string?> CustomAction { get; } = new(CustomActionAliases, "Run a saved custom action by numeric ID or exact name.");

        internal Option<string?> Prompt { get; } = new(PromptAliases, "Instructions for the paste-with-ai action.");

        internal Option<string?> Provider { get; } = new(ProviderAliases, "Use a configured AI provider ID instead of the action or active provider.");

        internal Option<FileInfo?> Input { get; } = new(InputAliases, "Read input from a text, image, audio, or video file.");

        internal Option<bool> Stdin { get; } = new(StdinAliases, "Read text input from standard input.");

        internal Option<bool> Clipboard { get; } = new(ClipboardAliases, "Read rich content explicitly from the Windows clipboard.");

        internal Option<FileInfo?> Output { get; } = new(OutputAliases, "Write transformed text or a generated file to a path.");

        internal Option<bool> Stdout { get; } = new(StdoutAliases, "Write text output to standard output.");

        internal Option<bool> OutputClipboard { get; } = new(OutputClipboardAliases, "Write transformed content to the Windows clipboard. This is the default.");

        internal Option<bool> Json { get; } = new(JsonAliases, "Emit a stable machine-readable result or error envelope.");

        internal Option<bool> ListJson { get; } = new(JsonAliases, "Emit the action list as JSON.");
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SuccessResult))]
    [JsonSerializable(typeof(ErrorResult))]
    [JsonSerializable(typeof(CliActionDescriptor[]))]
    private sealed partial class CliJsonContext : JsonSerializerContext;
}
