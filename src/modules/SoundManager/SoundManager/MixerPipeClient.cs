// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;

namespace SoundManager
{
    // Talks to the module interface over a named pipe. Every message is one JSON document on its own line.
    public sealed class MixerPipeClient : IDisposable
    {
        // The peak levels are read much more often than the rest of the mixer.
        private static readonly TimeSpan PeakInterval = TimeSpan.FromMilliseconds(60);

        private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(300);

        // How long the window waits for the answer of a change before it gives up on it.
        private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(5);

        // How long the window waits for the module interface to open the pipe.
        private const int ConnectTimeoutMilliseconds = 10000;

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly SemaphoreSlim _writeLock = new(1, 1);

        // The answers of the changes that are still waiting, by the number that was sent with them.
        private readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _answers = new();

        private NamedPipeClientStream _pipe;
        private StreamReader _reader;
        private long _nextId;

        public event EventHandler<MixerSnapshot> SnapshotReceived;

        public event EventHandler<MixerPeaks> PeaksReceived;

        // A connection that is still open is closed first, so the window can connect again.
        public async Task<bool> ConnectAsync(string pipeName, CancellationToken cancellationToken)
        {
            Disconnect();

            if (string.IsNullOrEmpty(pipeName))
            {
                Logger.LogError("Sound Manager was started without the name of the pipe");
                return false;
            }

            NamedPipeClientStream pipe = null;
            try
            {
                pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                await pipe.ConnectAsync(ConnectTimeoutMilliseconds, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Sound Manager could not reach the module interface: {ex.Message}");
                pipe?.Dispose();
                return false;
            }

            _pipe = pipe;
            _reader = new StreamReader(pipe, new UTF8Encoding(false));
            return true;
        }

        // Reads the state of the mixer over and over, so the meters keep moving. Runs off the user interface thread, because reading from the pipe blocks.
        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            var nextSnapshot = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                var askForSnapshot = DateTime.UtcNow >= nextSnapshot;

                try
                {
                    var request = new JsonObject { ["command"] = askForSnapshot ? "get_snapshot" : "get_peaks" };
                    if (!await SendAsync(request, cancellationToken))
                    {
                        return;
                    }

                    var line = await ReadMixerLineAsync(cancellationToken);
                    if (line == null)
                    {
                        return;
                    }

                    if (askForSnapshot)
                    {
                        // The next snapshot is counted from the moment this one is done, so a slow module interface does not push the peaks out.
                        nextSnapshot = DateTime.UtcNow + SnapshotInterval;

                        var snapshot = JsonSerializer.Deserialize<MixerSnapshot>(line, JsonOptions);
                        if (snapshot != null)
                        {
                            SnapshotReceived?.Invoke(this, snapshot);
                        }
                    }
                    else
                    {
                        var peaks = JsonSerializer.Deserialize<MixerPeaks>(line, JsonOptions);
                        if (peaks != null)
                        {
                            PeaksReceived?.Invoke(this, peaks);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException ex)
                {
                    Logger.LogWarning($"The module interface closed the connection: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    // The stream went wrong. The window looks for the module interface again.
                    Logger.LogError($"Sound Manager could not read from the module interface: {ex.Message}");
                    return;
                }

                await Task.Delay(PeakInterval, cancellationToken);
            }
        }

        public Task<bool> SetAppVolumeAsync(uint processId, bool isCapture, double volume)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_app_volume",
                ["processId"] = processId,
                ["isCapture"] = isCapture,
                ["value"] = volume,
            });
        }

        public Task<bool> SetAppMuteAsync(uint processId, bool isCapture, bool muted)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_app_mute",
                ["processId"] = processId,
                ["isCapture"] = isCapture,
                ["value"] = muted,
            });
        }

        // Windows can only scale the two channels, it cannot fold the sound into one.
        public Task<bool> SetAppBalanceAsync(uint processId, bool isCapture, double left, double right)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_app_balance",
                ["processId"] = processId,
                ["isCapture"] = isCapture,
                ["value"] = left,
                ["secondValue"] = right,
            });
        }

        public Task<bool> SetAppDeviceAsync(uint processId, bool isCapture, string deviceId)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_app_device",
                ["processId"] = processId,
                ["isCapture"] = isCapture,
                ["deviceId"] = deviceId,
            });
        }

        public Task<bool> SetDeviceVolumeAsync(string deviceId, double volume)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_device_volume",
                ["deviceId"] = deviceId,
                ["value"] = volume,
            });
        }

        public Task<bool> SetDeviceMuteAsync(string deviceId, bool muted)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_device_mute",
                ["deviceId"] = deviceId,
                ["value"] = muted,
            });
        }

        // The old Sound dialog calls this "Listen to this device".
        public Task<bool> SetDeviceListenAsync(string deviceId, bool listening)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_device_listen",
                ["deviceId"] = deviceId,
                ["value"] = listening,
            });
        }

        // Windows keeps two default devices: one for everything and one for calls and chats.
        public Task<bool> SetDefaultDeviceAsync(string deviceId, bool communications)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_default_device",
                ["deviceId"] = deviceId,
                ["communications"] = communications,
            });
        }

        // Windows can only scale the two channels, it cannot fold the sound into one.
        public Task<bool> SetDeviceBalanceAsync(string deviceId, double left, double right)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "set_device_balance",
                ["deviceId"] = deviceId,
                ["value"] = left,
                ["secondValue"] = right,
            });
        }

        // Plays the test sound through a device.
        public Task<bool> TestDeviceAsync(string deviceId)
        {
            return SendCommandAsync(new JsonObject
            {
                ["command"] = "test_device",
                ["deviceId"] = deviceId,
            });
        }

        public void Dispose()
        {
            Disconnect();
        }

        // Sends a change and waits for the answer, so the window knows whether Windows took it.
        private async Task<bool> SendCommandAsync(JsonObject command, CancellationToken cancellationToken = default)
        {
            var id = Interlocked.Increment(ref _nextId);
            command["id"] = id;

            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _answers[id] = answer;

            try
            {
                if (!await SendAsync(command, cancellationToken))
                {
                    return false;
                }

                var finished = await Task.WhenAny(answer.Task, Task.Delay(AnswerTimeout, cancellationToken));
                if (finished != answer.Task)
                {
                    // No answer came in time. That is not a refusal, so the next snapshot settles it.
                    return true;
                }

                return await answer.Task;
            }
            finally
            {
                _answers.TryRemove(id, out _);
            }
        }

        // The answer to a change comes in between the snapshots, so it is put aside.
        private async Task<string> ReadMixerLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    return null;
                }

                if (!TryTakeAnswer(line))
                {
                    return line;
                }
            }
        }

        private bool TryTakeAnswer(string line)
        {
            JsonNode node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                return false;
            }

            if (node is not JsonObject message || !message.TryGetPropertyValue("id", out var id))
            {
                return false;
            }

            if (!_answers.TryGetValue(id.GetValue<long>(), out var answer))
            {
                return true;
            }

            var ok = message.TryGetPropertyValue("ok", out var value) && value.GetValue<bool>();
            answer.TrySetResult(ok);
            return true;
        }

        private async Task<bool> SendAsync(JsonObject command, CancellationToken cancellationToken = default)
        {
            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command) + "\n");

            try
            {
                await _writeLock.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            try
            {
                if (_pipe == null || !_pipe.IsConnected)
                {
                    return false;
                }

                await _pipe.WriteAsync(payload, cancellationToken);
                return true;
            }
            catch (IOException ex)
            {
                // The module interface closed the pipe, which happens when PowerToys shuts down.
                Logger.LogWarning($"Could not send a command to the module interface: {ex.Message}");
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
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
