// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using PowerScripts.UI.ViewModels;

namespace ViewModelTests;

[TestClass]
public sealed class PowerScriptOperationPreviewTests
{
    private readonly string _testParent = Path.Combine(Path.GetTempPath(), "PowerScriptsPreviewTests");
    private readonly List<Exception> _cleanupErrors = new();
    private string _testRoot;

    [TestInitialize]
    public void Initialize()
    {
        _testRoot = Path.Combine(_testParent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        var target = Path.GetFullPath(_testRoot);
        Assert.AreEqual(Path.GetFullPath(_testParent), Path.GetDirectoryName(target));
        Assert.IsTrue(Guid.TryParseExact(Path.GetFileName(target), "N", out _));
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }

    [TestMethod]
    public async Task UnavailableOperationDoesNotCreatePackageOrPromptForTrust()
    {
        var backend = new PreviewBackend();
        var template = Template();
        template.UnavailableReason = "Host process visibility is unavailable.";

        var result = await Preview(backend).RunAsync(template, Operation(), null, null, CancellationToken.None);

        Assert.AreEqual(125, result.ExitCode);
        Assert.AreEqual(template.UnavailableReason, result.StdErr);
        Assert.AreEqual(0, backend.CreateCalls);
        Assert.AreEqual(0, backend.RunCalls);
        Assert.AreEqual(0, backend.Revoked.Count);
        Assert.IsFalse(Directory.Exists(PreviewParent));
    }

    [TestMethod]
    public async Task TestRunUsesPrivatePackageAndKeepsExplicitValuesAndNativeResult()
    {
        var installed = Path.Combine(_testRoot, "installed");
        Directory.CreateDirectory(installed);
        var marker = Path.Combine(installed, "keep.txt");
        await File.WriteAllTextAsync(marker, "existing script");
        var backend = new PreviewBackend
        {
            Result = new PowerScriptTransformResult { ExitCode = 7, Text = "command output", StdErr = "command diagnostics" },
        };
        var parameters = JsonNode.Parse("""
            [{"name":"label","schema":{"type":"string"},"required":false},
             {"name":"enabled","schema":{"type":"boolean"},"required":false},
             {"name":"count","schema":{"type":"integer"},"required":false}]
            """)!.AsArray();
        var values = new Dictionary<string, JsonElement>
        {
            ["label"] = JsonSerializer.SerializeToElement(string.Empty),
            ["enabled"] = JsonSerializer.SerializeToElement(false),
            ["count"] = JsonSerializer.SerializeToElement(0),
        };

        var result = await Preview(backend).RunAsync(Template(), Operation(), parameters, values, CancellationToken.None);

        Assert.AreEqual(backend.Result, result);
        Assert.AreEqual(1, backend.CreateCalls);
        Assert.AreEqual(1, backend.RunCalls);
        Assert.AreEqual(Path.GetFullPath(PreviewParent), Path.GetDirectoryName(backend.Root));
        Assert.AreEqual(backend.Root, backend.RunRoot);
        Assert.AreEqual(backend.Id, backend.RunId);
        Assert.AreEqual(backend.Id, backend.Revoked[0]);
        Assert.IsFalse(backend.CleanupTokenWasCancelled);
        Assert.AreEqual(string.Empty, backend.Input.Parameters["label"].GetString());
        Assert.IsFalse(backend.Input.Parameters["enabled"].GetBoolean());
        Assert.AreEqual(0, backend.Input.Parameters["count"].GetInt32());
        Assert.IsNull(backend.Input.Text);
        Assert.IsNull(backend.Input.FilePaths);
        Assert.IsFalse(Directory.Exists(backend.Root));
        Assert.AreEqual("existing script", await File.ReadAllTextAsync(marker));
        Assert.AreEqual(0, _cleanupErrors.Count);
    }

    [TestMethod]
    public async Task DraftEditsCannotChangeTestConfigurationOrRuntimeValues()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueCreation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new PreviewBackend
        {
            BeforeCreate = async (_, _, cancellationToken) =>
            {
                entered.SetResult();
                await continueCreation.Task.WaitAsync(cancellationToken);
            },
        };
        var operation = Operation();
        var parameters = JsonNode.Parse("""[{"name":"count","schema":{"type":"integer","default":0}}]""")!.AsArray();
        var values = new Dictionary<string, JsonElement> { ["count"] = JsonSerializer.SerializeToElement(0) };
        var run = Preview(backend).RunAsync(Template(), operation, parameters, values, CancellationToken.None);
        await entered.Task;

        operation["bindings"]!["enabled"]!["constant"] = true;
        parameters[0]!["schema"]!["default"] = 4;
        values["count"] = JsonSerializer.SerializeToElement(9);
        continueCreation.SetResult();
        await run;

        Assert.IsFalse(backend.Request["operation"]!["bindings"]!["enabled"]!["constant"]!.GetValue<bool>());
        Assert.AreEqual(0, backend.Request["parameters"]![0]!["schema"]!["default"]!.GetValue<int>());
        Assert.AreEqual(0, backend.Input.Parameters["count"].GetInt32());
    }

    [TestMethod]
    public async Task CancellationBeforeTestDoesNotCreateAnyFiles()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var backend = new PreviewBackend();

        await AssertCancelledAsync(() => Preview(backend).RunAsync(Template(), Operation(), null, null, cancellation.Token));

        Assert.AreEqual(0, backend.CreateCalls);
        Assert.IsFalse(Directory.Exists(PreviewParent));
    }

    [TestMethod]
    public async Task CancellationDuringRunStillRevokesOnlyPrivateIdAndRemovesItsPackage()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new PreviewBackend
        {
            DuringRun = async cancellationToken =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new PowerScriptTransformResult();
            },
        };
        using var cancellation = new CancellationTokenSource();
        var run = Preview(backend).RunAsync(Template(), Operation(), null, null, cancellation.Token);
        await entered.Task;

        cancellation.Cancel();
        await AssertCancelledAsync(() => run);

        CollectionAssert.AreEqual(new[] { backend.Id }, backend.Revoked);
        Assert.IsFalse(backend.CleanupTokenWasCancelled);
        Assert.IsFalse(Directory.Exists(backend.Root));
    }

    [TestMethod]
    public async Task TimeoutCancelsRunWithoutCancellingCallerAndStillCleansUp()
    {
        var backend = new PreviewBackend
        {
            DuringRun = async cancellationToken =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new PowerScriptTransformResult();
            },
        };
        using var cancellation = new CancellationTokenSource();

        await AssertCancelledAsync(() => Preview(backend, TimeSpan.FromSeconds(1)).RunAsync(
            Template(), Operation(), null, null, cancellation.Token));

        Assert.IsFalse(cancellation.IsCancellationRequested);
        Assert.AreEqual(1, backend.RunCalls);
        CollectionAssert.AreEqual(new[] { backend.Id }, backend.Revoked);
        Assert.IsFalse(Directory.Exists(backend.Root));
    }

    [TestMethod]
    public async Task PartialCreationFailureRemovesPrivateFilesWithoutRevokingAnotherId()
    {
        var backend = new PreviewBackend
        {
            BeforeCreate = async (root, _, _) =>
            {
                await File.WriteAllTextAsync(Path.Combine(root, "partial.json"), "{}", CancellationToken.None);
                throw new IOException("Creation failed.");
            },
        };

        await Assert.ThrowsExceptionAsync<IOException>(() => Preview(backend).RunAsync(
            Template(), Operation(), null, null, CancellationToken.None));

        Assert.AreEqual(0, backend.RunCalls);
        Assert.AreEqual(0, backend.Revoked.Count);
        Assert.IsFalse(Directory.Exists(backend.Root));
    }

    [TestMethod]
    public async Task OutsideRootHostResponseIsNotExecutedRevokedOrRemoved()
    {
        var outside = Path.Combine(_testRoot, "existing-script");
        Directory.CreateDirectory(outside);
        var existing = Path.Combine(outside, "action.json");
        await File.WriteAllTextAsync(existing, "owned by another script");
        var backend = new PreviewBackend { EntryOverride = existing };

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Preview(backend).RunAsync(
            Template(), Operation(), null, null, CancellationToken.None));

        Assert.AreEqual(0, backend.RunCalls);
        Assert.AreEqual(0, backend.Revoked.Count);
        Assert.AreEqual("owned by another script", await File.ReadAllTextAsync(existing));
        Assert.IsFalse(Directory.Exists(backend.Root));
    }

    [TestMethod]
    public async Task MismatchedDescriptorIdentityIsNotExecutedOrRevoked()
    {
        var backend = new PreviewBackend { DescriptorIdOverride = "existing-trusted-script" };

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => Preview(backend).RunAsync(
            Template(), Operation(), null, null, CancellationToken.None));

        Assert.AreEqual(0, backend.RunCalls);
        Assert.AreEqual(0, backend.Revoked.Count);
        Assert.IsFalse(Directory.Exists(backend.Root));
    }

    [TestMethod]
    public async Task CleanupFailureDoesNotHideNativeOutcomeOrPreventPackageRemoval()
    {
        var backend = new PreviewBackend { RevokeFailure = new IOException("Trust cleanup failed.") };

        var result = await Preview(backend).RunAsync(Template(), Operation(), null, null, CancellationToken.None);

        Assert.AreEqual(backend.Result, result);
        Assert.AreEqual(1, _cleanupErrors.Count);
        Assert.AreEqual(backend.RevokeFailure, _cleanupErrors[0]);
        Assert.IsFalse(Directory.Exists(backend.Root));
    }

    [TestMethod]
    public async Task EachTestGetsIndependentIdentityAndPackageDirectory()
    {
        var first = new PreviewBackend();
        var second = new PreviewBackend();

        await Preview(first).RunAsync(Template(), Operation(), null, null, CancellationToken.None);
        await Preview(second).RunAsync(Template(), Operation(), null, null, CancellationToken.None);

        Assert.AreNotEqual(first.Root, second.Root);
        Assert.AreNotEqual(first.Id, second.Id);
        CollectionAssert.AreEqual(new[] { first.Id }, first.Revoked);
        CollectionAssert.AreEqual(new[] { second.Id }, second.Revoked);
    }

    private string PreviewParent => Path.Combine(_testRoot, "preview");

    private static PowerScriptOperationTemplate Template() => new()
    {
        Name = "Query a PowerToys module",
        Description = "Operation description",
        CommandId = "module.query",
    };

    private static JsonObject Operation() => JsonNode.Parse("""
        {"commandId":"module.query","bindings":{"enabled":{"constant":false}}}
        """)!.AsObject();

    private PowerScriptOperationPreview Preview(PreviewBackend backend, TimeSpan? timeout = null) =>
        new(backend, PreviewParent, _cleanupErrors.Add, timeout);

    private static async Task AssertCancelledAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Assert.Fail("Expected a cancelled test run.");
    }

    private sealed class PreviewBackend : IPowerScriptOperationPreviewBackend
    {
        public int CreateCalls { get; private set; }

        public int RunCalls { get; private set; }

        public string Root { get; private set; }

        public string Id { get; private set; }

        public string RunRoot { get; private set; }

        public string RunId { get; private set; }

        public JsonObject Request { get; private set; }

        public PowerScriptTransformInput Input { get; private set; }

        public List<string> Revoked { get; } = new();

        public bool CleanupTokenWasCancelled { get; private set; }

        public PowerScriptTransformResult Result { get; set; } = new() { Text = "test output" };

        public Func<string, JsonObject, CancellationToken, Task> BeforeCreate { get; set; }

        public Func<CancellationToken, Task<PowerScriptTransformResult>> DuringRun { get; set; }

        public string EntryOverride { get; set; }

        public string DescriptorIdOverride { get; set; }

        public Exception RevokeFailure { get; set; }

        public async Task<string> CreateAsync(string root, JsonObject request, CancellationToken cancellationToken)
        {
            CreateCalls++;
            Root = root;
            Request = request;
            if (BeforeCreate is not null)
            {
                await BeforeCreate(root, request, cancellationToken);
            }

            if (EntryOverride is not null)
            {
                return EntryOverride;
            }

            Id = "preview-" + Guid.NewGuid().ToString("N");
            var package = Path.Combine(root, Id);
            Directory.CreateDirectory(package);
            var entry = Path.Combine(package, "action.json");
            await File.WriteAllTextAsync(entry, "{}", cancellationToken);
            await File.WriteAllTextAsync(entry + ".tool.json", JsonSerializer.Serialize(new { name = DescriptorIdOverride ?? Id }), cancellationToken);
            return entry;
        }

        public Task<PowerScriptTransformResult> RunAsync(string root, string id, PowerScriptTransformInput input, CancellationToken cancellationToken)
        {
            RunCalls++;
            RunRoot = root;
            RunId = id;
            Input = input;
            return DuringRun is null ? Task.FromResult(Result) : DuringRun(cancellationToken);
        }

        public Task RevokeAsync(string root, string id, CancellationToken cancellationToken)
        {
            Assert.AreEqual(Root, root);
            Revoked.Add(id);
            CleanupTokenWasCancelled = cancellationToken.IsCancellationRequested;
            if (RevokeFailure is not null)
            {
                throw RevokeFailure;
            }

            return Task.CompletedTask;
        }
    }
}
