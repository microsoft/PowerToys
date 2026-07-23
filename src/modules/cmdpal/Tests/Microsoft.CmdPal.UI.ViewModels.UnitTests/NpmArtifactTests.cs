// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class NpmArtifactTests
{
    private const string ValidIntegrity = "sha512-abc123==";

    [TestMethod]
    public void TryCreate_UnscopedPackage_Succeeds()
    {
        var ok = NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, null, out var artifact, out var error);

        Assert.IsTrue(ok);
        Assert.IsNotNull(artifact);
        Assert.AreEqual(NpmArtifactValidationError.None, error);
        Assert.AreEqual("left-pad", artifact!.Package);
        Assert.AreEqual("1.3.0", artifact.Version);
        Assert.AreEqual(ValidIntegrity, artifact.Integrity);
        Assert.IsNull(artifact.Registry);
        Assert.AreEqual("left-pad@1.3.0", artifact.InstallSpec);
    }

    [TestMethod]
    public void TryCreate_ScopedPackage_Succeeds()
    {
        var ok = NpmArtifact.TryCreate("@contoso/sample", "2.0.1", ValidIntegrity, null, out var artifact, out var error);

        Assert.IsTrue(ok);
        Assert.AreEqual(NpmArtifactValidationError.None, error);
        Assert.AreEqual("@contoso/sample@2.0.1", artifact!.InstallSpec);
    }

    [TestMethod]
    public void TryCreate_ApprovedRegistry_Succeeds()
    {
        var ok = NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, "https://registry.npmjs.org/", out var artifact, out var error);

        Assert.IsTrue(ok);
        Assert.AreEqual(NpmArtifactValidationError.None, error);
        Assert.AreEqual("https://registry.npmjs.org/", artifact!.Registry);
    }

    [TestMethod]
    public void TryCreate_PrereleaseAndBuildMetadata_Succeeds()
    {
        var ok = NpmArtifact.TryCreate("left-pad", "1.3.0-beta.1+build.5", ValidIntegrity, null, out var artifact, out var error);

        Assert.IsTrue(ok);
        Assert.AreEqual(NpmArtifactValidationError.None, error);
        Assert.AreEqual("1.3.0-beta.1+build.5", artifact!.Version);
    }

    [TestMethod]
    public void TryCreate_MissingPackage_FailsClosed()
    {
        var ok = NpmArtifact.TryCreate("   ", "1.3.0", ValidIntegrity, null, out var artifact, out var error);

        Assert.IsFalse(ok);
        Assert.IsNull(artifact);
        Assert.AreEqual(NpmArtifactValidationError.PackageMissing, error);
    }

    [DataTestMethod]
    [DataRow("-flag")]
    [DataRow("../escape")]
    [DataRow("has space")]
    [DataRow("https://evil.example/pkg.tgz")]
    [DataRow("git+https://example.com/x.git")]
    [DataRow("./local/path")]
    [DataRow("@scope")]
    public void TryCreate_InvalidPackage_FailsClosed(string package)
    {
        var ok = NpmArtifact.TryCreate(package, "1.3.0", ValidIntegrity, null, out var artifact, out var error);

        Assert.IsFalse(ok);
        Assert.IsNull(artifact);
        Assert.AreEqual(NpmArtifactValidationError.PackageInvalid, error);
    }

    [TestMethod]
    public void TryCreate_MissingVersion_FailsClosed()
    {
        var ok = NpmArtifact.TryCreate("left-pad", null, ValidIntegrity, null, out _, out var error);

        Assert.IsFalse(ok);
        Assert.AreEqual(NpmArtifactValidationError.VersionMissing, error);
    }

    [DataTestMethod]
    [DataRow("latest")]
    [DataRow("^1.3.0")]
    [DataRow("~1.3.0")]
    [DataRow(">=1.0.0")]
    [DataRow("1.x")]
    [DataRow("1.2")]
    [DataRow("*")]
    [DataRow("1.2.3 || 2.0.0")]
    public void TryCreate_RangeOrDistTagVersion_FailsClosed(string version)
    {
        var ok = NpmArtifact.TryCreate("left-pad", version, ValidIntegrity, null, out _, out var error);

        Assert.IsFalse(ok);
        Assert.AreEqual(NpmArtifactValidationError.VersionInvalid, error);
    }

    [TestMethod]
    public void TryCreate_MissingIntegrity_FailsClosed()
    {
        var ok = NpmArtifact.TryCreate("left-pad", "1.3.0", string.Empty, null, out _, out var error);

        Assert.IsFalse(ok);
        Assert.AreEqual(NpmArtifactValidationError.IntegrityMissing, error);
    }

    [DataTestMethod]
    [DataRow("sha1-abc123==")]
    [DataRow("sha256-abc123==")]
    [DataRow("abc123")]
    [DataRow("sha512-")]
    public void TryCreate_UnsupportedIntegrity_FailsClosed(string integrity)
    {
        var ok = NpmArtifact.TryCreate("left-pad", "1.3.0", integrity, null, out _, out var error);

        Assert.IsFalse(ok);
        Assert.AreEqual(NpmArtifactValidationError.IntegrityInvalid, error);
    }

    [DataTestMethod]
    [DataRow("http://registry.npmjs.org/")]
    [DataRow("https://evil.example.com/")]
    [DataRow("ftp://registry.npmjs.org/")]
    [DataRow("registry.npmjs.org")]
    [DataRow("not a url")]
    public void TryCreate_UnapprovedRegistry_FailsClosed(string registry)
    {
        var ok = NpmArtifact.TryCreate("left-pad", "1.3.0", ValidIntegrity, registry, out _, out var error);

        Assert.IsFalse(ok);
        Assert.AreEqual(NpmArtifactValidationError.RegistryInvalid, error);
    }
}
