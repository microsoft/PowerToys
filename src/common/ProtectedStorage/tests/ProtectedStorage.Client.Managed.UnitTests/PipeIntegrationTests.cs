// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.ProtectedStorage;

namespace ProtectedStorage.Client.Managed.UnitTests;

[TestClass]
public sealed class PipeIntegrationTests
{
    [TestMethod]
    [DataRow("success", false)]
    [DataRow("success", true)]
    [DataRow("error", false)]
    [DataRow("error", true)]
    public async Task DelayedReaderReceivesCompleteNativeResponse(string mode, bool readHeaderFirst)
    {
        string executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "ProtectedStorage", "ProtectedStorage.UnitTests.exe"));
        Assert.IsTrue(File.Exists(executable), "Build the native ProtectedStorage.UnitTests project before running pipe integration tests.");
        for (int iteration = 0; iteration < 5; iteration++)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var token = timeout.Token;
            string name = $"PTPS.PipeTests.{Guid.NewGuid():N}";
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("--pipe-test-server");
            start.ArgumentList.Add(name);
            start.ArgumentList.Add(mode);
            using var server = Process.Start(start) ?? throw new IOException("Native test server did not start.");
            var errors = server.StandardError.ReadToEndAsync(token);
            try
            {
                Assert.AreEqual("ready", await server.StandardOutput.ReadLineAsync(token));
                using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    await pipe.ConnectAsync(token);
                    using var document = JsonDocument.Parse("{}");
                    var request = new StorageFrame(1, Guid.NewGuid(), document.RootElement.Clone(), []);
                    await pipe.WriteAsync(PipeProtocol.Encode(request), token);
                    Assert.AreEqual("written", await server.StandardOutput.ReadLineAsync(token));
                    Stream input = pipe;
                    if (readHeaderFirst)
                    {
                        byte[] header = new byte[PipeProtocol.HeaderLength];
                        await pipe.ReadExactlyAsync(header, token);
                        input = new PrefixStream(header, pipe);
                    }

                    await Task.Delay(75, token);
                    var response = await PipeProtocol.ReadAsync(input, token);
                    Assert.AreEqual(request.RequestId, response.RequestId);
                    Assert.AreEqual(request.Command, response.Command);
                    Assert.AreEqual(mode == "error" ? "Unauthorized" : "None", response.Metadata.GetProperty("errorCode").GetString());
                }

                await server.WaitForExitAsync(token);
                Assert.AreEqual(0, server.ExitCode, await errors);
            }
            finally
            {
                if (!server.HasExited)
                {
                    server.Kill(entireProcessTree: true);
                    await server.WaitForExitAsync();
                }
            }
        }
    }

    private sealed class PrefixStream(byte[] prefix, Stream remaining) : Stream
    {
        private int offset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (offset < prefix.Length)
            {
                int count = Math.Min(buffer.Length, prefix.Length - offset);
                prefix.AsMemory(offset, count).CopyTo(buffer);
                offset += count;
                return count;
            }

            return await remaining.ReadAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
