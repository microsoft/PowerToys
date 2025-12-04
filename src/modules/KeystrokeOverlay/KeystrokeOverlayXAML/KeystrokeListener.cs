// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KeystrokeOverlayUI.Controls;

namespace KeystrokeOverlayUI
{
    public class KeystrokeListener : IDisposable
    {
        private static readonly JsonSerializerOptions CachedJsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

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

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.In);
                    await client.ConnectAsync(token);

                    using var reader = new BinaryReader(client);

                    while (client.IsConnected && !token.IsCancellationRequested)
                    {
                        // 1. Read Length (4 bytes / DWORD) matching PipeServer.cpp
                        int length = reader.ReadInt32();

                        // Sanity-check length (same guard as native side)
                        const int MaxFrameSize = 8 * 1024 * 1024;
                        if (length <= 0 || length > MaxFrameSize)
                        {
                            Debug.WriteLine($"KeystrokeListener: invalid frame length {length}, reconnecting");
                            break; // break inner loop and reconnect
                        }

                        // 2. Read JSON Payload
                        byte[] buffer = reader.ReadBytes(length);
                        if (buffer.Length != length)
                        {
                            Debug.WriteLine($"KeystrokeListener: short read {buffer.Length} of {length}, reconnecting");
                            break; // broken frame/connection, reconnect
                    }

                        string json = Encoding.UTF8.GetString(buffer);

                        // 3. Deserialize (case-insensitive to match native lowercase keys)
                        try
                        {
                            var batch = JsonSerializer.Deserialize<KeystrokeEvent>(json, CachedJsonOptions);

                            if (!batch.Equals(default(KeystrokeEvent)))
                            {
                                OnBatchReceived?.Invoke(batch);
                            }
                        }
                        catch (JsonException je)
                        {
                            Debug.WriteLine($"KeystrokeListener: JSON deserialization failed: {je.Message}");

                        // Skip this frame and continue reading
                        }
                    }
                }
                catch (Exception)
                {
                    // Pipe broke or server not ready. Wait and retry.
                    await Task.Delay(1000, token);
                }
            }
        }
    }
}
