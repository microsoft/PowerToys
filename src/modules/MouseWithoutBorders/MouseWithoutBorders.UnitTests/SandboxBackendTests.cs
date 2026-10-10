// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SandboxBackendTests
{
    private static readonly string[] LegacyMappedPaths = [@"C:\MwbArchive", @"C:\MwbPayload", @"C:\MwbTools", @"C:\MwbInput", @"C:\MwbOutput"];

    [DataTestMethod]
    [DataRow(null, 19045, "Legacy")]
    [DataRow(null, 26200, "Legacy")]
    [DataRow("Legacy", 19045, "Legacy")]
    [DataRow("Auto", 19045, "Legacy")]
    [DataRow("Auto", 26200, "WinApp")]
    [DataRow("WinApp", 26100, "WinApp")]
    public void BackendSelectionPreservesLegacyAndRequiresExplicitModernProvisioning(string? requested, int build, string expected)
    {
        Assert.AreEqual(expected, SandboxBackendSelection.Resolve(requested, build));
    }

    [TestMethod]
    public void ModernBackendDoesNotSilentlyFallBackOnWin10()
    {
        Assert.ThrowsExactly<PlatformNotSupportedException>(() => SandboxBackendSelection.Resolve("WinApp", 19045));
        Assert.ThrowsExactly<ArgumentException>(() => SandboxBackendSelection.Resolve("unknown", 26200));
        Assert.ThrowsExactly<ArgumentException>(() => SandboxBackendSelection.Resolve(string.Empty, 26200));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void BothBackendsKeepClipboardAndDeviceIsolation(bool legacy)
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            var channel = new EndpointChannel("fixture", Path.Combine(root, "input"), Path.Combine(root, "output"));
            var document = SandboxConfiguration.Create(
                Path.Combine(root, "archive & files", "product.zip"),
                Path.Combine(root, "payload"),
                Path.Combine(root, "tools"),
                channel,
                legacy);
            var configuration = document.Root!;
            foreach (var option in new[] { "ClipboardRedirection", "AudioInput", "VideoInput", "PrinterRedirection", "VGpu" })
            {
                Assert.AreEqual("Disable", configuration.Element(option)!.Value);
            }

            Assert.AreEqual("Enable", configuration.Element("Networking")!.Value);
            Assert.AreEqual("4096", configuration.Element("MemoryInMB")!.Value);
            var mappings = configuration.Element("MappedFolders")!.Elements("MappedFolder").ToArray();
            Assert.AreEqual(legacy ? 5 : 0, mappings.Length);
            var channels = SandboxConfiguration.EndpointMappings(channel);
            Assert.AreEqual(2, channels.Length);
            Assert.AreEqual((channel.InputRoot, @"C:\MwbInput", true), channels[0]);
            Assert.AreEqual((channel.OutputRoot, @"C:\MwbOutput", false), channels[1]);
            Assert.AreEqual(legacy, configuration.Element("LogonCommand") is not null);
            if (legacy)
            {
                var input = mappings.Single(item => item.Element("SandboxFolder")!.Value == @"C:\MwbInput");
                Assert.AreEqual(channel.InputRoot, input.Element("HostFolder")!.Value);
                Assert.AreEqual("true", input.Element("ReadOnly")!.Value);
                var output = mappings.Single(item => item.Element("SandboxFolder")!.Value == @"C:\MwbOutput");
                Assert.AreEqual(channel.OutputRoot, output.Element("HostFolder")!.Value);
                Assert.AreEqual("false", output.Element("ReadOnly")!.Value);
                CollectionAssert.AreEqual(
                    LegacyMappedPaths,
                    mappings.Select(item => item.Element("SandboxFolder")!.Value).ToArray());
                StringAssert.Contains(configuration.Element("LogonCommand")!.Value, "EndpointWorker.ps1");
                Assert.IsTrue(mappings.Where(item => item != output).All(item => item.Element("ReadOnly")!.Value == "true"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
