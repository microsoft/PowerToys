// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using Microsoft.Win32;

namespace EnvironmentVariablesUILib.UnitTests.Models;

[TestClass]
[DoNotParallelize]
public class VariableTests
{
    private static readonly string[] OriginalSegments = { "a", string.Empty, "b", string.Empty };
    private static readonly string[] UpdatedSegments = { "a", string.Empty, "%ROOT%", string.Empty };
    private static readonly (string Operation, string Name)[] RenameMutations =
    {
        ("Delete", "OLD"),
        ("Delete", "OLD_PowerToys_Focus"),
        ("Write", "OLD"),
        ("Write", "NEW_PowerToys_Focus"),
        ("Write", "NEW"),
    };

    [TestMethod]
    [DataRow("", new[] { "" })]
    [DataRow("a;;b;", new[] { "a", "", "b", "" })]
    [DataRow("%ROOT%; C:\\Tools\\ ;%ROOT%", new[] { "%ROOT%", " C:\\Tools\\ ", "%ROOT%" })]
    public void Constructor_PathList_PreservesEmptySegmentsAndRawLiterals(string raw, string[] expected)
    {
        var variable = new Variable("pAtH", raw, VariablesSetType.User);

        Assert.AreEqual(raw, variable.Values);
        CollectionAssert.AreEqual(expected, variable.ValuesList.Select(item => item.Text).ToArray());
        Assert.IsTrue(variable.IsPath);
        Assert.IsTrue(variable.ShowAsList);
        if (variable.ValuesList.Count > 1)
        {
            Assert.AreNotSame(variable.ValuesList[0], variable.ValuesList[^1]);
        }
    }

    [TestMethod]
    [DataRow(false, VariablesSetType.System)]
    [DataRow(true, VariablesSetType.Profile)]
    public void Clone_EditedListAndFields_DoNotChangeOriginal(bool profile, VariablesSetType expectedScope)
    {
        var original = new Variable("PATH", "a;;b;", VariablesSetType.System);

        var clone = original.Clone(profile);

        Assert.AreNotSame(original, clone);
        Assert.AreNotSame(original.ValuesList, clone.ValuesList);
        Assert.AreEqual(original.Name, clone.Name);
        Assert.AreEqual(original.Values, clone.Values);
        Assert.AreEqual(expectedScope, clone.ParentType);
        for (var index = 0; index < original.ValuesList.Count; index++)
        {
            Assert.AreNotSame(original.ValuesList[index], clone.ValuesList[index]);
        }

        clone.ValuesList[0].Text = "changed";
        clone.ValuesList.RemoveAt(1);
        clone.Name = "OTHER";
        clone.Values = "replacement";

        Assert.AreEqual("PATH", original.Name);
        Assert.AreEqual("a;;b;", original.Values);
        Assert.AreEqual(VariablesSetType.System, original.ParentType);
        CollectionAssert.AreEqual(OriginalSegments, original.ValuesList.Select(item => item.Text).ToArray());
    }

    [TestMethod]
    public async Task Update_InactiveVariableChangesModelBeforeSubmittingWorkWithoutRegistryEffects()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var variable = new Variable("OLD", "original", VariablesSetType.Profile);
        var originalRows = variable.ValuesList;
        var edited = new Variable("pAtH", "a;;%ROOT%;", VariablesSetType.User);
        var submitted = false;

        await variable.Update(edited, false, null!, fixture.Environment, action =>
        {
            submitted = true;
            Assert.AreEqual("pAtH", variable.Name);
            Assert.AreEqual("a;;%ROOT%;", variable.Values);
            Assert.AreEqual(VariablesSetType.Profile, variable.ParentType);
            Assert.AreNotSame(originalRows, variable.ValuesList);
            CollectionAssert.AreEqual(
                UpdatedSegments,
                variable.ValuesList.Select(item => item.Text).ToArray());
            return EnvironmentVariablesFixture.RunImmediately(action);
        });

        Assert.IsTrue(submitted);
        Assert.AreEqual(0, fixture.Environment.Opens.Count);
        Assert.AreEqual(0, fixture.Environment.Notifications.Count);
    }

    [TestMethod]
    [DataRow(true, "%ROOT%;saved", new[] { "PATH" })]
    [DataRow(false, "currently-applied", new[] { "PATH_PowerToys_Focus", "PATH" })]
    public async Task Update_ActiveProfilePreservesExistingBackupOrCapturesCurrentUserValue(
        bool hasBackup, string expectedBackup, string[] expectedWrites)
    {
        using var fixture = new EnvironmentVariablesFixture();
        fixture.Environment.User["PATH"] = "currently-applied";
        if (hasBackup)
        {
            fixture.Environment.User["PATH_PowerToys_Focus"] = "%ROOT%;saved";
        }

        var profile = new ProfileVariablesSet(Guid.NewGuid(), "Focus");
        var variable = new Variable("PATH", "currently-applied", VariablesSetType.Profile);
        var edited = new Variable("PATH", "next;;", VariablesSetType.Profile);

        await variable.Update(edited, true, profile, fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

        Assert.AreEqual(expectedBackup, fixture.Environment.User["PATH_PowerToys_Focus"]);
        Assert.AreEqual("next;;", fixture.Environment.User["PATH"]);
        CollectionAssert.AreEqual(
            expectedWrites,
            fixture.Environment.Operations.Where(operation => operation.Operation == "Write").Select(operation => operation.Name).ToArray());
        Assert.AreEqual(1, fixture.Environment.Notifications.Count);
        Assert.IsTrue(fixture.Environment.Keys.All(key => key.IsDisposed));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Update_RenameRestoresOldBackupThenBacksUpNewNameEvenAfterDeleteFailure(bool failOldDelete)
    {
        using var fixture = new EnvironmentVariablesFixture();
        fixture.Environment.User["OLD"] = "old-profile";
        fixture.Environment.User["OLD_PowerToys_Focus"] = "old-original";
        fixture.Environment.User["NEW"] = "new-original";
        if (failOldDelete)
        {
            fixture.Environment.BeforeOperation = (operation, _, name) =>
            {
                if (operation == "Delete" && name == "OLD")
                {
                    throw new IOException("Scripted old-variable deletion failure.");
                }
            };
        }

        var profile = new ProfileVariablesSet(Guid.NewGuid(), "Focus");
        var variable = new Variable("OLD", "old-profile", VariablesSetType.Profile);
        var edited = new Variable("NEW", "new-profile", VariablesSetType.Profile);

        await variable.Update(edited, true, profile, fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

        Assert.AreEqual("old-original", fixture.Environment.User["OLD"]);
        Assert.AreEqual("new-profile", fixture.Environment.User["NEW"]);
        Assert.AreEqual("new-original", fixture.Environment.User["NEW_PowerToys_Focus"]);
        Assert.IsFalse(fixture.Environment.User.ContainsKey("OLD_PowerToys_Focus"));
        CollectionAssert.AreEqual(
            RenameMutations,
            fixture.Environment.Operations
                .Where(operation => operation.Operation is "Write" or "Delete")
                .Select(operation => (operation.Operation, operation.Name))
                .ToArray());
        Assert.IsTrue(fixture.Environment.Operations
            .Where(operation => operation.Operation is "Write" or "Delete")
            .All(operation => operation.Hive == RegistryHive.CurrentUser));
        Assert.AreEqual(failOldDelete ? 1 : 2, fixture.Environment.Notifications.Count);
        Assert.AreEqual(failOldDelete, fixture.Logger.Entries.Any(entry => entry.Message == "Failed to unset original variable."));
    }

    [TestMethod]
    public async Task Update_SystemVariableKeepsSameNamedUserValueUnchanged()
    {
        using var fixture = new EnvironmentVariablesFixture();
        fixture.Environment.User["Shared"] = "user-value";
        fixture.Environment.Machine["Shared"] = "machine-value";
        var variable = new Variable("Shared", "machine-value", VariablesSetType.System);
        var edited = new Variable("Shared", "changed-machine", VariablesSetType.System);

        await variable.Update(edited, true, null!, fixture.Environment, EnvironmentVariablesFixture.RunImmediately);

        Assert.AreEqual("user-value", fixture.Environment.User["Shared"]);
        Assert.AreEqual("changed-machine", fixture.Environment.Machine["Shared"]);
        var write = fixture.Environment.Operations.Single(operation => operation.Operation == "Write");
        Assert.AreEqual(RegistryHive.LocalMachine, write.Hive);
        Assert.AreEqual(1, fixture.Environment.Notifications.Count);
    }
}
