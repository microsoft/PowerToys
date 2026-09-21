// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class WinAppSandboxProtocolTests
{
    private const string Epoch = "opaque-provider-epoch-not-a-guid";
    private static readonly Guid Instance = Guid.Parse("d2b96d2f-5b49-42d1-9576-227250294fb0");
    private static readonly Guid UnrelatedInstance = Guid.Parse("638638e2-b7c3-4f31-b779-cb7715cb0145");

    [TestMethod]
    public void GuestLoginProbeRequiresAnExplicitSuccessfulGuestExitCode()
    {
        WinAppSandboxProtocol.RequireGuestCommandSuccess("""{"ExitCode":0}""");
        foreach (var invalid in new[] { "{}", """{"ExitCode":null}""", """{"ExitCode":"0"}""", """{"ExitCode":0,"ExitCode":1}""" })
        {
            Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireGuestCommandSuccess(invalid));
        }

        var error = Assert.ThrowsExactly<WinAppSandboxException>(() =>
            WinAppSandboxProtocol.RequireGuestCommandSuccess("""{"ExitCode":7}"""));
        Assert.AreEqual(7, error.ExitCode);
        Assert.AreEqual(unchecked((int)0x80070520), WinAppSandboxProtocol.NoSuchLogonSession);
    }

    [TestMethod]
    public void EmptyInventoryIsRequiredBeforeCreatingEvenTheRequestedInstance()
    {
        WinAppSandboxProtocol.RequireEmptyInventory(Inventory());
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireEmptyInventory(Inventory(Instance)));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireEmptyInventory(Inventory(UnrelatedInstance)));
    }

    [TestMethod]
    public void ExistingTargetMustBeExactlyTheRunOwnedProviderInstance()
    {
        WinAppSandboxProtocol.RequireExclusiveInstance(Inventory(Instance), Instance);
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireExclusiveInstance(Inventory(), Instance));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireExclusiveInstance(Inventory(UnrelatedInstance), Instance));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireExclusiveInstance(Inventory(Instance, UnrelatedInstance), Instance));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireExclusiveInstance(Inventory(Instance, Instance), Instance));
    }

    [TestMethod]
    public void FailedInventoryQueryAbortsWithoutClaimingOwnershipChanged()
    {
        var unconfirmed = false;
        var error = Assert.ThrowsExactly<WinAppSandboxException>(() =>
            WinAppSandboxProtocol.RequireExclusiveInstance(
                () => throw new WinAppSandboxException("command_timeout"),
                Instance,
                () => unconfirmed = true));
        Assert.AreEqual("command_timeout", error.Code);
        Assert.IsFalse(unconfirmed, "An unavailable provider must not become evidence of a different instance.");

        WinAppSandboxProtocol.RequireExclusiveInstance(() => Inventory(Instance), Instance, () => unconfirmed = true);
        Assert.IsFalse(unconfirmed);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ContradictoryOrMalformedInventoryRequiresPreservingOwnershipEvidence(bool malformed)
    {
        var unconfirmed = false;
        Assert.ThrowsExactly<WinAppSandboxException>(() =>
            WinAppSandboxProtocol.RequireExclusiveInstance(
                () => malformed ? "{}" : Inventory(UnrelatedInstance),
                Instance,
                () => unconfirmed = true));
        Assert.IsTrue(unconfirmed);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("{\"WindowsSandboxEnvironments\":null}")]
    [DataRow("{\"WindowsSandboxEnvironments\":{}}")]
    [DataRow("{\"WindowsSandboxEnvironments\":[{}]}")]
    [DataRow("{\"WindowsSandboxEnvironments\":[{\"Id\":\"not-a-guid\"}]}")]
    [DataRow("{\"WindowsSandboxEnvironments\":[{\"Id\":\"00000000-0000-0000-0000-000000000000\"}]}")]
    [DataRow("{\"WindowsSandboxEnvironments\":[],\"WindowsSandboxEnvironments\":[]}")]
    public void MalformedProviderInventoryIsNeverTreatedAsEmpty(string json)
    {
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireEmptyInventory(json));
    }

    [TestMethod]
    public void StartRequiresTheRequestedIdInEitherDocumentedProviderShape()
    {
        WinAppSandboxProtocol.RequireStartResult(new JsonObject { ["Id"] = Instance.ToString("D") }.ToJsonString(), Instance);
        WinAppSandboxProtocol.RequireStartResult(Inventory(Instance), Instance);
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireStartResult("{}", Instance));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireStartResult(Inventory(UnrelatedInstance), Instance));
        var ambiguous = JsonNode.Parse(Inventory(UnrelatedInstance))!.AsObject();
        ambiguous["Id"] = Instance.ToString("D");
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireStartResult(ambiguous.ToJsonString(), Instance));
    }

    [TestMethod]
    public void OwnershipRequiresOneExplicitInstanceIdAndAnOpaqueStableEpoch()
    {
        Assert.AreEqual(Epoch, WinAppSandboxProtocol.RequireAdoptedState([State().ToJsonString()], Instance));
        Assert.AreEqual(Epoch, WinAppSandboxProtocol.RequireAdoptedState([State().ToJsonString()], Instance, Epoch));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([], Instance));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([State().ToJsonString(), State().ToJsonString()], Instance));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([State().ToJsonString()], Instance, "old-epoch"));
        var state = State();
        state.Remove("instanceId");
        state["bootstrappedEpoch"] = Instance + ":this-is-not-an-ownership-proof";
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([state.ToJsonString()], Instance));
    }

    [DataTestMethod]
    [DataRow("instanceId", "638638e2-b7c3-4f31-b779-cb7715cb0145")]
    [DataRow("instanceOrigin", "Created")]
    [DataRow("instanceOrigin", "RecoveredStart")]
    [DataRow("targetKind", "local")]
    [DataRow("targetId", "another-target")]
    [DataRow("bootstrappedEpoch", "")]
    [DataRow("pendingInstanceId", "d2b96d2f-5b49-42d1-9576-227250294fb0")]
    public void UnrelatedUnbootstrappedAndAutoCreatedTargetsAreRefused(string property, string value)
    {
        var state = State();
        state[property] = value;
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([state.ToJsonString()], Instance));
    }

    [TestMethod]
    public void UnknownOrMalformedOwnershipSchemaFailsClosed()
    {
        var state = State();
        state["schemaVersion"] = 2;
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([state.ToJsonString()], Instance));
        state["schemaVersion"] = "1";
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireAdoptedState([state.ToJsonString()], Instance));
    }

    [TestMethod]
    public void PreviewSchemaMustIncludeTargetExecutionTransfersAndNativeMedia()
    {
        var schema = Schema();
        WinAppSandboxProtocol.RequireCapabilities(schema.ToJsonString());
        foreach (var name in new[] { "exec", "push", "pull", "snapshot", "screenshot", "record" })
        {
            var missing = Schema();
            missing["subcommands"]!["target"]!["subcommands"]!.AsObject().Remove(name);
            Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireCapabilities(missing.ToJsonString()));
        }

        schema["subcommands"]!["target"]!["subcommands"]!["record"]!["options"]!.AsObject().Remove("--max-edge");
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireCapabilities(schema.ToJsonString()));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireCapabilities("{\"subcommands\":{\"target\":null}}"));
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.RequireCapabilities("{}"));
    }

    [TestMethod]
    public void SnapshotIsPassiveEvidenceNotAnAlternativeOwnershipProof()
    {
        var snapshot = Snapshot();
        var safe = WinAppSandboxProtocol.ReadSafeSnapshot(snapshot.ToJsonString(), Epoch);
        Assert.AreEqual(1234L, safe["ViewerHwnd"]!.GetValue<long>());
        Assert.IsFalse(safe.ToJsonString().Contains("secret", StringComparison.Ordinal));
        Assert.IsNull(safe["executionTarget"]);
        snapshot["executionTarget"]!["epoch"] = "other-epoch";
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.ReadSafeSnapshot(snapshot.ToJsonString(), Epoch));
        snapshot = Snapshot();
        snapshot["attached"] = false;
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.ReadSafeSnapshot(snapshot.ToJsonString(), Epoch));
        snapshot = Snapshot();
        snapshot["capabilities"]!["supportsRealInput"] = false;
        Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxProtocol.ReadSafeSnapshot(snapshot.ToJsonString(), Epoch));
    }

    [TestMethod]
    public void SnapshotWithoutAViewerDoesNotInventAHostWindow()
    {
        var snapshot = Snapshot();
        snapshot.Remove("desktop");
        Assert.AreEqual(0L, WinAppSandboxProtocol.ReadSafeSnapshot(snapshot.ToJsonString(), Epoch)["ViewerHwnd"]!.GetValue<long>());
    }

    [TestMethod]
    public void StructuredErrorsExportOnlyAllowlistedCodesNeverMessagesOrTokens()
    {
        Assert.AreEqual("sandbox_unsupported", WinAppSandboxProtocol.SafeErrorCode(
            """{"error":{"code":"sandbox_unsupported","message":"secret","context":{"psk":"secret"}}}"""));
        Assert.AreEqual("command_failed", WinAppSandboxProtocol.SafeErrorCode("""{"error":{"code":"secret"}}"""));
        Assert.AreEqual("command_failed", WinAppSandboxProtocol.SafeErrorCode("worker wrote a secret"));
        Assert.AreEqual("command_failed", WinAppSandboxProtocol.SafeErrorCode("""{"error":"secret"}"""));
        Assert.AreEqual("command_failed", WinAppSandboxProtocol.SafeErrorCode(
            """{"error":{"code":"sandbox_unsupported","code":"secret"}}"""));
    }

    [TestMethod]
    public void RecordingReadinessRequiresTheStructuredStartedEvent()
    {
        Assert.IsTrue(WinAppSandboxProtocol.RecordingStarted("""{"event":"recording-started","path":"private"}""" + "\r\n{\n"));
        Assert.IsFalse(WinAppSandboxProtocol.RecordingStarted("recording-started\n"));
        Assert.IsFalse(WinAppSandboxProtocol.RecordingStarted("""{"message":"recording-started"}"""));
        Assert.IsFalse(WinAppSandboxProtocol.RecordingStarted("""{"event":"recording-st"""));
    }

    private static string Inventory(params Guid[] ids) => new JsonObject
    {
        ["WindowsSandboxEnvironments"] = new JsonArray(ids.Select(id => (JsonNode)new JsonObject { ["Id"] = id.ToString("D") }).ToArray()),
    }.ToJsonString();

    private static JsonObject State() => new()
    {
        ["schemaVersion"] = 1,
        ["targetKind"] = "sandbox",
        ["targetId"] = "default",
        ["instanceId"] = Instance.ToString("D"),
        ["instanceOrigin"] = "Adopted",
        ["bootstrappedEpoch"] = Epoch,
    };

    private static JsonObject Schema()
    {
        var commands = new JsonObject();
        foreach (var name in new[] { "exec", "push", "pull", "snapshot", "screenshot", "record" })
        {
            commands[name] = new JsonObject
            {
                ["arguments"] = new JsonObject { ["target"] = new JsonObject() },
                ["options"] = new JsonObject
                {
                    ["--json"] = new JsonObject(),
                    ["--output"] = new JsonObject(),
                    ["--fps"] = new JsonObject(),
                    ["--max-edge"] = new JsonObject(),
                    ["--duration-sec"] = new JsonObject(),
                },
            };
        }

        return new JsonObject { ["subcommands"] = new JsonObject { ["target"] = new JsonObject { ["subcommands"] = commands } } };
    }

    private static JsonObject Snapshot() => new()
    {
        ["executionTarget"] = new JsonObject { ["kind"] = "sandbox", ["id"] = "default", ["epoch"] = Epoch },
        ["running"] = true,
        ["attached"] = true,
        ["capabilities"] = new JsonObject
        {
            ["supportsInteractiveDesktop"] = true,
            ["supportsRealInput"] = true,
            ["supportsScreenCapture"] = true,
        },
        ["desktop"] = new JsonObject { ["rendered"] = true, ["windowHandle"] = 1234, ["effectiveCaptureReady"] = true },
        ["windows"] = new JsonArray(new JsonObject { ["title"] = "secret" }),
        ["psk"] = "secret",
    };
}
