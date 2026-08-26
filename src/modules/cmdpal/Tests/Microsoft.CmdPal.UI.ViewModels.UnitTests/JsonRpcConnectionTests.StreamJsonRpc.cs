// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class JsonRpcConnectionTests
{
    [TestMethod]
    public async Task PendingRequest_FailsPromptly_WhenReaderReachesEof()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        try
        {
            var requestTask = harness.Host.SendRequestAsync("inflight", null, cts.Token);
            _ = await ReadFramedAsync(harness.ExtensionReads, cts.Token);

            harness.ExtensionWrites.Dispose();

            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () => await requestTask.WaitAsync(cts.Token));
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task ResponseBeforeEndOfStream_IsReturned()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        try
        {
            var requestTask = harness.Host.SendRequestAsync("one-shot", null, cts.Token);
            var (_, requestBody) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var requestDocument = JsonDocument.Parse(requestBody);
            var requestId = requestDocument.RootElement.GetProperty("id").GetInt32();

            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = requestId,
                ["result"] = new JsonObject { ["value"] = 42 },
            };
            await WriteFramedAsync(harness.ExtensionWrites, response.ToJsonString(), cts.Token);
            harness.ExtensionWrites.Dispose();

            var result = await requestTask.WaitAsync(cts.Token);
            Assert.AreEqual(42, result.Result?.GetProperty("value").GetInt32());
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task TimedOutRequest_SendsCancellation()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromMilliseconds(250));

        try
        {
            var requestTask = harness.Host.SendRequestAsync("inflight", null, cts.Token);
            var (_, requestBody) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var requestDocument = JsonDocument.Parse(requestBody);
            var requestId = requestDocument.RootElement.GetProperty("id").GetInt32();

            await Assert.ThrowsExceptionAsync<TimeoutException>(async () => await requestTask);

            var (_, cancellationBody) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var cancellationDocument = JsonDocument.Parse(cancellationBody);
            Assert.AreEqual("$/cancelRequest", cancellationDocument.RootElement.GetProperty("method").GetString());
            Assert.AreEqual(requestId, cancellationDocument.RootElement.GetProperty("params").GetProperty("id").GetInt32());
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task ErrorResponse_PreservesStructuredData()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();

        try
        {
            var requestTask = harness.Host.SendRequestAsync("boom", null, cts.Token);
            var (_, requestBody) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var requestDocument = JsonDocument.Parse(requestBody);
            var requestId = requestDocument.RootElement.GetProperty("id").GetInt32();

            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = requestId,
                ["error"] = new JsonObject
                {
                    ["code"] = JsonRpcError.InvalidParams,
                    ["message"] = "bad params",
                    ["data"] = new JsonObject { ["reason"] = "detail" },
                },
            };
            await WriteFramedAsync(harness.ExtensionWrites, response.ToJsonString(), cts.Token);

            var result = await requestTask.WaitAsync(cts.Token);
            Assert.AreEqual(requestId, result.Id);
            Assert.AreEqual(JsonRpcError.InvalidParams, result.Error?.Code);
            Assert.AreEqual("detail", result.Error?.Data?["reason"]?.GetValue<string>());
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task SlowNotificationHandler_DoesNotBlockFrameReading()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            harness.Host.RegisterNotificationHandler("slow", _ => release.Task.Wait(cts.Token));
            harness.Host.RegisterRequestHandler("ping", (_, _) =>
                Task.FromResult<JsonNode?>(new JsonObject { ["pong"] = true }));

            await WriteFramedAsync(harness.ExtensionWrites, BuildNotification("slow", new JsonObject()), cts.Token);
            await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(1, "ping", null), cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual(1, document.RootElement.GetProperty("id").GetInt32());
            Assert.IsTrue(document.RootElement.GetProperty("result").GetProperty("pong").GetBoolean());
        }
        finally
        {
            release.TrySetResult();
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task ReentrantNotificationHandler_SendingRequest_DoesNotDeadlock()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            harness.Host.RegisterNotificationHandler("reenter", _ =>
            {
                try
                {
                    var response = harness.Host
                        .SendRequestAsync("callback", null, JsonRpcTestJsonContext.Default.TestPayload, cts.Token)
                        .GetAwaiter()
                        .GetResult();
                    done.TrySetResult(response?.Message);
                }
                catch (Exception ex)
                {
                    done.TrySetException(ex);
                }
            });

            await WriteFramedAsync(harness.ExtensionWrites, BuildNotification("reenter", new JsonObject()), cts.Token);

            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            var id = document.RootElement.GetProperty("id").GetInt32();

            await RespondWithResultAsync(harness.ExtensionWrites, id, new JsonObject { ["message"] = "ok" }, cts.Token);

            Assert.AreEqual("ok", await done.Task.WaitAsync(cts.Token));
        }
        finally
        {
            harness.Host.Dispose();
        }
    }
}
