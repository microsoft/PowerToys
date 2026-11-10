// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.AgentSkills;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class AgentSkillBundleTests
{
    private string root = null!;
    private string applicationDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(AppContext.BaseDirectory, "agent-skill-bundle-tests-" + Guid.NewGuid().ToString("N"));
        applicationDirectory = Directory.CreateDirectory(Path.Combine(root, "WinUI3Apps")).FullName;
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, recursive: true);

    [DataTestMethod]
    [DataRow(@"WinUI3Apps\PowerScriptsAgentSkill")]
    [DataRow(@"PowerScripts\AgentSkill")]
    public void FindsTheBundledSkillInDevelopmentAndInstalledLayouts(string relativeDirectory)
    {
        var bundle = CreateBundle(relativeDirectory);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);

        Assert.AreEqual(bundle, AgentSkillBundle.GetDirectory(applicationDirectory));
        CollectionAssert.AreEquivalent(files, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void AnIncompleteDevelopmentDirectoryDoesNotMaskTheInstalledBundle()
    {
        Directory.CreateDirectory(Path.Combine(applicationDirectory, "PowerScriptsAgentSkill"));
        var bundle = CreateBundle(@"PowerScripts\AgentSkill");

        Assert.AreEqual(bundle, AgentSkillBundle.GetDirectory(applicationDirectory));
    }

    [TestMethod]
    public void MissingSkillReportsAnErrorWithoutCreatingAnInstallation()
    {
        var exception = Assert.ThrowsException<FileNotFoundException>(() => AgentSkillBundle.GetDirectory(applicationDirectory));

        StringAssert.EndsWith(exception.FileName!, Path.Combine("PowerScripts", "AgentSkill", "SKILL.md"));
        Assert.AreEqual(0, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        Assert.AreEqual(1, Directory.GetDirectories(root, "*", SearchOption.AllDirectories).Length);
    }

    private string CreateBundle(string relativeDirectory)
    {
        var bundle = Directory.CreateDirectory(Path.Combine(root, relativeDirectory)).FullName;
        File.WriteAllText(Path.Combine(bundle, "SKILL.md"), "# PowerScripts");
        return bundle;
    }
}
