// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.AgentSkills;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class AgentSkillManagerTests
{
    private string root = null!;
    private string bundle = null!;
    private string profile = null!;
    private string statePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        // Keep isolated fixtures under the project rather than using a machine-global temp directory.
        root = Path.Combine(AppContext.BaseDirectory, "agent-skill-tests-" + Guid.NewGuid().ToString("N"));
        bundle = Path.Combine(root, "bundle");
        profile = Path.Combine(root, "home");
        statePath = Path.Combine(root, "settings", "agent-skills.json");
        Directory.CreateDirectory(Path.Combine(bundle, "references", "empty"));
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(bundle, "SKILL.md"), "# PowerScripts skill");
        File.WriteAllText(Path.Combine(bundle, "references", "guide.md"), "Portable reference");
        SetBundleVersion("1.0.0");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(AgentSkillTarget.Copilot, ".copilot")]
    [DataRow(AgentSkillTarget.ClaudeCode, ".claude")]
    [DataRow(AgentSkillTarget.Codex, ".agents")]
    public void InstallMapsPersonalDirectoryAndCopiesEntireBundle(AgentSkillTarget target, string agentDirectory)
    {
        var manager = Manager();
        Assert.AreEqual(AgentSkillInstallState.NotInstalled, Status(manager, target).State);
        var installed = manager.Install(target, keepUpdated: false);
        Assert.AreEqual(Path.Combine(profile, agentDirectory, "skills", "powertoys-powerscripts"), installed.DestinationDirectory);
        Assert.AreEqual(AgentSkillInstallState.Installed, installed.State);
        Assert.AreEqual("1.0.0", installed.InstalledVersion);
        Assert.IsFalse(installed.KeepUpdated);
        AssertBundle(installed.DestinationDirectory, "1.0.0");
        Assert.AreEqual(installed, Status(Manager(), target));
        Assert.IsTrue(File.ReadAllText(statePath).Contains("Files", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CustomFolderRequiresSelectionThenSupportsPersistedInstallAndUpdate()
    {
        var manager = Manager();
        Assert.AreEqual(string.Empty, Status(manager, AgentSkillTarget.Custom).DestinationDirectory);
        Assert.ThrowsException<InvalidOperationException>(() => manager.Install(AgentSkillTarget.Custom, keepUpdated: false));
        Assert.ThrowsException<InvalidOperationException>(() => manager.Update(AgentSkillTarget.Custom));
        var parent = Directory.CreateDirectory(Path.Combine(root, "custom-skills")).FullName;
        manager.SetCustomDirectory(parent);
        var destination = Path.Combine(parent, "powertoys-powerscripts");
        Assert.AreEqual(destination, Status(Manager(), AgentSkillTarget.Custom).DestinationDirectory);
        Assert.IsFalse(Directory.Exists(destination));

        var installed = Manager().Install(AgentSkillTarget.Custom, keepUpdated: false);
        Assert.AreEqual(AgentSkillInstallState.Installed, installed.State);
        AssertExactTree(bundle, destination);
        SetBundleVersion("2.0.0");
        Assert.AreEqual(AgentSkillInstallState.UpdateAvailable, Status(manager, AgentSkillTarget.Custom).State);
        manager.Update(AgentSkillTarget.Custom);
        AssertBundle(destination, "2.0.0");
        Assert.IsFalse(Status(manager, AgentSkillTarget.Custom).KeepUpdated);
    }

    [TestMethod]
    public void SwitchingCustomFoldersPreservesOwnershipAndAutomaticUpdateChoices()
    {
        var manager = Manager();
        var first = Directory.CreateDirectory(Path.Combine(root, "first-skills")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(root, "second-skills")).FullName;
        manager.SetCustomDirectory(first);
        var optedIn = manager.Install(AgentSkillTarget.Custom, keepUpdated: true);
        manager.SetCustomDirectory(second);
        var optedOut = manager.Install(AgentSkillTarget.Custom, keepUpdated: false);
        SetBundleVersion("2.0.0");
        Manager().SynchronizeAutomaticUpdates();
        AssertBundle(optedIn.DestinationDirectory, "2.0.0");
        AssertBundle(optedOut.DestinationDirectory, "1.0.0");
        manager.SetCustomDirectory(first.ToUpperInvariant());
        Assert.AreEqual(AgentSkillInstallState.Installed, Status(manager, AgentSkillTarget.Custom).State);
        manager.SetCustomDirectory(second);
        Assert.AreEqual(AgentSkillInstallState.UpdateAvailable, Status(manager, AgentSkillTarget.Custom).State);
    }

    [TestMethod]
    public void CustomFolderAliasingAnAgentUsesTheSameOwnershipRecord()
    {
        var manager = Manager();
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: false);
        manager.SetCustomDirectory(Path.GetDirectoryName(installed.DestinationDirectory)!);
        Assert.AreEqual(AgentSkillInstallState.Installed, Status(manager, AgentSkillTarget.Custom).State);
        manager.SetAutomaticUpdates(AgentSkillTarget.Custom, enabled: true);
        Assert.IsTrue(Status(manager, AgentSkillTarget.Copilot).KeepUpdated);
        SetBundleVersion("2.0.0");
        manager.Update(AgentSkillTarget.Custom);
        AssertBundle(installed.DestinationDirectory, "2.0.0");
        Assert.AreEqual(AgentSkillInstallState.Installed, Status(manager, AgentSkillTarget.Copilot).State);
        Assert.AreEqual(0, JsonNode.Parse(File.ReadAllText(statePath))!["CustomCopies"]!.AsObject().Count);
    }

    [TestMethod]
    public void CustomModifiedAndUnmanagedCopiesArePreserved()
    {
        var manager = Manager();
        var parent = Directory.CreateDirectory(Path.Combine(root, "custom-skills")).FullName;
        manager.SetCustomDirectory(parent);
        manager.Export(parent);
        Assert.AreEqual(AgentSkillInstallState.Conflict, Status(manager, AgentSkillTarget.Custom).State);
        Assert.ThrowsException<IOException>(() => manager.Install(AgentSkillTarget.Custom, keepUpdated: false));
        var managedParent = Directory.CreateDirectory(Path.Combine(root, "managed-skills")).FullName;
        manager.SetCustomDirectory(managedParent);
        var installed = manager.Install(AgentSkillTarget.Custom, keepUpdated: true);
        var skill = Path.Combine(installed.DestinationDirectory, "SKILL.md");
        File.WriteAllText(skill, "Personal instructions");
        SetBundleVersion("2.0.0");
        manager.SynchronizeAutomaticUpdates();
        Assert.AreEqual(AgentSkillInstallState.Modified, Status(manager, AgentSkillTarget.Custom).State);
        Assert.ThrowsException<IOException>(() => manager.Update(AgentSkillTarget.Custom));
        Assert.AreEqual("Personal instructions", File.ReadAllText(skill));
        manager.SetAutomaticUpdates(AgentSkillTarget.Custom, enabled: false);
        Assert.IsFalse(Status(Manager(), AgentSkillTarget.Custom).KeepUpdated);
    }

    [TestMethod]
    public void CustomSelectionRejectsMissingAndNestedManagedFoldersWithoutChangingState()
    {
        var manager = Manager();
        var parent = Directory.CreateDirectory(Path.Combine(root, "custom-skills")).FullName;
        manager.SetCustomDirectory(parent);
        var installed = manager.Install(AgentSkillTarget.Custom, keepUpdated: false);
        var state = File.ReadAllText(statePath);
        Assert.ThrowsException<DirectoryNotFoundException>(() => manager.SetCustomDirectory(Path.Combine(root, "missing")));
        Assert.ThrowsException<ArgumentException>(() => manager.SetCustomDirectory(bundle));
        Assert.ThrowsException<ArgumentException>(() => manager.SetCustomDirectory(installed.DestinationDirectory));
        Assert.AreEqual(state, File.ReadAllText(statePath));
        AssertBundle(installed.DestinationDirectory, "1.0.0");
    }

    [TestMethod]
    public void ExistingPersonalStateWithoutCustomFieldsLoadsWithoutRewriting()
    {
        var manager = Manager();
        manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        var state = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        state.Remove("CustomDirectory");
        state.Remove("CustomCopies");
        var legacy = state.ToJsonString();
        File.WriteAllText(statePath, legacy);
        Assert.AreEqual(AgentSkillInstallState.Installed, Status(Manager(), AgentSkillTarget.Copilot).State);
        Assert.AreEqual(string.Empty, Status(manager, AgentSkillTarget.Custom).DestinationDirectory);
        Assert.AreEqual(legacy, File.ReadAllText(statePath));
    }

    [DataTestMethod]
    [DataRow("folder")]
    [DataRow("copies")]
    [DataRow("hash")]
    [DataRow("personal-target")]
    public void InvalidCustomStateIsReportedWithoutReplacingTheCopy(string corruption)
    {
        var manager = Manager();
        var parent = Directory.CreateDirectory(Path.Combine(root, "custom-skills")).FullName;
        manager.SetCustomDirectory(parent);
        var installed = manager.Install(AgentSkillTarget.Custom, keepUpdated: false);
        var state = JsonNode.Parse(File.ReadAllText(statePath))!;
        switch (corruption)
        {
            case "folder":
                state["CustomDirectory"] = @"..\not-canonical";
                break;
            case "copies":
                state["CustomCopies"] = null;
                break;
            case "hash":
                state["CustomCopies"]![installed.DestinationDirectory]!["Files"]!["SKILL.md"] = "invalid";
                break;
            case "personal-target":
                state["Targets"]!["Custom"] = state["CustomCopies"]![installed.DestinationDirectory]!.DeepClone();
                break;
        }

        var content = state.ToJsonString();
        File.WriteAllText(statePath, content);
        Assert.ThrowsException<InvalidDataException>(() => manager.Update(AgentSkillTarget.Custom));
        Assert.AreEqual(content, File.ReadAllText(statePath));
        AssertBundle(installed.DestinationDirectory, "1.0.0");
    }

    [DataTestMethod]
    [DataRow(AgentSkillTarget.Copilot)]
    [DataRow(AgentSkillTarget.ClaudeCode)]
    [DataRow(AgentSkillTarget.Codex)]
    public void RealBundleRoundTripsEveryFileUnchanged(AgentSkillTarget target)
    {
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project != null && !File.Exists(Path.Combine(project.FullName, "PowerScripts.Core.Tests.csproj")))
        {
            project = project.Parent;
        }

        Assert.IsNotNull(project, "This acceptance test requires the PowerScripts source checkout.");
        var realBundle = Path.Combine(project.Parent!.FullName, "AgentSkills", "powertoys-powerscripts");
        var manager = new AgentSkillManager(realBundle, statePath, profile);
        var installed = manager.Install(target, keepUpdated: false);
        AssertExactTree(realBundle, installed.DestinationDirectory);
        AssertOwnershipHashes(realBundle, target);

        var exportParent = Path.Combine(root, "export");
        manager.Export(exportParent);
        AssertExactTree(realBundle, Path.Combine(exportParent, "powertoys-powerscripts"));

        manager.Update(target);
        AssertExactTree(realBundle, installed.DestinationDirectory);
        AssertOwnershipHashes(realBundle, target);
    }

    [TestMethod]
    public void FormattedBundleMetadataIsCopiedVerbatimAndIncludedInOwnership()
    {
        var metadata = Path.Combine(bundle, "bundle.json");
        var original = Encoding.UTF8.GetBytes("\uFEFF{\r\n  \"version\" : \"1.0.0\",\r\n  \"description\": \"Formatting must survive copying.\"\r\n}\r\n");
        File.WriteAllBytes(metadata, original);
        File.WriteAllBytes(Path.Combine(bundle, "references", "binary.dat"), [0, 255, 13, 10, 128, 0]);
        var manager = Manager();
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        AssertExactTree(bundle, installed.DestinationDirectory);
        AssertOwnershipHashes(bundle, AgentSkillTarget.Copilot);

        var exportParent = Path.Combine(root, "export");
        manager.Export(exportParent);
        AssertExactTree(bundle, Path.Combine(exportParent, "powertoys-powerscripts"));

        var updated = Encoding.UTF8.GetBytes("\uFEFF{\n\t\"description\" : \"Still verbatim.\",\n\t\"version\" : \"2.0.0\"\n}\n");
        File.WriteAllBytes(metadata, updated);
        manager.Update(AgentSkillTarget.Copilot);
        AssertExactTree(bundle, installed.DestinationDirectory);
        AssertOwnershipHashes(bundle, AgentSkillTarget.Copilot);

        var destinationMetadata = Path.Combine(installed.DestinationDirectory, "bundle.json");
        var customized = updated.Concat(Encoding.UTF8.GetBytes("\r\n ")).ToArray();
        File.WriteAllBytes(destinationMetadata, customized);
        Assert.AreEqual(AgentSkillInstallState.Modified, Status(manager, AgentSkillTarget.Copilot).State);
        SetBundleVersion("3.0.0");
        manager.SynchronizeAutomaticUpdates();
        CollectionAssert.AreEqual(customized, File.ReadAllBytes(destinationMetadata));
        Assert.ThrowsException<IOException>(() => manager.Update(AgentSkillTarget.Copilot));
    }

    [TestMethod]
    public void SynchronizeUpdatesOnlyOptedInIntactCopies()
    {
        var manager = Manager();
        File.WriteAllText(Path.Combine(bundle, "references", "obsolete.md"), "Old bundled reference");
        var optedIn = manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        var optedOut = manager.Install(AgentSkillTarget.ClaudeCode, keepUpdated: false);
        var modified = manager.Install(AgentSkillTarget.Codex, keepUpdated: true);
        File.WriteAllText(Path.Combine(modified.DestinationDirectory, "SKILL.md"), "My customized instructions");
        File.Delete(Path.Combine(bundle, "references", "obsolete.md"));
        File.WriteAllText(Path.Combine(bundle, "references", "new.md"), "New bundled reference");
        SetBundleVersion("2.0.0");

        manager.SynchronizeAutomaticUpdates();

        AssertBundle(optedIn.DestinationDirectory, "2.0.0");
        AssertBundle(optedOut.DestinationDirectory, "1.0.0");
        Assert.IsFalse(File.Exists(Path.Combine(optedIn.DestinationDirectory, "references", "obsolete.md")));
        Assert.AreEqual("New bundled reference", File.ReadAllText(Path.Combine(optedIn.DestinationDirectory, "references", "new.md")));
        Assert.IsTrue(File.Exists(Path.Combine(optedOut.DestinationDirectory, "references", "obsolete.md")));
        Assert.AreEqual(AgentSkillInstallState.UpdateAvailable, Status(manager, AgentSkillTarget.ClaudeCode).State);
        Assert.AreEqual(AgentSkillInstallState.Modified, Status(manager, AgentSkillTarget.Codex).State);
        Assert.AreEqual("My customized instructions", File.ReadAllText(Path.Combine(modified.DestinationDirectory, "SKILL.md")));
        var updated = manager.Update(AgentSkillTarget.ClaudeCode);
        Assert.AreEqual("2.0.0", updated.InstalledVersion);
        Assert.IsFalse(updated.KeepUpdated);
    }

    [TestMethod]
    public void AutomaticUpdatePreferencePersistsAndCanBeDisabled()
    {
        var manager = Manager();
        manager.Install(AgentSkillTarget.Copilot, keepUpdated: false);
        manager.SetAutomaticUpdates(AgentSkillTarget.Copilot, enabled: true);
        Assert.IsTrue(Status(Manager(), AgentSkillTarget.Copilot).KeepUpdated);
        manager.SetAutomaticUpdates(AgentSkillTarget.Copilot, enabled: false);
        SetBundleVersion("2.0.0");
        Manager().SynchronizeAutomaticUpdates();
        Assert.AreEqual("1.0.0", Status(manager, AgentSkillTarget.Copilot).InstalledVersion);
        Assert.ThrowsException<InvalidOperationException>(() => manager.SetAutomaticUpdates(AgentSkillTarget.Codex, enabled: true));
    }

    [DataTestMethod]
    [DataRow("edited")]
    [DataRow("added")]
    [DataRow("missing")]
    [DataRow("empty-directory")]
    public void ModifiedCopiesAreNeverOverwritten(string modification)
    {
        var manager = Manager();
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        var skill = Path.Combine(installed.DestinationDirectory, "SKILL.md");
        switch (modification)
        {
            case "edited":
                File.WriteAllText(skill, "Customized");
                break;
            case "added":
                File.WriteAllText(Path.Combine(installed.DestinationDirectory, "mine.md"), "Personal");
                break;
            case "missing":
                File.Delete(skill);
                break;
            case "empty-directory":
                Directory.CreateDirectory(Path.Combine(installed.DestinationDirectory, "mine"));
                break;
        }

        SetBundleVersion("2.0.0");
        Assert.AreEqual(AgentSkillInstallState.Modified, Status(manager, AgentSkillTarget.Copilot).State);
        Assert.ThrowsException<IOException>(() => manager.Update(AgentSkillTarget.Copilot));
        Assert.ThrowsException<IOException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: false));
        Assert.ThrowsException<InvalidOperationException>(() => manager.SetAutomaticUpdates(AgentSkillTarget.Copilot, enabled: true));
        manager.SynchronizeAutomaticUpdates();
        Assert.AreEqual(AgentSkillInstallState.Modified, Status(manager, AgentSkillTarget.Copilot).State);
        Assert.AreEqual("1.0.0", Status(manager, AgentSkillTarget.Copilot).InstalledVersion);
        switch (modification)
        {
            case "edited":
                Assert.AreEqual("Customized", File.ReadAllText(skill));
                break;
            case "added":
                Assert.AreEqual("Personal", File.ReadAllText(Path.Combine(installed.DestinationDirectory, "mine.md")));
                break;
            case "missing":
                Assert.IsFalse(File.Exists(skill));
                break;
            case "empty-directory":
                Assert.IsTrue(Directory.Exists(Path.Combine(installed.DestinationDirectory, "mine")));
                break;
        }
    }

    [TestMethod]
    public void UnknownExistingFolderRemainsAConflictEvenWhenItMatchesBundle()
    {
        var manager = Manager();
        var destination = Status(manager, AgentSkillTarget.Copilot).DestinationDirectory;
        manager.Export(Path.GetDirectoryName(destination)!);
        Assert.AreEqual(AgentSkillInstallState.Conflict, Status(manager, AgentSkillTarget.Copilot).State);
        Assert.ThrowsException<IOException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: true));
        Assert.ThrowsException<IOException>(() => manager.Update(AgentSkillTarget.Copilot));
        manager.SynchronizeAutomaticUpdates();
        AssertBundle(destination, "1.0.0");
    }

    [TestMethod]
    public void MissingOwnedFolderRequiresExplicitReinstallAndNewOptIn()
    {
        var manager = Manager();
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        Directory.Delete(installed.DestinationDirectory, recursive: true);
        SetBundleVersion("2.0.0");
        manager.SynchronizeAutomaticUpdates();
        var missing = Status(manager, AgentSkillTarget.Copilot);
        Assert.AreEqual(AgentSkillInstallState.NotInstalled, missing.State);
        Assert.IsFalse(missing.KeepUpdated);
        Assert.IsFalse(Directory.Exists(installed.DestinationDirectory));
        Assert.ThrowsException<InvalidOperationException>(() => manager.Update(AgentSkillTarget.Copilot));
        Assert.ThrowsException<InvalidOperationException>(() => manager.SetAutomaticUpdates(AgentSkillTarget.Copilot, enabled: true));
        Assert.IsFalse(manager.Install(AgentSkillTarget.Copilot, keepUpdated: false).KeepUpdated);
        AssertBundle(installed.DestinationDirectory, "2.0.0");
    }

    [TestMethod]
    public void ExportCreatesCompleteUnmanagedCopyAndNeverOverwrites()
    {
        var manager = Manager();
        var parent = Path.Combine(root, "export");
        manager.Export(parent);
        var exported = Path.Combine(parent, "powertoys-powerscripts");
        AssertBundle(exported, "1.0.0");
        File.WriteAllText(Path.Combine(exported, "SKILL.md"), "Personal");
        Assert.ThrowsException<IOException>(() => manager.Export(parent));
        Assert.AreEqual("Personal", File.ReadAllText(Path.Combine(exported, "SKILL.md")));
        Assert.IsFalse(File.Exists(statePath));
        Assert.IsTrue(manager.GetStatuses().All(status => status.State == AgentSkillInstallState.NotInstalled));
    }

    [TestMethod]
    public void ExistingDestinationFileIsAConflictAndIsPreserved()
    {
        var manager = Manager();
        var destination = Status(manager, AgentSkillTarget.Copilot).DestinationDirectory;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, "User-owned file");
        Assert.AreEqual(AgentSkillInstallState.Conflict, Status(manager, AgentSkillTarget.Copilot).State);
        Assert.ThrowsException<IOException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: false));
        Assert.AreEqual("User-owned file", File.ReadAllText(destination));
    }

    [DataTestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("""{"FormatVersion":99,"Targets":{}}""")]
    [DataRow("""{"FormatVersion":1,"Targets":null}""")]
    public void MalformedStateIsReportedAndNotReplaced(string content)
    {
        var manager = Manager();
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, content);
        Assert.ThrowsException<InvalidDataException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: true));
        Assert.AreEqual(content, File.ReadAllText(statePath));
        Assert.IsFalse(Directory.Exists(Path.Combine(profile, ".copilot")));
    }

    [DataTestMethod]
    [DataRow(@"..\outside.md")]
    [DataRow(@"C:\outside.md")]
    [DataRow(@"references\..\outside.md")]
    [DataRow(@"references\file.md:stream")]
    [DataRow(@"references\NUL.md")]
    [DataRow(@"references\file.md.")]
    [DataRow(@"references\\file.md")]
    [DataRow("references/file.md")]
    public void UnsafeOwnershipPathsAreRejectedWithoutTouchingCopy(string path)
    {
        var manager = Manager();
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        var state = JsonNode.Parse(File.ReadAllText(statePath))!;
        state["Targets"]!["Copilot"]!["Files"]![path] = new string('A', 64);
        File.WriteAllText(statePath, state.ToJsonString());
        SetBundleVersion("2.0.0");
        Assert.ThrowsException<InvalidDataException>(() => manager.SynchronizeAutomaticUpdates());
        AssertBundle(installed.DestinationDirectory, "1.0.0");
    }

    [TestMethod]
    public void DeterministicMalformedManifestFuzzDoesNotAdoptOrChangeFolders()
    {
        var manager = Manager();
        var destination = Status(manager, AgentSkillTarget.Copilot).DestinationDirectory;
        Directory.CreateDirectory(destination);
        var userFile = Path.Combine(destination, "mine.md");
        File.WriteAllText(userFile, "Keep");
        var random = new Random(9327);
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var bytes = new byte[random.Next(1, 150)];
            random.NextBytes(bytes);
            File.WriteAllText(statePath, "!" + Convert.ToBase64String(bytes));
            Assert.ThrowsException<InvalidDataException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: true));
            Assert.AreEqual("Keep", File.ReadAllText(userFile));
        }
    }

    [TestMethod]
    public void GetStatusesDoesNotStageBundlesOrRewriteState()
    {
        var manager = Manager();
        manager.GetStatuses();
        Assert.IsFalse(File.Exists(statePath));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(profile).Length);
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: false);
        var content = File.ReadAllText(statePath);
        var timestamp = File.GetLastWriteTimeUtc(statePath);
        manager.GetStatuses();
        Assert.AreEqual(content, File.ReadAllText(statePath));
        Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(statePath));
        Assert.AreEqual(1, Directory.GetFileSystemEntries(Path.GetDirectoryName(installed.DestinationDirectory)!).Length);
        Assert.AreEqual(2, Directory.GetFileSystemEntries(Path.GetDirectoryName(statePath)!).Length);
    }

    [DataTestMethod]
    [DataRow("hash")]
    [DataRow("files")]
    [DataRow("directories")]
    [DataRow("target")]
    [DataRow("profile")]
    [DataRow("duplicate")]
    public void InvalidOwnershipCannotReplaceAnExistingCopy(string corruption)
    {
        var manager = Manager();
        var installed = manager.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        var state = JsonNode.Parse(File.ReadAllText(statePath))!;
        var copy = state["Targets"]!["Copilot"]!;
        switch (corruption)
        {
            case "hash":
                copy["Files"]!["SKILL.md"] = "invalid";
                break;
            case "files":
                copy["Files"] = new JsonObject();
                break;
            case "directories":
                copy["Directories"] = null;
                break;
            case "target":
                state["Targets"]!["UnknownAgent"] = copy.DeepClone();
                break;
            case "profile":
                state["UserProfileDirectory"] = root;
                break;
        }

        var content = state.ToJsonString();
        if (corruption == "duplicate")
        {
            content = content.Replace("\"FormatVersion\":1", "\"FormatVersion\":1,\"FormatVersion\":1", StringComparison.Ordinal);
        }

        File.WriteAllText(statePath, content);
        File.WriteAllText(Path.Combine(installed.DestinationDirectory, "SKILL.md"), "User customization");
        SetBundleVersion("2.0.0");
        Assert.ThrowsException<InvalidDataException>(() => manager.SynchronizeAutomaticUpdates());
        Assert.ThrowsException<InvalidDataException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: true));
        Assert.AreEqual(content, File.ReadAllText(statePath));
        Assert.AreEqual("User customization", File.ReadAllText(Path.Combine(installed.DestinationDirectory, "SKILL.md")));
    }

    [DataTestMethod]
    [DataRow(AgentSkillTarget.Copilot)]
    [DataRow(AgentSkillTarget.Custom)]
    public void FailedStateCommitRollsBackUpdatedFolderAndPreservesOwnership(AgentSkillTarget target)
    {
        var manager = Manager();
        if (target == AgentSkillTarget.Custom)
        {
            manager.SetCustomDirectory(Directory.CreateDirectory(Path.Combine(root, "custom-skills")).FullName);
        }

        var installed = manager.Install(target, keepUpdated: false);
        var oldState = File.ReadAllText(statePath);
        SetBundleVersion("2.0.0");
        using (var readOnlyState = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AssertCommitFailure(() => manager.Update(target));
        }

        Assert.AreEqual(oldState, File.ReadAllText(statePath));
        AssertBundle(installed.DestinationDirectory, "1.0.0");
        Assert.AreEqual(AgentSkillInstallState.UpdateAvailable, Status(manager, target).State);
        Assert.AreEqual(1, Directory.GetFileSystemEntries(Path.GetDirectoryName(installed.DestinationDirectory)!).Length);
    }

    [TestMethod]
    public void FailedFirstStateCommitRemovesNewCopyWithoutAdoptingAnything()
    {
        var manager = Manager();
        var destination = Status(manager, AgentSkillTarget.Copilot).DestinationDirectory;
        // A readable valid manifest can still be locked against atomic replacement.
        manager.Install(AgentSkillTarget.ClaudeCode, keepUpdated: false);
        var oldState = File.ReadAllText(statePath);
        using (var readOnlyState = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AssertCommitFailure(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: true));
        }

        Assert.AreEqual(oldState, File.ReadAllText(statePath));
        Assert.IsFalse(Directory.Exists(destination));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(Path.GetDirectoryName(destination)!).Length);
    }

    [TestMethod]
    public void ExclusiveLockRejectsConcurrentInstanceAndReloadPreventsStaleState()
    {
        var first = Manager();
        var second = Manager();
        first.GetStatuses();
        using (var heldLock = new FileStream(statePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsException<IOException>(() => second.Install(AgentSkillTarget.Copilot, keepUpdated: true));
            Assert.ThrowsException<IOException>(() => second.GetStatuses());
        }

        first.Install(AgentSkillTarget.Copilot, keepUpdated: true);
        second.Install(AgentSkillTarget.ClaudeCode, keepUpdated: false);
        Assert.AreEqual(AgentSkillInstallState.Installed, Status(second, AgentSkillTarget.Copilot).State);
        first.SetAutomaticUpdates(AgentSkillTarget.Copilot, enabled: false);
        Assert.AreEqual(AgentSkillInstallState.Installed, Status(first, AgentSkillTarget.ClaudeCode).State);
    }

    [DataTestMethod]
    [DataRow("source")]
    [DataRow("destination")]
    [DataRow("manifest")]
    [DataRow("custom")]
    public void ReparsePointChainsAreRejected(string location)
    {
        var manager = Manager();
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "keep.md"), "Keep");
        var link = location switch
        {
            "source" => Path.Combine(bundle, "linked"),
            "destination" => Path.Combine(profile, ".copilot"),
            "custom" => Path.Combine(root, "custom-link"),
            _ => Path.GetDirectoryName(statePath)!,
        };
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Inconclusive("Creating symlinks requires Developer Mode or the symbolic-link privilege.");
        }
        catch (IOException)
        {
            Assert.Inconclusive("The test environment does not support creating directory symlinks.");
        }

        try
        {
            if (location == "custom")
            {
                Assert.ThrowsException<IOException>(() => manager.SetCustomDirectory(link));
            }
            else
            {
                Assert.ThrowsException<IOException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: true));
            }
            Assert.AreEqual("Keep", File.ReadAllText(Path.Combine(outside, "keep.md")));
            Assert.AreEqual(1, Directory.GetFileSystemEntries(outside).Length);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [TestMethod]
    public void UnsafeOverlappingPathsAndInvalidTargetAreRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => new AgentSkillManager(bundle, Path.Combine(bundle, "state.json"), profile));
        var manager = Manager();
        Assert.ThrowsException<ArgumentException>(() => manager.Export(bundle));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => manager.Install((AgentSkillTarget)42, keepUpdated: false));
        Assert.ThrowsException<ArgumentException>(() => new AgentSkillManager(bundle, statePath, @"C:\"));
    }

    [TestMethod]
    public void InvalidBundleFailsExplicitlyBeforeInstallation()
    {
        var manager = Manager();
        File.WriteAllText(Path.Combine(bundle, "bundle.json"), "{}");
        Assert.ThrowsException<InvalidDataException>(() => manager.Install(AgentSkillTarget.Copilot, keepUpdated: false));
        File.Delete(Path.Combine(bundle, "SKILL.md"));
        SetBundleVersion("1.0.0");
        Assert.ThrowsException<InvalidDataException>(() => manager.GetStatuses());
        Assert.IsFalse(Directory.Exists(Path.Combine(profile, ".copilot")));
    }

    private AgentSkillManager Manager()
    {
        return new AgentSkillManager(bundle, statePath, profile);
    }

    private static AgentSkillStatus Status(AgentSkillManager manager, AgentSkillTarget target)
    {
        return manager.GetStatuses().Single(status => status.Target == target);
    }

    private void SetBundleVersion(string version)
    {
        File.WriteAllText(Path.Combine(bundle, "bundle.json"), new JsonObject { ["version"] = version }.ToJsonString());
    }

    private static void AssertBundle(string directory, string version)
    {
        Assert.AreEqual("# PowerScripts skill", File.ReadAllText(Path.Combine(directory, "SKILL.md")));
        Assert.AreEqual("Portable reference", File.ReadAllText(Path.Combine(directory, "references", "guide.md")));
        Assert.IsTrue(Directory.Exists(Path.Combine(directory, "references", "empty")));
        Assert.AreEqual(version, JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "bundle.json")))!["version"]!.GetValue<string>());
    }

    private static void AssertExactTree(string sourceDirectory, string destinationDirectory)
    {
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(sourceDirectory, path)).Order(StringComparer.Ordinal).ToArray();
        var destinationFiles = Directory.GetFiles(destinationDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(destinationDirectory, path)).Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(sourceFiles, destinationFiles, "The complete file set, including bundle.json, must be copied.");
        foreach (var file in sourceFiles)
        {
            CollectionAssert.AreEqual(
                File.ReadAllBytes(Path.Combine(sourceDirectory, file)),
                File.ReadAllBytes(Path.Combine(destinationDirectory, file)),
                "Bundle bytes changed: " + file);
        }

        var sourceDirectories = Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(sourceDirectory, path)).Order(StringComparer.Ordinal).ToArray();
        var destinationDirectories = Directory.GetDirectories(destinationDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(destinationDirectory, path)).Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(sourceDirectories, destinationDirectories);
    }

    private void AssertOwnershipHashes(string sourceDirectory, AgentSkillTarget target)
    {
        var files = JsonNode.Parse(File.ReadAllText(statePath))!["Targets"]![target.ToString()]!["Files"]!.AsObject();
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        Assert.AreEqual(sourceFiles.Length, files.Count);
        foreach (var file in sourceFiles)
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), files[relative]!.GetValue<string>(), relative);
        }
    }

    private static void AssertCommitFailure(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Assert.Fail("Replacing a manifest held without delete sharing must fail.");
    }
}
