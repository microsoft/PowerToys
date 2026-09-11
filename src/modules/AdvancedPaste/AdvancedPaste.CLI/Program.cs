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
using AdvancedPaste.Core;
using ManagedCommon;

namespace AdvancedPaste.Cli;

public static partial class Program
{
    internal const int SuccessExitCode = 0;
    internal const int RuntimeErrorExitCode = 1;
    internal const int ArgumentErrorExitCode = 2;
    private const int MaximumInputCharacters = 16 * 1024 * 1024;

    private static readonly string[] InputAliases = ["--input", "-i"];
    private static readonly string[] StdinAliases = ["--stdin"];
    private static readonly string[] ClipboardAliases = ["--clipboard"];
    private static readonly string[] OutputAliases = ["--output", "-o"];
    private static readonly string[] StdoutAliases = ["--stdout"];
    private static readonly string[] OutputClipboardAliases = ["--output-clipboard"];
    private static readonly string[] JsonAliases = ["--json"];

    [STAThread]
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
            Console.CancelKeyPress += cancelHandler;
            Logger.InitializeLogger("\\AdvancedPaste\\CLI\\Logs");
            return await RunAsync(args, Console.In, Console.Out, Console.Error, new SystemClipboardAdapter(), cancellationSource.Token);
        }
        catch (Exception ex)
        {
            Logger.LogError("Advanced Paste CLI failed.", ex);
            WriteError(Console.Error, false, "internal_error", "Advanced Paste CLI failed.");
            return RuntimeErrorExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static RootCommand CreateRootCommand(out CliOptions options)
    {
        var root = new RootCommand("Run deterministic Advanced Paste transformations without opening the Advanced Paste UI.");
        var transform = new Command("transform", "Transform text, HTML, XML, INI, or CSV content.");
        options = new CliOptions();
        transform.AddOption(options.Format);
        transform.AddOption(options.Input);
        transform.AddOption(options.Stdin);
        transform.AddOption(options.Clipboard);
        transform.AddOption(options.Output);
        transform.AddOption(options.Stdout);
        transform.AddOption(options.OutputClipboard);
        transform.AddOption(options.Json);
        root.AddCommand(transform);
        return root;
    }

    internal static async Task<int> RunAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr,
        IClipboardAdapter clipboard,
        CancellationToken cancellationToken)
    {
        var root = CreateRootCommand(out var options);
        var parseResult = new Parser(root).Parse(args);

        if (args.Length == 0 || HasHelpToken(parseResult))
        {
            return await root.InvokeAsync(args);
        }

        var json = parseResult.GetValueForOption(options.Json);
        if (parseResult.Errors.Count > 0 || parseResult.CommandResult.Command is RootCommand)
        {
            var message = parseResult.Errors.Count > 0
                ? string.Join("; ", parseResult.Errors.Select(error => error.Message))
                : "The transform command is required.";
            WriteError(stderr, json, "invalid_arguments", message);
            if (!json)
            {
                PrintUsage(stderr);
            }

            return ArgumentErrorExitCode;
        }

        try
        {
            var formatText = parseResult.GetValueForOption(options.Format);
            if (!TryParseFormat(formatText, out var format))
            {
                WriteError(stderr, json, "unsupported_format", "Supported formats are plain-text, markdown, and json.");
                return ArgumentErrorExitCode;
            }

            var inputFile = parseResult.GetValueForOption(options.Input);
            var stdinRequested = parseResult.GetValueForOption(options.Stdin);
            var clipboardRequested = parseResult.GetValueForOption(options.Clipboard);
            if (CountSelected(inputFile is not null, stdinRequested, clipboardRequested) != 1)
            {
                WriteError(stderr, json, "invalid_input_mode", "Specify exactly one input mode: --input, --stdin, or --clipboard.");
                if (!json)
                {
                    PrintUsage(stderr);
                }

                return ArgumentErrorExitCode;
            }

            var outputFile = parseResult.GetValueForOption(options.Output);
            var stdoutRequested = parseResult.GetValueForOption(options.Stdout);
            var outputClipboardRequested = parseResult.GetValueForOption(options.OutputClipboard);
            if (CountSelected(outputFile is not null, stdoutRequested, outputClipboardRequested) > 1)
            {
                WriteError(stderr, json, "invalid_output_mode", "Specify at most one output mode: --output, --stdout, or --output-clipboard.");
                if (!json)
                {
                    PrintUsage(stderr);
                }

                return ArgumentErrorExitCode;
            }

            var input = inputFile is not null
                ? await ReadFileAsync(inputFile, cancellationToken)
                : stdinRequested
                    ? await ReadBoundedAsync(stdin, cancellationToken)
                    : clipboard.ReadText();
            if (string.IsNullOrEmpty(input))
            {
                WriteError(stderr, json, "empty_input", "The selected input source did not contain text.");
                return RuntimeErrorExitCode;
            }

            var output = HeadlessTransformService.Transform(format, input, cancellationToken);
            if (outputFile is not null)
            {
                await File.WriteAllTextAsync(outputFile.FullName, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            }
            else if (outputClipboardRequested)
            {
                clipboard.WriteText(output);
            }

            if (json)
            {
                WriteSuccess(stdout, new SuccessResult("success", FormatName(format), outputFile?.FullName, outputClipboardRequested, output));
            }
            else if (outputFile is null && !outputClipboardRequested)
            {
                await stdout.WriteAsync(output);
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException)
        {
            WriteError(stderr, json, "cancelled", "The transformation was cancelled.");
            return RuntimeErrorExitCode;
        }
        catch (IOException)
        {
            WriteError(stderr, json, "io_error", "The selected input or output file could not be accessed.");
            return RuntimeErrorExitCode;
        }
        catch (UnauthorizedAccessException)
        {
            WriteError(stderr, json, "io_error", "The selected input or output file could not be accessed.");
            return RuntimeErrorExitCode;
        }
        catch (Exception ex)
        {
            Logger.LogError("Advanced Paste transformation failed.", ex);
            WriteError(stderr, json, "transformation_error", "The transformation could not be completed.");
            return RuntimeErrorExitCode;
        }
    }

    private static async Task<string> ReadFileAsync(FileInfo inputFile, CancellationToken cancellationToken)
    {
        if (!inputFile.Exists)
        {
            throw new IOException();
        }

        if (inputFile.Length > MaximumInputCharacters)
        {
            throw new IOException();
        }

        await using var stream = inputFile.OpenRead();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return await ReadBoundedAsync(reader, cancellationToken);
    }

    private static async Task<string> ReadBoundedAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[8192];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return builder.ToString();
            }

            if (builder.Length + read > MaximumInputCharacters)
            {
                throw new IOException();
            }

            builder.Append(buffer, 0, read);
        }
    }

    private static bool TryParseFormat(string? value, out HeadlessTransformFormat format)
    {
        var normalized = value?.ToLowerInvariant();
        format = normalized switch
        {
            "plain-text" => HeadlessTransformFormat.PlainText,
            "markdown" => HeadlessTransformFormat.Markdown,
            "json" => HeadlessTransformFormat.Json,
            _ => default,
        };

        return normalized is "plain-text" or "markdown" or "json";
    }

    private static int CountSelected(params bool[] modes)
        => modes.Count(mode => mode);

    private static bool HasHelpToken(ParseResult parseResult)
        => parseResult.Tokens.Any(token => token.Value is "--help" or "-h" or "-?" or "/?");

    private static string FormatName(HeadlessTransformFormat format)
        => format switch
        {
            HeadlessTransformFormat.PlainText => "plain-text",
            HeadlessTransformFormat.Markdown => "markdown",
            HeadlessTransformFormat.Json => "json",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    private static void PrintUsage(TextWriter stderr)
        => stderr.WriteLine("Usage: PowerToys.AdvancedPaste.CLI.exe transform --format <plain-text|markdown|json> (--input <path>|--stdin|--clipboard) [--output <path>|--stdout|--output-clipboard] [--json]");

    private static void WriteError(TextWriter stderr, bool json, string code, string message)
    {
        if (json)
        {
            stderr.WriteLine(JsonSerializer.Serialize(new ErrorResult("error", code, message), CliJsonContext.Default.ErrorResult));
        }
        else
        {
            stderr.WriteLine($"Error: {message}");
        }
    }

    private static void WriteSuccess(TextWriter writer, SuccessResult value)
        => writer.WriteLine(JsonSerializer.Serialize(value, CliJsonContext.Default.SuccessResult));

    private sealed record SuccessResult(string Status, string Format, string? OutputPath, bool OutputClipboard, string Output);

    private sealed record ErrorResult(string Status, string Code, string Message);

    private sealed class CliOptions
    {
        internal Option<string> Format { get; } = new("--format", "The transform format: plain-text, markdown, or json.") { IsRequired = true };

        internal Option<FileInfo?> Input { get; } = new(InputAliases, "Read input from a file.");

        internal Option<bool> Stdin { get; } = new(StdinAliases, "Read input from standard input.");

        internal Option<bool> Clipboard { get; } = new(ClipboardAliases, "Read text or HTML explicitly from the Windows clipboard.");

        internal Option<FileInfo?> Output { get; } = new(OutputAliases, "Write transformed content to a file.");

        internal Option<bool> Stdout { get; } = new(StdoutAliases, "Write transformed content to standard output. This is the default.");

        internal Option<bool> OutputClipboard { get; } = new(OutputClipboardAliases, "Write transformed content explicitly to the Windows clipboard.");

        internal Option<bool> Json { get; } = new(JsonAliases, "Emit a stable machine-readable result or error envelope.");
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SuccessResult))]
    [JsonSerializable(typeof(ErrorResult))]
    private sealed partial class CliJsonContext : JsonSerializerContext;
}
