using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeystrokeOverlayUI
{
    public class KeystrokeListener
    {
        private const string PipeName = "PowerToys.KeystrokeOverlay"; // Must match C++
        private CancellationTokenSource _cts;

        public event Action<KeystrokeBatch> OnBatchReceived;

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

                        // 2. Read JSON Payload
                        byte[] buffer = reader.ReadBytes(length);
                        string json = Encoding.UTF8.GetString(buffer);

                        // 3. Deserialize
                        var batch = JsonSerializer.Deserialize<KeystrokeBatch>(json);

                        if (batch != null)
                        {
                            OnBatchReceived?.Invoke(batch);
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

    // Data models matching Batcher.cpp JSON schema
    public class KeystrokeBatch
    {
        public int schema { get; set; }

        public KeystrokeData[] events { get; set; }
    }

    public class KeystrokeData
    {
        public string t { get; set; } // "down", "up", "char"

        public int vk { get; set; }

        public string text { get; set; }

        public string[] mods { get; set; }

        public double ts { get; set; }
    }
}
