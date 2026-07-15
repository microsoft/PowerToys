// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class JsonRpcConnectionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    [TestMethod]
    public async Task SendRequest_ReceivesCorrelatedResult()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var requestTask = harness.Host.SendRequestAsync(
                "echo",
                new JsonObject { ["value"] = "cafe latte" },
                JsonRpcTestJsonContext.Default.TestPayload,
                cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            var id = document.RootElement.GetProperty("id").GetInt32();
            Assert.AreEqual("echo", document.RootElement.GetProperty("method").GetString());
            Assert.AreEqual("cafe latte", document.RootElement.GetProperty("params").GetProperty("value").GetString());

            await RespondWithResultAsync(harness.ExtensionWrites, id, new JsonObject { ["message"] = "cafe latte" }, cts.Token);

            var result = await requestTask.WaitAsync(cts.Token);
            Assert.IsNotNull(result);
            Assert.AreEqual("cafe latte", result!.Message);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Request_UsesByteAccurateContentLengthForMultiByteUtf8()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            const string MultiByte = "\u65e5\u672c\u8a9e\ud83d\ude00";
            var requestTask = harness.Host.SendRequestAsync(
                "unicode",
                new JsonObject { ["text"] = MultiByte },
                cts.Token);

            var (contentLength, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);

            Assert.AreEqual(Encoding.UTF8.GetByteCount(body), contentLength, "Content-Length must be the UTF-8 byte count of the body.");

            using var document = JsonDocument.Parse(body);
            Assert.AreEqual(MultiByte, document.RootElement.GetProperty("params").GetProperty("text").GetString());

            var id = document.RootElement.GetProperty("id").GetInt32();
            await RespondWithResultAsync(harness.ExtensionWrites, id, null, cts.Token);
            await requestTask.WaitAsync(cts.Token);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Inbound_DecodesMultiByteUtf8BodyByByteAccurateContentLength()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            const string MultiByte = "\u65e5\u672c\u8a9e\ud83d\ude00";
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Host.RegisterNotificationHandler("unicode", element =>
            {
                received.TrySetResult(element.GetProperty("text").GetString() ?? string.Empty);
            });

            // Build the raw JSON body with literal multi-byte characters so the framed
            // bytes genuinely exceed the character count. This exercises the decode path
            // reading exactly Content-Length bytes rather than characters.
            var rawBody = "{\"jsonrpc\":\"2.0\",\"method\":\"unicode\",\"params\":{\"text\":\"" + MultiByte + "\"}}";
            Assert.IsTrue(Encoding.UTF8.GetByteCount(rawBody) > rawBody.Length, "The raw body should contain multi-byte UTF-8 characters.");

            await harness.ExtensionWrites.WriteAsync(Frame(rawBody), cts.Token);
            await harness.ExtensionWrites.FlushAsync(cts.Token);

            var text = await received.Task.WaitAsync(cts.Token);
            Assert.AreEqual(MultiByte, text);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task ConcurrentRequests_AreCorrelatedIndependently()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var firstTask = harness.Host.SendRequestAsync("first", null, JsonRpcTestJsonContext.Default.TestPayload, cts.Token);
            var secondTask = harness.Host.SendRequestAsync("second", null, JsonRpcTestJsonContext.Default.TestPayload, cts.Token);

            var idByMethod = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < 2; i++)
            {
                var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
                using var document = JsonDocument.Parse(body);
                var method = document.RootElement.GetProperty("method").GetString() ?? string.Empty;
                idByMethod[method] = document.RootElement.GetProperty("id").GetInt32();
            }

            // Respond in the opposite order to prove correlation is by id, not arrival order.
            await RespondWithResultAsync(harness.ExtensionWrites, idByMethod["second"], new JsonObject { ["message"] = "two" }, cts.Token);
            await RespondWithResultAsync(harness.ExtensionWrites, idByMethod["first"], new JsonObject { ["message"] = "one" }, cts.Token);

            var firstResult = await firstTask.WaitAsync(cts.Token);
            var secondResult = await secondTask.WaitAsync(cts.Token);

            Assert.AreEqual("one", firstResult!.Message);
            Assert.AreEqual("two", secondResult!.Message);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task SendRequest_TimesOut_WhenNoResponseArrives()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromMilliseconds(250));
        try
        {
            await Assert.ThrowsExceptionAsync<TimeoutException>(async () =>
                await harness.Host.SendRequestAsync("noReply", null, cts.Token));
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task SendRequestGeneric_Throws_OnErrorResponse()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var requestTask = harness.Host.SendRequestAsync("boom", null, JsonRpcTestJsonContext.Default.TestPayload, cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            var id = document.RootElement.GetProperty("id").GetInt32();

            await RespondWithErrorAsync(harness.ExtensionWrites, id, JsonRpcError.InvalidParams, "bad params", cts.Token);

            var exception = await Assert.ThrowsExceptionAsync<JsonRpcException>(async () => await requestTask.WaitAsync(cts.Token));
            Assert.AreEqual(JsonRpcError.InvalidParams, exception.Code);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task SendRequestGeneric_ReturnsDefault_WhenResultIsNull()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var requestTask = harness.Host.SendRequestAsync("nothing", null, JsonRpcTestJsonContext.Default.TestPayload, cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            var id = document.RootElement.GetProperty("id").GetInt32();

            await RespondWithResultAsync(harness.ExtensionWrites, id, null, cts.Token);

            var result = await requestTask.WaitAsync(cts.Token);
            Assert.IsNull(result);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Notification_IsDispatchedToHandler()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var received = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Host.RegisterNotificationHandler("greeting", element =>
            {
                received.TrySetResult(element.GetProperty("name").GetString());
            });

            await WriteFramedAsync(harness.ExtensionWrites, BuildNotification("greeting", new JsonObject { ["name"] = "world" }), cts.Token);

            var name = await received.Task.WaitAsync(cts.Token);
            Assert.AreEqual("world", name);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Notification_IsDispatched_WhenBytesArriveOneAtATime()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var received = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Host.RegisterNotificationHandler("partial", element =>
            {
                received.TrySetResult(element.GetProperty("name").GetString());
            });

            var frame = Frame(BuildNotification("partial", new JsonObject { ["name"] = "trickle" }));
            var singleByte = new byte[1];
            foreach (var value in frame)
            {
                singleByte[0] = value;
                await harness.ExtensionWrites.WriteAsync(singleByte.AsMemory(0, 1), cts.Token);
                await harness.ExtensionWrites.FlushAsync(cts.Token);
            }

            var name = await received.Task.WaitAsync(cts.Token);
            Assert.AreEqual("trickle", name);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Notifications_AreDispatched_WhenTwoMessagesArriveInOneWrite()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var count = 0;
            var bothReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Host.RegisterNotificationHandler("tick", _ =>
            {
                if (Interlocked.Increment(ref count) == 2)
                {
                    bothReceived.TrySetResult();
                }
            });

            var first = Frame(BuildNotification("tick", null));
            var second = Frame(BuildNotification("tick", null));
            var combined = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, combined, 0, first.Length);
            Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);

            await harness.ExtensionWrites.WriteAsync(combined, cts.Token);
            await harness.ExtensionWrites.FlushAsync(cts.Token);

            await bothReceived.Task.WaitAsync(cts.Token);
            Assert.AreEqual(2, count);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task InboundRequest_IsDispatched_AndResponseReturned()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            harness.Host.RegisterRequestHandler("ping", (element, _) =>
            {
                var value = element.GetProperty("n").GetInt32();
                return Task.FromResult<JsonNode?>(new JsonObject { ["pong"] = value });
            });

            await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(7, "ping", new JsonObject { ["n"] = 42 }), cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual(7, document.RootElement.GetProperty("id").GetInt32());
            Assert.AreEqual(42, document.RootElement.GetProperty("result").GetProperty("pong").GetInt32());
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task InboundRequest_UnknownMethod_ReturnsMethodNotFound()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(9, "does-not-exist", null), cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual(9, document.RootElement.GetProperty("id").GetInt32());
            Assert.AreEqual(JsonRpcError.MethodNotFound, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Dispose_CancelsPendingRequests()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        var requestTask = harness.Host.SendRequestAsync("wait", null, cts.Token);

        // Make sure the request has actually been written before disposing.
        _ = await ReadFramedAsync(harness.ExtensionReads, cts.Token);

        harness.Host.Dispose();

        await Assert.ThrowsExceptionAsync<JsonRpcException>(async () => await requestTask.WaitAsync(cts.Token));
    }

    private static Harness CreateHarness(TimeSpan? requestTimeout = null)
    {
        var toHost = new Pipe();
        var fromHost = new Pipe();

        var host = new JsonRpcConnection(
            toHost.Reader.AsStream(),
            fromHost.Writer.AsStream(),
            errorStream: null,
            requestTimeout: requestTimeout);
        host.StartListening();

        return new Harness(host, fromHost.Reader.AsStream(), toHost.Writer.AsStream());
    }

    private static string BuildNotification(string method, JsonNode? parameters)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };

        if (parameters is not null)
        {
            message["params"] = parameters;
        }

        return message.ToJsonString();
    }

    private static string BuildRequest(int id, string method, JsonNode? parameters)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };

        if (parameters is not null)
        {
            message["params"] = parameters;
        }

        return message.ToJsonString();
    }

    private static Task RespondWithResultAsync(Stream stream, int id, JsonNode? result, CancellationToken cancellationToken)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };

        return WriteFramedAsync(stream, message.ToJsonString(), cancellationToken);
    }

    private static Task RespondWithErrorAsync(Stream stream, int id, int code, string errorMessage, CancellationToken cancellationToken)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = errorMessage,
            },
        };

        return WriteFramedAsync(stream, message.ToJsonString(), cancellationToken);
    }

    private static byte[] Frame(string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        var buffer = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
        Buffer.BlockCopy(body, 0, buffer, header.Length, body.Length);
        return buffer;
    }

    private static async Task WriteFramedAsync(Stream stream, string json, CancellationToken cancellationToken)
    {
        var frame = Frame(json);
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<(int ContentLength, string Body)> ReadFramedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>(64);
        var single = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(single.AsMemory(0, 1), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The stream closed before a full JSON-RPC header was read.");
            }

            headerBytes.Add(single[0]);
            var count = headerBytes.Count;
            if (count >= 4 &&
                headerBytes[count - 4] == (byte)'\r' &&
                headerBytes[count - 3] == (byte)'\n' &&
                headerBytes[count - 2] == (byte)'\r' &&
                headerBytes[count - 1] == (byte)'\n')
            {
                break;
            }
        }

        var header = Encoding.ASCII.GetString(headerBytes.ToArray());
        var contentLength = ParseContentLength(header);

        var body = new byte[contentLength];
        var offset = 0;
        while (offset < contentLength)
        {
            var read = await stream.ReadAsync(body.AsMemory(offset, contentLength - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The stream closed before the full JSON-RPC body was read.");
            }

            offset += read;
        }

        return (contentLength, Encoding.UTF8.GetString(body));
    }

    private static int ParseContentLength(string header)
    {
        foreach (var line in header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            if (line.AsSpan(0, separator).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(line.AsSpan(separator + 1).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidDataException("The framed message did not contain a Content-Length header.");
    }

    private sealed class Harness
    {
        public Harness(JsonRpcConnection host, Stream extensionReads, Stream extensionWrites)
        {
            Host = host;
            ExtensionReads = extensionReads;
            ExtensionWrites = extensionWrites;
        }

        public JsonRpcConnection Host { get; }

        public Stream ExtensionReads { get; }

        public Stream ExtensionWrites { get; }
    }

    private sealed record TestPayload
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(TestPayload))]
    private sealed partial class JsonRpcTestJsonContext : JsonSerializerContext
    {
    }
}
