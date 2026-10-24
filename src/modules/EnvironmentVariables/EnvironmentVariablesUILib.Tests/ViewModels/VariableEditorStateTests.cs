// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using EnvironmentVariablesUILib.Models;
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
