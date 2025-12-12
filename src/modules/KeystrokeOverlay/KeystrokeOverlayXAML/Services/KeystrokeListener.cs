// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KeystrokeOverlayUI;
using KeystrokeOverlayUI.Controls;
using KeystrokeOverlayUI.Models;

namespace KeystrokeOverlayUI.Services
{
    // Connects to the native KeystrokeOverlayPipe and converts native JSON
    // batches into KeystrokeEvent objects for the overlay UI.
    public class KeystrokeListener : IDisposable
    {
        private const string PipeName = "KeystrokeOverlayPipe";

        private CancellationTokenSource _cts;

        public event Action<KeystrokeEvent> OnBatchReceived;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var client = new NamedPipeClientStream(
                        serverName: ".",
                        pipeName: PipeName,
                        direction: PipeDirection.In,
                        options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await client.ConnectAsync(token).ConfigureAwait(false);

                    using var reader = new BinaryReader(client, Encoding.UTF8, leaveOpen: false);

                    while (client.IsConnected && !token.IsCancellationRequested)
                    {
                        // Length-prefixed frame
                        int length = reader.ReadInt32();
                        const int MaxFrameSize = 8 * 1024 * 1024;

                        if (length <= 0 || length > MaxFrameSize)
                        {
                            Debug.WriteLine($"KeystrokeListener: invalid frame length {length}, reconnecting");
                            break;
                        }

                        // Read JSON payload
                        byte[] buffer = reader.ReadBytes(length);
                        if (buffer.Length != length)
                        {
                            Debug.WriteLine($"KeystrokeListener: short read {buffer.Length}/{length}, reconnecting");
                            break;
                        }

                        string json = Encoding.UTF8.GetString(buffer);

                        try
                        {
                            // Deserialize batch
                            var root = JsonSerializer.Deserialize(
                                json,
                                KeystrokeEventJsonContext.Default.KeystrokeBatchRoot);

                            if (root?.Events == null || root.Events.Length == 0)
                            {
                                continue;
                            }

                            // Process only DOWN events to avoid duplicates
                            foreach (var e in root.Events)
                            {
                                bool isDown = string.Equals(e.Type, "down", StringComparison.OrdinalIgnoreCase);
                                bool isChar = string.Equals(e.Type, "char", StringComparison.OrdinalIgnoreCase);

                                if (!isDown && !isChar)
                                {
                                    continue;
                                }

                                var uiEvent = new KeystrokeEvent
                                {
                                    VirtualKey = (uint)e.VirtualKey,
                                    IsPressed = true, // Both down and char imply a press for UI purposes
                                    Modifiers = e.Modifiers != null ? new List<string>(e.Modifiers) : new List<string>(),
                                    Text = e.Text,       // Capture the text from backend
                                    EventType = e.Type,   // Capture the type
                                };

                                OnBatchReceived?.Invoke(uiEvent);
                            }
                        }
                        catch (JsonException je)
                        {
                            Debug.WriteLine($"KeystrokeListener: JSON parse error: {je.Message}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"KeystrokeListener: error processing frame: {ex}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"KeystrokeListener: connect error: {ex}");
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }
    }
}
