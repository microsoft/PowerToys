// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class EndpointChannel
{
    private readonly string runId;
    private DateTime bootstrapDeadlineUtc;
    private int sequence;
    private int leaseSequence;

    public EndpointChannel(string runId, string inputRoot, string outputRoot)
    {
        this.runId = runId;
        InputRoot = inputRoot;
        OutputRoot = outputRoot;
        Directory.CreateDirectory(Path.Combine(inputRoot, "requests"));
        Directory.CreateDirectory(Path.Combine(inputRoot, "leases"));
        Directory.CreateDirectory(outputRoot);
    }

    public string InputRoot { get; }

    public string OutputRoot { get; }

    public bool Ready => File.Exists(Path.Combine(OutputRoot, "ready.json.ready"));

    public void WriteLease()
    {
        var number = Interlocked.Increment(ref leaseSequence);

        // Legacy Sandbox may cache an already-open redirected file even when the
        // host rewrites it. Publish immutable generations, like request messages.
        RunFiles.Write(
            Path.Combine(InputRoot, "leases", $"{number:D8}.json"),
            new { RunId = runId, Sequence = number, TimestampUtc = DateTime.UtcNow });
    }

    public void BeginBootstrap()
    {
        bootstrapDeadlineUtc = DateTime.UtcNow.AddMinutes(15);
        var path = Path.Combine(InputRoot, "bootstrap.json");
        var configuration = RunFiles.Read(path);
        configuration["BootstrapDeadlineUtc"] = bootstrapDeadlineUtc;
        RunFiles.Write(path, configuration);
    }

    public JsonObject WaitForReady(Action? discover = null)
    {
        RunFiles.Wait(
            () => Ready,
            bootstrapDeadlineUtc - DateTime.UtcNow,
            $"Endpoint bootstrap did not acknowledge run {runId}. See {OutputRoot}.",
            () =>
            {
                ThrowIfFailed();
                discover?.Invoke();
            });
        var ready = RunFiles.Read(Path.Combine(OutputRoot, "ready.json"));
        RequireCorrelation(ready);
        return ready;
    }

    public JsonObject Request(string action, JsonObject? arguments = null, int timeoutSeconds = 40)
    {
        var request = arguments ?? new JsonObject();
        var number = ++sequence;
        request["RunId"] = runId;
        request["Sequence"] = number;
        request["Action"] = action;
        var requestPath = Path.Combine(InputRoot, "requests", $"{number:D4}.json");
        var resultPath = Path.Combine(OutputRoot, $"{number:D4}.json");
        RunFiles.Write(requestPath, request);
        try
        {
            RunFiles.Wait(
                () => File.Exists(resultPath + ".ready"),
                TimeSpan.FromSeconds(timeoutSeconds),
                $"Endpoint command {action} #{number} timed out; see {OutputRoot}.",
                ThrowIfFailed);
            var result = RunFiles.Read(resultPath);
            RequireCorrelation(result);
            if (result["Sequence"]?.GetValue<int>() != number || result["Status"]?.GetValue<string>() != "Completed")
            {
                throw new InvalidOperationException($"Endpoint {action} failed: {result["Error"]}; {result["ScriptStackTrace"]}");
            }

            return result["Result"] as JsonObject ?? throw new InvalidDataException($"Missing {action} result.");
        }
        finally
        {
            if (action == "Connect")
            {
                // Key transfer is setup only. Do not keep it in TRX attachments or cleanup evidence.
                RunFiles.Write(requestPath, new { RunId = runId, Sequence = number, Action = "Connect", Redacted = true });
            }
        }
    }

    private void RequireCorrelation(JsonObject document)
    {
        if (document["RunId"]?.GetValue<string>() != runId)
        {
            throw new InvalidDataException("Endpoint result belongs to another run.");
        }
    }

    private void ThrowIfFailed()
    {
        var path = Path.Combine(OutputRoot, "failed.json");
        if (File.Exists(path))
        {
            var failure = RunFiles.Read(path);
            RequireCorrelation(failure);
            throw new InvalidOperationException($"Endpoint worker failed: {failure["Error"]}; {failure["ScriptStackTrace"]}");
        }
    }
}
