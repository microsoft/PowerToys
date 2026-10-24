// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.UnitTests.TestSupport;
using EnvironmentVariablesUILib.ViewModels;

namespace EnvironmentVariablesUILib.UnitTests.ViewModels;

[TestClass]
public class VariableEditorStateTests
{
    private static readonly string[] OriginalPathRows = { "a", "b", "b", "c" };

    [TestMethod]
    [DataRow(VariablesSetType.User)]
    [DataRow(VariablesSetType.System)]
    public void DefaultAndEditGates_UseSuppliedScopeAndExcludeOnlyOriginalReference(VariablesSetType scope)
    {
        var original = new Variable("Alpha", "old", scope);
        var sameScope = new VariablesSet(Guid.NewGuid(), "scope", scope);
        sameScope.Variables.Add(original);
        var draft = new Variable("alpha", "changed", scope);

        Assert.IsFalse(VariableEditorState.CanAddDefault(draft, sameScope));
        Assert.IsTrue(VariableEditorState.CanSaveEdit(draft, original, sameScope));
        Assert.IsFalse(VariableEditorState.CanSaveEdit(draft, original.Clone(), sameScope));

        sameScope.Variables.Add(new Variable("ALPHA", "other", scope));
        Assert.IsFalse(VariableEditorState.CanSaveEdit(draft, original, sameScope));
        sameScope.Variables.Clear();
        Assert.IsTrue(VariableEditorState.CanAddDefault(draft, sameScope));
        Assert.IsTrue(VariableEditorState.CanSaveEdit(draft, original, null!));
        Assert.IsFalse(VariableEditorState.CanAddDefault(null!, sameScope));
        Assert.IsFalse(VariableEditorState.CanSaveEdit(null!, original, sameScope));
        draft.Name = "invalid=name";
        Assert.IsFalse(VariableEditorState.CanAddDefault(draft, sameScope));
        Assert.IsFalse(VariableEditorState.CanSaveEdit(draft, original, sameScope));
        draft.Name = "Beta";
        draft.Values = "invalid\0value";
        Assert.IsFalse(VariableEditorState.CanAddDefault(draft, sameScope));
        Assert.IsFalse(VariableEditorState.CanSaveEdit(draft, original, sameScope));
    }

    [TestMethod]
    [DataRow("Beta", "raw;;%ROOT%", true)]
    [DataRow("alpha", "different", false)]
    [DataRow("invalid=name", "value", false)]
    [DataRow("Beta", "bad\0value", false)]
    public void NewProfileVariable_GateAndActionValidateAndRejectDuplicateNames(string name, string value, bool accepted)
    {
        var profile = new ProfileVariablesSet(Guid.NewGuid(), "profile");
        var existing = new Variable("Alpha", "old", VariablesSetType.Profile);
        profile.Variables.Add(existing);

        Assert.AreEqual(accepted, VariableEditorState.CanAddProfileVariable(profile, true, name, value, Array.Empty<object>()));
        VariableEditorState.AddProfileVariable(profile, true, name, value, Array.Empty<object>());

        Assert.AreEqual(accepted ? 2 : 1, profile.Variables.Count);
        Assert.AreSame(existing, profile.Variables[0]);
        if (accepted)
        {
            Assert.AreEqual(name, profile.Variables[1].Name);
            Assert.AreEqual(value, profile.Variables[1].Values);
            Assert.AreEqual(VariablesSetType.Profile, profile.Variables[1].ParentType);
        }
    }

    [TestMethod]
    public void ExistingProfileVariable_ClonesInvalidLegacySelectionWithoutNewModeValidation()
    {
        var profile = new ProfileVariablesSet(Guid.NewGuid(), "profile");
        var source = new Variable("invalid=name", "raw;;%ROOT%", VariablesSetType.System);
        Assert.IsFalse(source.Validate());
        Assert.IsFalse(VariableEditorState.CanAddProfileVariable(profile, true, source.Name, source.Values, Array.Empty<object>()));
        Assert.IsTrue(VariableEditorState.CanAddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { source }));

        VariableEditorState.AddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { source });

        var added = profile.Variables.Single();
        Assert.AreNotSame(source, added);
        Assert.AreEqual(source.Name, added.Name);
        Assert.AreEqual(source.Values, added.Values);
        Assert.AreEqual(VariablesSetType.Profile, added.ParentType);
        Assert.AreEqual(VariablesSetType.System, source.ParentType);
        Assert.AreNotSame(source.ValuesList[0], added.ValuesList[0]);
        Assert.IsFalse(VariableEditorState.CanAddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { source }));
        VariableEditorState.AddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { source });
        Assert.AreEqual(1, profile.Variables.Count);
    }

    [TestMethod]
    public void ExistingSelection_GateFiltersNonVariablesButActionCastsThem()
    {
        var profile = new ProfileVariablesSet(Guid.NewGuid(), "profile");
        var source = new Variable("Beta", "value", VariablesSetType.User);
        Assert.IsFalse(VariableEditorState.CanAddProfileVariable(null!, false, string.Empty, string.Empty, new object[] { source }));
        Assert.IsFalse(VariableEditorState.CanAddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { "not a variable" }));
        Assert.IsTrue(VariableEditorState.CanAddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { "not a variable", source }));

        Assert.ThrowsExactly<InvalidCastException>(() =>
            VariableEditorState.AddProfileVariable(profile, false, string.Empty, string.Empty, new object[] { "not a variable", source }));
        Assert.AreEqual(0, profile.Variables.Count);
    }

    [TestMethod]
    public void ExistingSelection_ClonesValidPrefixBeforeInvalidEntryThrows()
    {
        var profile = new ProfileVariablesSet(Guid.NewGuid(), "profile");
        var existing = new Variable("Alpha", "existing", VariablesSetType.Profile);
        profile.Variables.Add(existing);
        var source = new Variable("Beta", "raw;;%ROOT%;raw", VariablesSetType.System);
        var later = new Variable("Gamma", "later", VariablesSetType.User);
        var selection = new object[] { source, "not a variable", later };
        var sourceRows = source.ValuesList.ToArray();

        Assert.IsTrue(VariableEditorState.CanAddProfileVariable(profile, false, string.Empty, string.Empty, selection));

        Assert.ThrowsExactly<InvalidCastException>(() =>
            VariableEditorState.AddProfileVariable(profile, false, string.Empty, string.Empty, selection));

        Assert.AreEqual(2, profile.Variables.Count);
        Assert.AreSame(existing, profile.Variables[0]);
        var added = profile.Variables[1];
        Assert.AreNotSame(source, added);
        Assert.AreEqual(source.Name, added.Name);
        Assert.AreEqual(source.Values, added.Values);
        Assert.AreEqual(VariablesSetType.Profile, added.ParentType);
        Assert.AreEqual(VariablesSetType.System, source.ParentType);
        Assert.AreNotSame(source.ValuesList, added.ValuesList);
        CollectionAssert.AreEqual(sourceRows, source.ValuesList.ToArray());
        CollectionAssert.AreEqual(
            sourceRows.Select(row => row.Text).ToArray(),
            added.ValuesList.Select(row => row.Text).ToArray());
        for (var index = 0; index < sourceRows.Length; index++)
        {
            Assert.AreNotSame(sourceRows[index], added.ValuesList[index]);
        }

        Assert.IsFalse(profile.Variables.Any(variable => variable.Name == later.Name));
    }

    [TestMethod]
    public void DuplicateSelections_ReturnOnlySecondMemberOfEachNameGroup()
    {
        var first = new Variable("Alpha", "one", VariablesSetType.User);
        var second = new Variable("ALPHA", "two", VariablesSetType.System);
        var third = new Variable("alpha", "three", VariablesSetType.User);
        var beta = new Variable("Beta", "one", VariablesSetType.User);
        var betaSecond = new Variable("BETA", "two", VariablesSetType.System);

        var removals = VariableEditorState.GetSecondSelectionsFromDuplicateNameGroups(new[] { first, second, third, beta, betaSecond });

        CollectionAssert.AreEqual(new[] { second, betaSecond }, removals);
    }

    [TestMethod]
    [DataRow("same", 1)]
    [DataRow("SAME", 2)]
    public void RemoveFirstEquivalent_UsesOrdinalValueAndRemovesOnlyFirstMatch(string removedValue, int remaining)
    {
        var profile = new ProfileVariablesSet(Guid.NewGuid(), "profile");
        var first = new Variable("Alpha", "same", VariablesSetType.Profile);
        var second = first.Clone();
        profile.Variables.Add(first);
        profile.Variables.Add(second);

        VariableEditorState.RemoveFirstEquivalent(profile, new Variable("ALPHA", removedValue, VariablesSetType.System));

        Assert.AreEqual(remaining, profile.Variables.Count);
        Assert.AreSame(second, profile.Variables[^1]);
        if (remaining == 2)
        {
            Assert.AreSame(first, profile.Variables[0]);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadSelection_ReturnsFirstEquivalentIdentityUnlessNameAlreadySelected(bool alreadySelected)
    {
        var profile = new ProfileVariablesSet(Guid.NewGuid(), "profile");
        profile.Variables.Add(new Variable("Alpha", "same", VariablesSetType.Profile));
        profile.Variables.Add(new Variable("ALPHA", "same", VariablesSetType.Profile));
        var wrongValue = new Variable("alpha", "SAME", VariablesSetType.User);
        var firstMatch = new Variable("alpha", "same", VariablesSetType.System);
        var secondMatch = new Variable("ALPHA", "same", VariablesSetType.User);
        var selected = new Variable("Alpha", "unrelated value", VariablesSetType.User);

        var additions = VariableEditorState.LoadSelection(
            profile,
            new[] { null!, wrongValue, firstMatch, secondMatch },
            alreadySelected ? new[] { selected } : Array.Empty<Variable>());

        CollectionAssert.AreEqual(alreadySelected ? Array.Empty<Variable>() : new[] { firstMatch }, additions);
        Assert.AreEqual(2, profile.Variables.Count);
    }

    [TestMethod]
    [DataRow("up", "a;b;b;c", new[] { 0, 2, 1, 3 })]
    [DataRow("down", "a;b;c;b", new[] { 0, 1, 3, 2 })]
    [DataRow("delete", "a;b;c", new[] { 0, 1, 3 })]
    [DataRow("before", "a;b;;b;c", new[] { 0, 1, -1, 2, 3 })]
    [DataRow("after", "a;b;b;;c", new[] { 0, 1, 2, -1, 3 })]
    public void PathActions_OperateOnSecondDuplicateIdentityWithoutAssigningRawValues(string action, string expected, int[] order)
    {
        var original = new Variable("PATH", "a;b;b;c", VariablesSetType.User);
        var draft = original.Clone();
        var rows = draft.ValuesList.ToArray();
        var collection = draft.ValuesList;

        var text = ActOnRow(action, draft, rows[2]);

        Assert.AreEqual(expected, text);
        Assert.AreSame(collection, draft.ValuesList);
        Assert.AreEqual(order.Length, draft.ValuesList.Count);
        for (var index = 0; index < order.Length; index++)
        {
            if (order[index] < 0)
            {
                Assert.AreEqual(string.Empty, draft.ValuesList[index].Text);
                Assert.IsFalse(rows.Any(row => ReferenceEquals(row, draft.ValuesList[index])));
            }
            else
            {
                Assert.AreSame(rows[order[index]], draft.ValuesList[index]);
            }
        }

        Assert.AreEqual("a;b;b;c", draft.Values);
        Assert.AreEqual("a;b;b;c", original.Values);
        CollectionAssert.AreEqual(OriginalPathRows, original.ValuesList.Select(row => row.Text).ToArray());
    }

    [TestMethod]
    [DataRow("up")]
    [DataRow("down")]
    [DataRow("delete")]
    [DataRow("before")]
    [DataRow("after")]
    [DataRow("commit")]
    public void PathActions_NullSelectionReturnsNoAssignmentAndPreservesRows(string action)
    {
        var draft = new Variable("PATH", "a;;%ROOT%; C:\\Tools\\ ", VariablesSetType.User);
        var rows = draft.ValuesList.ToArray();

        Assert.IsNull(ActOnRow(action, draft, null));
        CollectionAssert.AreEqual(rows, draft.ValuesList.ToArray());
        Assert.AreEqual("a;;%ROOT%; C:\\Tools\\ ", draft.Values);
    }

    [TestMethod]
    [DataRow("up", 0)]
    [DataRow("down", 2)]
    public void PathMoves_AtBoundaryReturnUnchangedTextWithoutReplacingRows(string action, int index)
    {
        var draft = new Variable("PATH", "%ROOT%;; C:\\Tools\\ ", VariablesSetType.User);
        var rows = draft.ValuesList.ToArray();

        Assert.AreEqual("%ROOT%;; C:\\Tools\\ ", ActOnRow(action, draft, rows[index]));
        CollectionAssert.AreEqual(rows, draft.ValuesList.ToArray());
        Assert.AreEqual("%ROOT%;; C:\\Tools\\ ", draft.Values);
    }

    [TestMethod]
    public void DeleteLastEntry_ReturnsMeaningfulEmptyTextAndLeavesZeroRows()
    {
        var draft = new Variable("PATH", "only", VariablesSetType.User);
        Assert.AreEqual(string.Empty, VariableEditorState.DeleteEntry(draft, draft.ValuesList[0]));
        Assert.AreEqual(0, draft.ValuesList.Count);
        Assert.AreEqual("only", draft.Values);
    }

    [TestMethod]
    [DataRow("up")]
    [DataRow("down")]
    public void PathMoves_SingleEmptyRowReturnsEmptyTextAndRetainsIdentity(string action)
    {
        var draft = new Variable("PATH", string.Empty, VariablesSetType.User);
        var row = draft.ValuesList.Single();

        Assert.AreEqual(string.Empty, ActOnRow(action, draft, row));
        Assert.AreSame(row, draft.ValuesList.Single());
        Assert.AreEqual(string.Empty, draft.Values);
    }

    [TestMethod]
    [DataRow("b", null)]
    [DataRow("changed", "a;changed;;%ROOT%")]
    [DataRow("", "a;;;%ROOT%")]
    public void CommitEntryText_ChangesOnlyChosenRowAndNeverAssignsRawValues(string text, string? expected)
    {
        var original = new Variable("PATH", "a;b;;%ROOT%", VariablesSetType.User);
        var draft = original.Clone();
        var rows = draft.ValuesList.ToArray();

        Assert.AreEqual(expected, VariableEditorState.CommitEntryText(draft, rows[1], text));
        Assert.AreEqual(text, rows[1].Text);
        CollectionAssert.AreEqual(rows, draft.ValuesList.ToArray());
        Assert.AreEqual("a;b;;%ROOT%", draft.Values);
        Assert.AreEqual("a;b;;%ROOT%", original.Values);
        Assert.AreEqual("b", original.ValuesList[1].Text);
    }

    [TestMethod]
    [DataRow("up", "a;b", false)]
    [DataRow("down", null, true)]
    [DataRow("delete", "a;b", false)]
    [DataRow("before", null, true)]
    [DataRow("after", ";a;b", false)]
    [DataRow("commit", "a;b", false)]
    public void PathActions_StaleNonmemberPreservesExistingIndexSemantics(string action, string? expected, bool throws)
    {
        var draft = new Variable("PATH", "a;b", VariablesSetType.User);
        var rows = draft.ValuesList.ToArray();
        var stale = new Variable.ValuesListItem { Text = "a" };

        if (throws)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ActOnRow(action, draft, stale));
        }
        else
        {
            Assert.AreEqual(expected, ActOnRow(action, draft, stale));
        }

        CollectionAssert.AreEqual(rows, draft.ValuesList.Where(row => rows.Contains(row)).ToArray());
        Assert.AreEqual(action == "after" ? 3 : 2, draft.ValuesList.Count);
        Assert.AreEqual(action == "commit" ? "changed" : "a", stale.Text);
        Assert.AreEqual("a;b", draft.Values);
    }

    [TestMethod]
    [DoNotParallelize]
    [DataRow("commit", "a;changed;;%ROOT%")]
    [DataRow("down", "a;;b;%ROOT%")]
    public async Task PathRowHelper_WithoutRawTransport_EditVariableSubmitsNoWorkOrPersistence(string action, string expected)
    {
        using var fixture = new EnvironmentVariablesFixture();
        var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
        using var service = new EnvironmentVariablesService(files);
        var seed = new ProfileVariablesSet(Guid.NewGuid(), "Inactive");
        seed.Variables.Add(new Variable("PATH", "a;b;;%ROOT%", VariablesSetType.Profile));
        await service.WriteAsync(new[] { seed });
        var baseline = files.Files[service.ProfilesJsonFilePath];
        fixture.Environment.User["Unrelated"] = "user-sentinel";
        fixture.Environment.Machine["Unrelated"] = "machine-sentinel";
        var vm = fixture.CreateViewModel(service: service);
        vm.LoadEnvironmentVariables();
        var profile = vm.Profiles.Single();
        var original = profile.Variables.Single();
        var originalRows = original.ValuesList.ToArray();
        var draft = original.Clone();
        var rows = draft.ValuesList.ToArray();
        fixture.Environment.Operations.Clear();
        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
        Assert.AreEqual(1, files.Writes.Count);
        Assert.IsTrue(fixture.Work.IsQuiescent);

        var result = action == "commit"
            ? VariableEditorState.CommitEntryText(draft, rows[1], "changed")
            : VariableEditorState.MoveDown(draft, rows[1]);

        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected, string.Join(";", draft.ValuesList.Select(row => row.Text)));
        if (action == "commit")
        {
            CollectionAssert.AreEqual(rows, draft.ValuesList.ToArray());
            Assert.AreEqual("changed", rows[1].Text);
        }
        else
        {
            CollectionAssert.AreEqual(new[] { rows[0], rows[2], rows[1], rows[3] }, draft.ValuesList.ToArray());
        }

        Assert.AreEqual("a;b;;%ROOT%", draft.Values);
        vm.EditVariable(original, draft, profile);

        Assert.AreSame(profile, vm.Profiles.Single());
        Assert.AreSame(original, profile.Variables.Single());
        Assert.AreEqual("PATH", original.Name);
        Assert.AreEqual("a;b;;%ROOT%", original.Values);
        Assert.AreEqual("a;b;;%ROOT%", draft.Values);
        CollectionAssert.AreEqual(originalRows, original.ValuesList.ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b", string.Empty, "%ROOT%" }, original.ValuesList.Select(row => row.Text).ToArray());
        Assert.IsFalse(profile.IsEnabled);
        Assert.IsNull(vm.AppliedProfile);
        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
        Assert.AreEqual(0, fixture.Work.RefreshReceiptCount);
        Assert.AreEqual(0, fixture.Telemetry.Changed.Count);
        Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
        Assert.AreEqual(0, fixture.Environment.Operations.Count);
        Assert.AreEqual(0, fixture.Environment.Notifications.Count);
        Assert.AreEqual(1, fixture.Environment.User.Count);
        Assert.AreEqual("user-sentinel", fixture.Environment.User["Unrelated"]);
        Assert.AreEqual(1, fixture.Environment.Machine.Count);
        Assert.AreEqual("machine-sentinel", fixture.Environment.Machine["Unrelated"]);
        Assert.AreEqual(1, files.Writes.Count);
        Assert.AreEqual(baseline, files.Writes.Single().Contents);
        Assert.AreEqual(baseline, files.Files[service.ProfilesJsonFilePath]);
        Assert.AreEqual(0, fixture.Logger.Entries.Count);
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task ShallowProfileDraft_InnerEditThenOuterUpdatePersistSharedChild()
    {
        using var fixture = new EnvironmentVariablesFixture();
        var files = new EnvironmentVariablesFixture.ScriptedProfileFileSystem();
        using var service = new EnvironmentVariablesService(files);
        var seed = new ProfileVariablesSet(Guid.NewGuid(), "Inactive");
        seed.Variables.Add(new Variable("PATH", "a;b;;%ROOT%", VariablesSetType.Profile));
        seed.Variables.Add(new Variable("KEEP", "untouched", VariablesSetType.Profile));
        await service.WriteAsync(new[] { seed });
        var baseline = files.Files[service.ProfilesJsonFilePath];
        fixture.Environment.User["Unrelated"] = "user-sentinel";
        fixture.Environment.Machine["Unrelated"] = "machine-sentinel";
        var vm = fixture.CreateViewModel(service: service);
        vm.LoadEnvironmentVariables();
        var profiles = vm.Profiles;
        var originalProfile = profiles.Single();
        var originalVariables = originalProfile.Variables;
        var originalChild = originalVariables[0];
        var untouchedChild = originalVariables[1];
        var draftProfile = originalProfile.Clone();
        draftProfile.Name = "Uncommitted outer name";
        Assert.AreNotSame(originalProfile, draftProfile);
        Assert.AreNotSame(originalVariables, draftProfile.Variables);
        Assert.AreSame(originalChild, draftProfile.Variables[0]);
        Assert.AreSame(untouchedChild, draftProfile.Variables[1]);
        var editedChild = draftProfile.Variables[0].Clone();
        editedChild.Values = "%EDITED%;;tail";
        fixture.Environment.Operations.Clear();
        Assert.AreEqual(0, fixture.Work.ActionCount);
        Assert.AreEqual(0, fixture.Work.SaveCount);
        Assert.IsTrue(fixture.Work.IsQuiescent);

        vm.EditVariable(draftProfile.Variables[0], editedChild, draftProfile);

        Assert.AreEqual("%EDITED%;;tail", originalChild.Values);
        Assert.AreNotSame(originalChild, editedChild);
        Assert.AreEqual(1, fixture.Work.ActionCount);
        Assert.AreEqual(1, fixture.Work.SaveCount);
        CollectionAssert.AreEqual(new[] { VariablesSetType.Profile }, fixture.Telemetry.Changed.ToArray());
        Assert.AreEqual(baseline, files.Files[service.ProfilesJsonFilePath]);
        Assert.AreEqual(1, files.Writes.Count);
        await fixture.Work.CompleteActionAsync(0);
        fixture.Work.DrainRefreshes();
        await fixture.Work.CompleteSaveAsync(0);

        Assert.AreSame(profiles, vm.Profiles);
        Assert.AreSame(originalProfile, vm.Profiles.Single());
        Assert.AreSame(originalVariables, originalProfile.Variables);
        CollectionAssert.AreEqual(new[] { originalChild, untouchedChild }, originalProfile.Variables.ToArray());
        Assert.AreSame(originalChild, draftProfile.Variables[0]);
        Assert.AreEqual((seed.Id, "Inactive", false), (originalProfile.Id, originalProfile.Name, originalProfile.IsEnabled));
        Assert.AreEqual(("PATH", "%EDITED%;;tail"), (originalChild.Name, originalChild.Values));
        CollectionAssert.AreEqual(new[] { "%EDITED%", string.Empty, "tail" }, originalChild.ValuesList.Select(row => row.Text).ToArray());
        Assert.AreEqual(("KEEP", "untouched"), (untouchedChild.Name, untouchedChild.Values));
        Assert.IsNull(vm.AppliedProfile);
        Assert.AreEqual(1, fixture.Work.ActionCount);
        Assert.AreEqual(1, fixture.Work.SaveCount);
        Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
        CollectionAssert.AreEqual(new[] { VariablesSetType.Profile }, fixture.Telemetry.Changed.ToArray());
        Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
        Assert.AreEqual(0, fixture.Environment.Notifications.Count);
        Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation == "Write" || operation.Operation == "Delete"));
        Assert.AreEqual(1, fixture.Environment.User.Count);
        Assert.AreEqual("user-sentinel", fixture.Environment.User["Unrelated"]);
        Assert.AreEqual(1, fixture.Environment.Machine.Count);
        Assert.AreEqual("machine-sentinel", fixture.Environment.Machine["Unrelated"]);
        Assert.AreEqual(2, files.Writes.Count);
        Assert.AreEqual(baseline, files.Writes[0].Contents);
        var persisted = files.Writes[1].Contents!;
        Assert.AreEqual(baseline.Replace("a;b;;%ROOT%", "%EDITED%;;tail", StringComparison.Ordinal), persisted);
        Assert.AreEqual(service.ProfilesJsonFilePath, files.Writes[1].Path);
        Assert.AreEqual(persisted, files.Files[service.ProfilesJsonFilePath]);
        using var json = JsonDocument.Parse(persisted);
        Assert.AreEqual(1, json.RootElement.GetArrayLength());
        var saved = json.RootElement[0];
        Assert.AreEqual(seed.Id, saved.GetProperty("Id").GetGuid());
        Assert.AreEqual("Inactive", saved.GetProperty("Name").GetString());
        Assert.IsFalse(saved.GetProperty("IsEnabled").GetBoolean());
        CollectionAssert.AreEqual(
            new[] { ("PATH", "%EDITED%;;tail"), ("KEEP", "untouched") },
            saved.GetProperty("Variables").EnumerateArray().Select(variable => (variable.GetProperty("Name").GetString(), variable.GetProperty("Values").GetString())).ToArray());
        using var oldJson = JsonDocument.Parse(baseline);
        Assert.AreEqual("a;b;;%ROOT%", oldJson.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
        Assert.AreEqual(0, fixture.Logger.Entries.Count);
        Assert.IsTrue(fixture.Work.IsQuiescent);

        vm.UpdateProfile(draftProfile);

        Assert.AreSame(profiles, vm.Profiles);
        Assert.AreSame(originalProfile, vm.Profiles.Single());
        Assert.AreEqual((seed.Id, draftProfile.Name, false), (originalProfile.Id, originalProfile.Name, originalProfile.IsEnabled));
        Assert.AreSame(draftProfile.Variables, originalProfile.Variables);
        Assert.AreNotSame(originalVariables, originalProfile.Variables);
        CollectionAssert.AreEqual(new[] { originalChild, untouchedChild }, originalProfile.Variables.ToArray());
        Assert.AreEqual(persisted, files.Files[service.ProfilesJsonFilePath]);
        Assert.AreEqual(2, files.Writes.Count);

        // Name, dependent Valid, Variables and the explicit update each schedule a save.
        Assert.AreEqual(5, fixture.Work.SaveCount);
        var outerJson = persisted.Replace("Inactive", draftProfile.Name, StringComparison.Ordinal);
        for (var saveIndex = 1; saveIndex < 5; saveIndex++)
        {
            await fixture.Work.CompleteSaveAsync(saveIndex);
            Assert.AreEqual(saveIndex + 2, files.Writes.Count);
            Assert.AreEqual(service.ProfilesJsonFilePath, files.Writes[^1].Path);
            Assert.AreEqual(outerJson, files.Writes[^1].Contents);
            Assert.AreEqual(outerJson, files.Files[service.ProfilesJsonFilePath]);
        }

        using var outerDocument = JsonDocument.Parse(files.Files[service.ProfilesJsonFilePath]);
        Assert.AreEqual(1, outerDocument.RootElement.GetArrayLength());
        var savedOuter = outerDocument.RootElement[0];
        Assert.AreEqual(seed.Id, savedOuter.GetProperty("Id").GetGuid());
        Assert.AreEqual(draftProfile.Name, savedOuter.GetProperty("Name").GetString());
        Assert.IsFalse(savedOuter.GetProperty("IsEnabled").GetBoolean());
        CollectionAssert.AreEqual(
            new[] { ("PATH", "%EDITED%;;tail"), ("KEEP", "untouched") },
            savedOuter.GetProperty("Variables").EnumerateArray().Select(variable => (variable.GetProperty("Name").GetString(), variable.GetProperty("Values").GetString())).ToArray());
        Assert.AreEqual(baseline, files.Writes[0].Contents);
        Assert.AreEqual(persisted, files.Writes[1].Contents);
        Assert.AreEqual("Inactive", saved.GetProperty("Name").GetString());
        Assert.AreEqual("a;b;;%ROOT%", oldJson.RootElement[0].GetProperty("Variables")[0].GetProperty("Values").GetString());
        Assert.AreEqual(("PATH", "%EDITED%;;tail"), (originalChild.Name, originalChild.Values));
        Assert.AreEqual(("KEEP", "untouched"), (untouchedChild.Name, untouchedChild.Values));
        Assert.AreEqual(1, fixture.Work.ActionCount);
        Assert.AreEqual(1, fixture.Work.RefreshReceiptCount);
        CollectionAssert.AreEqual(new[] { VariablesSetType.Profile }, fixture.Telemetry.Changed.ToArray());
        Assert.AreEqual(0, fixture.Telemetry.Enabled.Count);
        Assert.IsNull(vm.AppliedProfile);
        Assert.AreEqual(0, fixture.Environment.Notifications.Count);
        Assert.IsFalse(fixture.Environment.Operations.Any(operation => operation.Operation == "Write" || operation.Operation == "Delete"));
        Assert.AreEqual(1, fixture.Environment.User.Count);
        Assert.AreEqual("user-sentinel", fixture.Environment.User["Unrelated"]);
        Assert.AreEqual(1, fixture.Environment.Machine.Count);
        Assert.AreEqual("machine-sentinel", fixture.Environment.Machine["Unrelated"]);
        Assert.AreEqual(0, fixture.Logger.Entries.Count);
        Assert.IsTrue(fixture.Work.IsQuiescent);
    }

    private static string? ActOnRow(string action, Variable draft, Variable.ValuesListItem? selected)
    {
        return action switch
        {
            "up" => VariableEditorState.MoveUp(draft, selected!),
            "down" => VariableEditorState.MoveDown(draft, selected!),
            "delete" => VariableEditorState.DeleteEntry(draft, selected!),
            "before" => VariableEditorState.InsertBefore(draft, selected!),
            "after" => VariableEditorState.InsertAfter(draft, selected!),
            "commit" => VariableEditorState.CommitEntryText(draft, selected!, "changed"),
            _ => throw new ArgumentException("Unknown action.", nameof(action)),
        };
    }
}
