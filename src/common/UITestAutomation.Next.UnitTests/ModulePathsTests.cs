// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class ModulePathsTests
{
    [TestMethod]
    public void LooseCommandPaletteDoesNotQueryRegisteredPackages()
    {
        WithExecutablePaths((loose, packaged) =>
        {
            File.WriteAllText(loose, "fixture");
            Assert.AreEqual(
                loose,
                ModulePaths.ResolveInstalledExecutablePath(
                    PowerToysModule.CommandPalette, loose, () => throw new AssertFailedException("Unexpected package query.")));
        });
    }

    [TestMethod]
    public void MissingLooseCommandPaletteUsesRegisteredExecutable()
    {
        WithExecutablePaths((loose, packaged) =>
            Assert.AreEqual(
                packaged,
                ModulePaths.ResolveInstalledExecutablePath(PowerToysModule.CommandPalette, loose, () => packaged)));
    }

    [TestMethod]
    [DataRow(PowerToysModule.Runner)]
    [DataRow(PowerToysModule.PowerToysSettings)]
    public void OtherModulesNeverUseCommandPalettePackage(PowerToysModule module)
    {
        WithExecutablePaths((loose, packaged) =>
            Assert.AreEqual(
                loose,
                ModulePaths.ResolveInstalledExecutablePath(
                    module, loose, () => throw new AssertFailedException("Unexpected package query."))));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingRegisteredExecutableFailsExplicitly(bool hasRegisteredPath)
    {
        WithExecutablePaths((loose, packaged) =>
        {
            File.Delete(packaged);
            var failure = Assert.ThrowsExactly<FileNotFoundException>(
                () => ModulePaths.ResolveInstalledExecutablePath(
                    PowerToysModule.CommandPalette, loose, () => hasRegisteredPath ? packaged : null));
            Assert.AreEqual(loose, failure.FileName);
        });
    }

    private static void WithExecutablePaths(Action<string, string> assertion)
    {
        var root = Path.Combine(Path.GetTempPath(), $"ModulePaths-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var loose = Path.Combine(root, "loose.exe");
            var packaged = Path.Combine(root, "packaged.exe");
            File.WriteAllText(packaged, "fixture");
            assertion(loose, packaged);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
