// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.ProtectedStorage;

namespace ProtectedStorage.Client.Managed.UnitTests;

[TestClass]
public sealed class MaintenanceTests
{
    [TestMethod]
    public void UnsignedNativeTrustFailurePreservesTheProcessExitBits()
    {
        const uint nativeCode = 0x800b0100;
        const string output = """
            {"operationId":"00112233445566778899AABBCCDDEEFF","state":"Failed","nativeCode":2148204800,"retryKind":"RetryAfterInputFix","cleanupPending":false,"dataRetained":true}
            """;
        var result = MaintenanceResult.Parse(unchecked((int)nativeCode), output);
        Assert.AreEqual(nativeCode, result.NativeCode);
        var error = Assert.ThrowsExactly<ProtectedStorageException>(result.ThrowIfFailed);
        Assert.AreEqual(unchecked((int)nativeCode), error.NativeCode);
    }

    [TestMethod]
    public void SetupIsResolvedBesideMainPowerToysNotTheEditorSubfolder()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), $"setup-path-{Guid.NewGuid():N}");
        string editor = Path.Combine(root, "Editor");
        Directory.CreateDirectory(editor);
        try
        {
            File.WriteAllText(Path.Combine(root, "PowerToys.exe"), string.Empty);
            string expected = Path.Combine(root, "PowerToys.ProtectedStorageSetup.exe");
            File.WriteAllText(expected, string.Empty);
            File.WriteAllText(Path.Combine(editor, "PowerToys.ProtectedStorageSetup.exe"), string.Empty);
            Assert.AreEqual(expected, ProtectedStorageSetupClient.FindSetupPath(editor));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void NativeRebootResultsRemainSuccessfulButRequireReadinessAction()
    {
        foreach (uint code in new uint[] { 3010, 1641 })
        {
            var result = new MaintenanceResult(Guid.NewGuid(), "Ready", code, "RetryAfterRestart", true, true);
            Assert.IsTrue(result.Succeeded);
            result.ThrowIfFailed();
            Assert.IsTrue(result.RequiresRestart);
            var error = Assert.ThrowsExactly<ProtectedStorageException>(result.ThrowIfRestartRequired);
            Assert.AreEqual("SetupRestartRequired", error.ErrorCode);
            Assert.AreEqual(unchecked((int)code), error.NativeCode);
        }
    }

    [TestMethod]
    public void SetupResultParsesOnlyTheFinalJsonObservation()
    {
        const string output = """
            diagnostic output
            {"operationId":"00112233445566778899AABBCCDDEEFF","state":"NotProvisioned","nativeCode":0,"retryKind":"None","cleanupPending":false,"dataRetained":true,"productCode":"","installedVersion":""}
            """;
        var result = MaintenanceResult.Parse(0, output);
        Assert.AreEqual("NotProvisioned", result.State);
        Assert.AreEqual(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"), result.OperationId);
        Assert.AreEqual(string.Empty, result.InstalledVersion);
        Assert.IsTrue(result.DataRetained);
        Assert.ThrowsExactly<ProtectedStorageException>(() => MaintenanceResult.Parse(5, output));
        Assert.ThrowsExactly<ProtectedStorageException>(() => MaintenanceResult.Parse(0, output.Replace("00112233445566778899AABBCCDDEEFF", "00112233-4455-6677-8899-aabbccddeeff", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void UnknownMaintenanceOutcomeIsNeverClassifiedAsOrdinaryRetry()
    {
        var result = new MaintenanceResult(Guid.NewGuid(), "Failed", 1603, "InspectUnknownOutcome", false, false);
        var error = Assert.ThrowsExactly<ProtectedStorageException>(result.ThrowIfFailed);
        Assert.AreEqual("SetupOutcomeUnknown", error.ErrorCode);
        Assert.AreEqual(result.OperationId, error.OperationId);
    }

    [TestMethod]
    public void CapabilitiesRequireSupportedProtocolAndStorageSemantics()
    {
        var capabilities = new StorageCapabilities(1, 0, PipeProtocol.MaximumBlob, PipeProtocol.MaximumMetadata, ProtectedStoreClient.ReleaseVersion, false, false, new HashSet<string> { "cas" });
        Assert.ThrowsExactly<ProtectedStorageException>(() => capabilities.RequireReady(1024, ["cas", "write-query"]));
        Assert.ThrowsExactly<ProtectedStorageException>(() => (capabilities with { Maintenance = true }).RequireReady(1024, ["cas"]));
        (capabilities with { Release = new Version(99, 0, 0, 0) }).RequireReady(1024, ["cas"]);
        Assert.ThrowsExactly<ArgumentException>(() => new ProtectedStorageSetupClient().RetryFailedOperationAsync(Guid.Empty));
    }
}
