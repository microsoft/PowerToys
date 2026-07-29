// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies that the host launch goes through the Phase 1 SDK bootstrap (r3-p4-07). The
/// bootstrap claims and guards stdout before it dynamically imports the extension entry, so
/// static top-level stdout writes cannot corrupt the JSON-RPC framing. The launcher must
/// resolve the bootstrap relative to the extension's installed SDK under
/// <c>node_modules/@microsoft/cmdpal-sdk</c>, preferring the package's declared
/// <c>bin</c> entry, and must return null (falling back to a direct entry launch) when the
/// SDK or its bootstrap is absent.
/// </summary>
[TestClass]
public class JSExtensionWrapperBootstrapTests
{
    private static string CreateSdkRoot(string manifestDir)
    {
        var sdkRoot = Path.Combine(manifestDir, "node_modules", "@microsoft", "cmdpal-sdk");
        Directory.CreateDirectory(sdkRoot);
        return sdkRoot;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cmdpal-bootstrap-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [TestMethod]
    public void ResolveBootstrapScript_PrefersPackageJsonBinEntry()
    {
        var manifestDir = NewTempDir();
        try
        {
            var sdkRoot = CreateSdkRoot(manifestDir);
            var binTarget = Path.Combine(sdkRoot, "dist", "runtime", "bootstrap.js");
            Directory.CreateDirectory(Path.GetDirectoryName(binTarget)!);
            File.WriteAllText(binTarget, "// bootstrap");
            File.WriteAllText(
                Path.Combine(sdkRoot, "package.json"),
                "{ \"name\": \"@microsoft/cmdpal-sdk\", \"bin\": { \"cmdpal-bootstrap\": \"./dist/runtime/bootstrap.js\" } }");

            var resolved = JSExtensionWrapper.ResolveBootstrapScript(manifestDir);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(Path.GetFullPath(binTarget), Path.GetFullPath(resolved!));
        }
        finally
        {
            Directory.Delete(manifestDir, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveBootstrapScript_FallsBackToKnownArtifact_WhenNoBinEntry()
    {
        var manifestDir = NewTempDir();
        try
        {
            var sdkRoot = CreateSdkRoot(manifestDir);
            File.WriteAllText(Path.Combine(sdkRoot, "package.json"), "{ \"name\": \"@microsoft/cmdpal-sdk\" }");

            var fallback = Path.Combine(sdkRoot, "dist", "runtime", "bootstrap.js");
            Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
            File.WriteAllText(fallback, "// bootstrap");

            var resolved = JSExtensionWrapper.ResolveBootstrapScript(manifestDir);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(Path.GetFullPath(fallback), Path.GetFullPath(resolved!));
        }
        finally
        {
            Directory.Delete(manifestDir, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveBootstrapScript_ReturnsNull_WhenSdkMissing()
    {
        var manifestDir = NewTempDir();
        try
        {
            Assert.IsNull(JSExtensionWrapper.ResolveBootstrapScript(manifestDir));
        }
        finally
        {
            Directory.Delete(manifestDir, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveBootstrapScript_ReturnsNull_WhenBootstrapArtifactMissing()
    {
        var manifestDir = NewTempDir();
        try
        {
            // The SDK is installed but neither the bin entry target nor the known artifacts
            // exist on disk, so no bootstrap can be launched.
            var sdkRoot = CreateSdkRoot(manifestDir);
            File.WriteAllText(
                Path.Combine(sdkRoot, "package.json"),
                "{ \"name\": \"@microsoft/cmdpal-sdk\", \"bin\": { \"cmdpal-bootstrap\": \"./dist/runtime/bootstrap.js\" } }");

            Assert.IsNull(JSExtensionWrapper.ResolveBootstrapScript(manifestDir));
        }
        finally
        {
            Directory.Delete(manifestDir, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveBootstrapScript_ReturnsNull_ForEmptyManifestDirectory()
    {
        Assert.IsNull(JSExtensionWrapper.ResolveBootstrapScript(string.Empty));
    }
}
