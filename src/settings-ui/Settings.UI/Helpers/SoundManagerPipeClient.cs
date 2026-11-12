// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    // Talks to the Sound Manager module interface over its named pipe. Every message is a single JSON document on its own line.
    public sealed class SoundManagerPipeClient : IDisposable
    {
        // The name of the pipe carries the number of the runner, so that a development build and an installed copy never talk to each other.
        public const string PipeNamePrefix = "PowerToys-SoundManager-";

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(5);

        private NamedPipeClientStream _pipe;

        private StreamReader _reader;

        public static string GetPipeName(int runnerProcessId) => $"{PipeNamePrefix}{runnerProcessId}";

        // Asks the module interface which programs are making sound. A quiet program is not in the list, because there is nothing to silence.
        public async Task<List<SoundManagerProgram>> GetProgramsAsync(int runnerProcessId, CancellationToken cancellationToken)
        {
            var answer = await AskAsync(runnerProcessId, new JsonObject { ["command"] = "get_snapshot" }, "ask for the list of programs", cancellationToken);
            if (answer == null)
            {
                return new List<SoundManagerProgram>();
            }

            return ParsePrograms(answer);
        }

        public void Dispose()
        {
            Disconnect();
        }

        // Asks the module interface what Windows does with the other sounds while a program is talking. The number is the one Windows keeps it under, or -1.
        public async Task<int> GetDuckingModeAsync(int runnerProcessId, CancellationToken cancellationToken)
        {
            var answer = await AskAsync(runnerProcessId, new JsonObject { ["command"] = "get_ducking" }, "ask what Windows does with the other sounds", cancellationToken);
            if (answer == null)
            {
                return -1;
            }

            return ParseDuckingMode(answer);
        }

        // Tells the module interface what Windows should do with the other sounds while a program is talking.
        public async Task<bool> SetDuckingModeAsync(int runnerProcessId, int mode, CancellationToken cancellationToken)
        {
            var answer = await AskAsync(runnerProcessId, new JsonObject { ["command"] = "set_ducking", ["value"] = mode }, "tell Windows what to do with the other sounds", cancellationToken);
            return answer != null && ParseResult(answer);
        }

        // Sends a command to the module interface and gives back its answer, or nothing when it does not answer.
        private async Task<string> AskAsync(int runnerProcessId, JsonObject command, string what, CancellationToken cancellationToken)
        {
            if (runnerProcessId <= 0 || !await ConnectAsync(runnerProcessId, cancellationToken))
            {
                return null;
            }

            try
            {
                await WriteAsync(command, cancellationToken);
                return await ReadLineAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError($"The Sound Manager settings could not {what}: {ex.Message}");
                return null;
            }
            finally
            {
                Disconnect();
            }
        }

        // Reads the choice of Windows out of the answer of the module interface.
        private static int ParseDuckingMode(string line)
        {
            var value = GetValue(line, "ducking");
            if (value == null || value.GetValueKind() != JsonValueKind.Number)
            {
                return -1;
            }

            return value.GetValue<int>();
        }

        // Reads the answer of a change out of the answer of the module interface.
        private static bool ParseResult(string line)
        {
            var value = GetValue(line, "ok");
            return value != null && value.GetValueKind() == JsonValueKind.True;
        }

        // Reads a value out of the answer of the module interface, or nothing when the answer does not hold it.
        private static JsonNode GetValue(string line, string name)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            try
            {
                if (JsonNode.Parse(line) is JsonObject message)
                {
                    return GetValue(message, name);
                }
            }
            catch (JsonException)
            {
                // The answer of the module interface is not JSON, so it holds nothing.
            }

            return null;
        }

        // Reads a value out of the message of the module interface.
        private static JsonNode GetValue(JsonObject message, string name)
        {
            return message.TryGetPropertyValue(name, out var value) ? value : null;
        }

        // Reads the programs out of the answer of the module interface. A program that makes sound in more than one way is in the list once.
        private static List<SoundManagerProgram> ParsePrograms(string line)
        {
            var programs = new List<SoundManagerProgram>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (GetValue(line, "apps") is not JsonArray appList)
            {
                return programs;
            }

            foreach (var app in appList)
            {
                if (app is not JsonObject entry)
                {
                    continue;
                }

                // A shortcut must never touch the recording side, because muting a recording mutes the whole microphone.
                if (GetValue(entry, "isCapture")?.GetValueKind() == JsonValueKind.True)
                {
                    continue;
                }

                var path = GetString(entry, "processPath");
                var displayName = GetString(entry, "displayName");

                string program;
                if (!string.IsNullOrEmpty(path))
                {
                    program = Path.GetFileName(path);
                }
                else if (string.IsNullOrEmpty(displayName) || displayName == "System Sounds")
                {
                    // The sound does not belong to a program, so there is nothing to pick.
                    continue;
                }
                else
                {
                    // The program could not be opened, so all that is left is the name of the executable file.
                    program = displayName;
                }

                if (string.IsNullOrEmpty(program) || !seen.Add(program))
                {
                    continue;
                }

                programs.Add(new SoundManagerProgram
                {
                    Program = program,
                    Name = Path.GetFileNameWithoutExtension(program),
                    ProcessPath = path ?? string.Empty,
                });
            }

            programs.Sort((first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name));

            return programs;
        }

        private static string GetString(JsonObject entry, string name)
        {
            var value = GetValue(entry, name);
            if (value != null && value.GetValueKind() == JsonValueKind.String)
            {
                return value.GetValue<string>();
            }

            return string.Empty;
        }

        private async Task<bool> ConnectAsync(int runnerProcessId, CancellationToken cancellationToken)
        {
            var pipeName = GetPipeName(runnerProcessId);

            NamedPipeClientStream pipe = null;
            try
            {
                pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError($"The Sound Manager settings could not reach the module interface: {ex.Message}");
                pipe?.Dispose();
                return false;
            }

            _pipe = pipe;
            _reader = new StreamReader(pipe, new UTF8Encoding(false));
            return true;
        }

        private async Task WriteAsync(JsonObject command, CancellationToken cancellationToken)
        {
            if (_pipe == null || !_pipe.IsConnected)
            {
                throw new IOException("The Sound Manager module interface is not there");
            }

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command) + "\n");
            await _pipe.WriteAsync(payload, cancellationToken);
        }

        private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            var read = _reader.ReadLineAsync(cancellationToken).AsTask();

            var finished = await Task.WhenAny(read, Task.Delay(AnswerTimeout, cancellationToken));
            if (finished != read)
            {
                throw new TimeoutException("The Sound Manager module interface did not answer in time");
            }

            return await read;
        }

        private void Disconnect()
        {
            _reader?.Dispose();
            _reader = null;

            _pipe?.Dispose();
            _pipe = null;
        }
    }
}
