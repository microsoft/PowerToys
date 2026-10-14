// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Workspaces.UITests;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class PackagedFixturePrerequisitesTests
{
    [TestMethod]
    public void UnsignedArchiveIsSkippedOnlyLocally()
    {
        WithArchive(hasSignature: false, path =>
        {
            var skipped = Assert.ThrowsExactly<AssertInconclusiveException>(
                () => PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: false));
            StringAssert.Contains(skipped.Message, path);
            StringAssert.Contains(skipped.Message, "doc\\devdocs\\modules\\workspaces.md");
            Assert.ThrowsExactly<AssertFailedException>(
                () => PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: true));
        });
    }

    [TestMethod]
    public void SignatureMarkerDefersIntegrityAndTrustValidationToWindows()
    {
        WithArchive(hasSignature: true, path =>
        {
            PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: false);
            PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: true);
        });
    }

    [TestMethod]
    [DataRow(PackagedFixturePrerequisites.NoSignature)]
    [DataRow(PackagedFixturePrerequisites.UntrustedRoot)]
    public void MissingSigningIsInconclusiveLocallyButFailsInPipeline(int errorCode)
    {
        var local = PackagedFixturePrerequisites.MissingSigningException("fixture.msix", errorCode, isInPipeline: false);
        var pipeline = PackagedFixturePrerequisites.MissingSigningException("fixture.msix", errorCode, isInPipeline: true);

        Assert.IsInstanceOfType<AssertInconclusiveException>(local);
        Assert.IsInstanceOfType<AssertFailedException>(pipeline);
    }

    [TestMethod]
    [DataRow(unchecked((int)0x80096010))]
    [DataRow(unchecked((int)0x80070005))]
    [DataRow(unchecked((int)0x80073CF6))]
    public void CorruptSignaturesAndOtherDeploymentFailuresAreNotSetupSkips(int errorCode)
    {
        Assert.IsFalse(PackagedFixturePrerequisites.IsMissingSigning(errorCode));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => PackagedFixturePrerequisites.MissingSigningException("fixture.msix", errorCode, isInPipeline: false));
    }

    [TestMethod]
    public void MissingPackageIsAlwaysABuildFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Workspaces-absent-{Guid.NewGuid():N}.msix");
        Assert.ThrowsExactly<AssertFailedException>(
            () => PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: false));
        Assert.ThrowsExactly<AssertFailedException>(
            () => PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: true));
    }

    [TestMethod]
    public void MalformedArchiveIsNeverSkipped()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not an MSIX archive");
            Assert.ThrowsExactly<InvalidDataException>(
                () => PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: false));
            Assert.ThrowsExactly<InvalidDataException>(
                () => PackagedFixturePrerequisites.RequireSignature(path, isInPipeline: true));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WithArchive(bool hasSignature, Action<string> assertion)
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var file = File.Open(path, FileMode.Create))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                archive.CreateEntry("AppxManifest.xml");
                if (hasSignature)
                {
                    archive.CreateEntry("AppxSignature.p7x");
                }
            }

            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
