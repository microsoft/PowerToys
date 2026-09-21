// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class WinAppSandboxPayloadTests
{
    [TestMethod]
    public void SingleBootstrapPayloadPreservesInputsAndExcludesToolSymbols()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-bundled-payload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var payload = Directory.CreateDirectory(Path.Combine(root, "payload")).FullName;
            var tools = Directory.CreateDirectory(Path.Combine(root, "tools")).FullName;
            var resource = Directory.CreateDirectory(Path.Combine(tools, "en-US")).FullName;
            File.WriteAllText(Path.Combine(payload, "EndpointWorker.ps1"), "worker");
            File.WriteAllText(Path.Combine(payload, "NativeSupport.cs"), "native-support");
            File.WriteAllText(Path.Combine(tools, "winapp.exe"), "stable-ui-cli");
            File.WriteAllText(Path.Combine(tools, "libSkiaSharp.dll"), "native-library");
            File.WriteAllText(Path.Combine(tools, "winapp.pdb"), "symbols");
            File.WriteAllText(Path.Combine(resource, "resources.mui"), "localized-resource");
            var archive = Path.Combine(root, "runtime.zip");
            File.WriteAllBytes(archive, [1, 2, 3, 4]);

            var bundle = WinAppSandboxPayload.Create(archive, payload, tools, Path.Combine(root, "bundle"));

            Assert.AreEqual("worker", File.ReadAllText(Path.Combine(bundle, "Payload", "EndpointWorker.ps1")));
            Assert.AreEqual("native-support", File.ReadAllText(Path.Combine(bundle, "Payload", "NativeSupport.cs")));
            Assert.AreEqual("stable-ui-cli", File.ReadAllText(Path.Combine(bundle, "Tools", "winapp.exe")));
            Assert.AreEqual("native-library", File.ReadAllText(Path.Combine(bundle, "Tools", "libSkiaSharp.dll")));
            Assert.AreEqual("localized-resource", File.ReadAllText(Path.Combine(bundle, "Tools", "en-US", "resources.mui")));
            Assert.IsFalse(File.Exists(Path.Combine(bundle, "Tools", "winapp.pdb")));
            CollectionAssert.AreEqual(File.ReadAllBytes(archive), File.ReadAllBytes(Path.Combine(bundle, "Runtime", "product.zip")));
            Assert.IsTrue(File.Exists(Path.Combine(tools, "winapp.pdb")), "Source inputs must not be changed.");

            var error = Assert.ThrowsExactly<WinAppSandboxException>(() => WinAppSandboxPayload.Create(archive, payload, tools, bundle));
            Assert.AreEqual("guest_payload_preexisting", error.Code);
            Assert.AreEqual("worker", File.ReadAllText(Path.Combine(bundle, "Payload", "EndpointWorker.ps1")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FailedBootstrapStagingRemovesOnlyItsNewPartialBundle()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-payload-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var payload = Directory.CreateDirectory(Path.Combine(root, "payload")).FullName;
            var tools = Directory.CreateDirectory(Path.Combine(root, "tools")).FullName;
            File.WriteAllText(Path.Combine(payload, "EndpointWorker.ps1"), "worker");
            File.WriteAllText(Path.Combine(tools, "winapp.exe"), "stable-ui-cli");
            var bundle = Path.Combine(root, "bundle");
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                WinAppSandboxPayload.Create(Path.Combine(root, "missing.zip"), payload, tools, bundle));
            Assert.IsFalse(Directory.Exists(bundle));
            Assert.IsTrue(File.Exists(Path.Combine(payload, "EndpointWorker.ps1")));
            Assert.IsTrue(File.Exists(Path.Combine(tools, "winapp.exe")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
