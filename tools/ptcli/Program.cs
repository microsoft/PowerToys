// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PowerToys.Cli
{
    internal sealed class Program
    {
        private const string PipeName = "PowerToys.Runner.CLI";
        private static readonly JsonSerializerOptions JsonOutputOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
        };

        private static readonly JsonSerializerOptions JsonRequestOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = null,
        };

        [SupportedOSPlatform("windows")]
        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return 1;
            }

            bool listModules = false;
            bool listCommands = false;
            string? listCommandsModule = null;
            string? module = null;
            string? action = null;
            bool rawJson = false;
            int timeoutMs = 20000;

            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < args.Length; i++)
            {
                var argument = args[i];
                switch (argument)
                {
                    case "--help":
                    case "-h":
                        PrintHelp();
                        return 0;
                    case "--list-modules":
                        listModules = true;
                        break;
                    case "--list-commands":
                        listCommands = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            listCommandsModule = args[++i];
                        }

                        break;
                    case "--module":
                    case "-m":
                        module = RequireValue(args, ref i, argument);
                        break;
                    case "--action":
                    case "-a":
                        action = RequireValue(args, ref i, argument);
                        break;
                    case "--json":
                        rawJson = true;
                        break;
                    case "--timeout":
                        var timeoutValue = RequireValue(args, ref i, argument);
                        if (!int.TryParse(timeoutValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out timeoutMs) || timeoutMs <= 0)
                        {
                            Console.Error.WriteLine("--timeout expects a positive integer value (milliseconds).");
                            return 1;
                        }

                        break;
                    default:
                        if (argument.StartsWith("--", StringComparison.Ordinal))
                        {
                            var key = argument.Substring(2);
                            var value = RequireValue(args, ref i, argument);
                            payload[key] = ParseValue(value);
                        }
                        else
                        {
                            Console.Error.WriteLine($"Unrecognized argument '{argument}'.");
                            PrintHelp();
                            return 1;
                        }

                        break;
                }
            }

            try
            {
                if (listModules)
                {
                    return await ExecuteCommandAsync(string.Empty, "list-modules", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), rawJson, timeoutMs);
                }

                if (listCommands)
                {
                    if (string.IsNullOrWhiteSpace(listCommandsModule))
                    {
                        Console.Error.WriteLine("--list-commands requires a module name (e.g. ptcli --list-commands awake).");
                        return 1;
                    }

                    var argsPayload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["module"] = listCommandsModule!,
                    };

                    return await ExecuteCommandAsync(string.Empty, "list-commands", argsPayload, rawJson, timeoutMs);
                }

                if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(action))
                {
                    Console.Error.WriteLine("Both --module and --action must be specified.");
                    PrintHelp();
                    return 1;
                }

                return await ExecuteCommandAsync(module!, action!, payload, rawJson, timeoutMs);
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine("Timed out while communicating with the PowerToys runner.");
                return 1;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Pipe communication failed: {ex.Message}");
                return 1;
            }
        }

        private static string RequireValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Option {option} requires a value.");
                Environment.Exit(1);
            }

            return args[++index];
        }

        private static object ParseValue(string value)
        {
            if (bool.TryParse(value, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return doubleValue;
            }

            return value;
        }

        [SupportedOSPlatform("windows")]
        private static async Task<int> ExecuteCommandAsync(string module, string action, Dictionary<string, object> args, bool rawJson, int timeoutMs)
        {
            var request = new
            {
                v = 1,
                correlationId = Guid.NewGuid().ToString(),
                command = new
                {
                    module,
                    action,
                    args,
                },
                options = new
                {
                    timeoutMs,
                    wantProgress = false,
                },
            };

            string payload = JsonSerializer.Serialize(request, JsonRequestOptions);

            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await client.ConnectAsync(timeoutMs).ConfigureAwait(false);
            client.ReadMode = PipeTransmissionMode.Message;

            using (var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true))
            {
                await writer.WriteAsync(payload).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }

            client.WaitForPipeDrain();

            var responseMessage = await ReadMessageAsync(client).ConfigureAwait(false);
            if (string.IsNullOrEmpty(responseMessage))
            {
                Console.Error.WriteLine("Received empty response from the PowerToys runner.");
                return 1;
            }

            var document = JsonSerializer.Deserialize<JsonElement>(responseMessage);
            var status = document.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : "error";

            if (rawJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(document, JsonOutputOptions));
                return string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            }

            if (string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                if (document.TryGetProperty("result", out var resultElement))
                {
                    RenderResult(resultElement);
                }
                else
                {
                    Console.WriteLine("Command completed successfully.");
                }

                return 0;
            }

            if (document.TryGetProperty("error", out var errorElement))
            {
                var code = errorElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : "E_UNKNOWN";
                var message = errorElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "Command failed.";
                Console.Error.WriteLine($"{code}: {message}");
            }
            else
            {
                Console.Error.WriteLine("Command failed.");
            }

            return 1;
        }

        private static async Task<string?> ReadMessageAsync(NamedPipeClientStream client)
        {
            var builder = new StringBuilder();
            using var reader = new StreamReader(client, Encoding.UTF8, false, bufferSize: 1024, leaveOpen: true);
            char[] buffer = new char[1024];
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                builder.Append(buffer, 0, read);
            }

            return builder.ToString();
        }

        private static void RenderResult(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("modules", out var modulesElement) && modulesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var module in modulesElement.EnumerateArray())
                    {
                        var name = module.TryGetProperty("module", out var moduleName) ? moduleName.GetString() : "<module>";
                        Console.WriteLine(name);

                        if (module.TryGetProperty("commands", out var commandsElement) && commandsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var command in commandsElement.EnumerateArray())
                            {
                                var action = command.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : "<action>";
                                var description = command.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() : string.Empty;
                                Console.WriteLine($"  - {action}: {description}");
                            }
                        }
                    }

                    return;
                }

                if (element.TryGetProperty("commands", out var commands) && commands.ValueKind == JsonValueKind.Array)
                {
                    var moduleName = element.TryGetProperty("module", out var moduleElement) ? moduleElement.GetString() : "<module>";
                    Console.WriteLine(moduleName);
                    foreach (var command in commands.EnumerateArray())
                    {
                        var action = command.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : "<action>";
                        var description = command.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() : string.Empty;
                        Console.WriteLine($"  - {action}: {description}");
                    }

                    return;
                }
            }

            Console.WriteLine(JsonSerializer.Serialize(element, JsonOutputOptions));
        }

        private static void PrintHelp()
        {
            Console.WriteLine("PowerToys CLI");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  ptcli --list-modules");
            Console.WriteLine("  ptcli --list-commands <module>");
            Console.WriteLine("  ptcli -m <module> -a <action> [--key value] [--json]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  ptcli --list-modules");
            Console.WriteLine("  ptcli --list-commands awake");
            Console.WriteLine("  ptcli -m awake -a status");
            Console.WriteLine("  ptcli -m awake -a set --mode timed --duration 30m --displayOn true");
        }
    }
}
