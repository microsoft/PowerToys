// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PasteTarget = Microsoft.PowerToys.TextExpander.Editor.Helpers.TextExpanderReplacementEditor.PasteTarget;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

[TestClass]
public sealed class TextExpanderPasteTests
{
    [TestMethod]
    public async Task DelayedPasteUsesTheOriginalUnchangedSelection()
    {
        var target = new PasteTarget(new object(), (1, 3), 0);

        var pasted = await CompleteDelayedPasteAsync(target, target);

        Assert.AreEqual<(int Start, int End, string Text)?>((1, 3, "line one\nline two"), pasted);
    }

    [TestMethod]
    public async Task DelayedPasteCannotReplaceTheNewSelectionInTheSameDialog()
    {
        var target = new PasteTarget(new object(), (1, 3), 0);

        var pasted = await CompleteDelayedPasteAsync(target, target with { Selection = (4, 6), Revision = 1 });

        Assert.IsNull(pasted);
    }

    [TestMethod]
    public async Task DelayedPasteIsDiscardedAfterAnEditEvenWhenTheSelectionIsTheSame()
    {
        var target = new PasteTarget(new object(), (1, 3), 0);

        var pasted = await CompleteDelayedPasteAsync(target, target with { Revision = 1 });

        Assert.IsNull(pasted);
    }

    [TestMethod]
    public async Task DelayedPasteIsDiscardedWhenAPromptDraftBlocksTheTarget()
    {
        var target = new PasteTarget(new object(), (1, 3), 0);

        var pasted = await CompleteDelayedPasteAsync(target, null);

        Assert.IsNull(pasted);
    }

    [TestMethod]
    public async Task DelayedPasteIsDiscardedWhenAnotherDialogReusesTheEditor()
    {
        var target = new PasteTarget(new object(), (1, 3), 0);

        var pasted = await CompleteDelayedPasteAsync(target, target with { Edit = new object() });

        Assert.IsNull(pasted);
    }

    private static async Task<(int Start, int End, string Text)?> CompleteDelayedPasteAsync(PasteTarget original, PasteTarget? updated)
    {
        var clipboard = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        PasteTarget? current = original;
        (int Start, int End, string Text)? pasted = null;
        Task paste = TextExpanderReplacementEditor.PasteAtUnchangedTargetAsync(
            () => current,
            () => clipboard.Task,
            (start, end, text) => pasted = (start, end, text));

        Assert.IsFalse(paste.IsCompleted);
        current = updated;
        clipboard.SetResult("line one\r\nline two");
        await paste;

        return pasted;
    }
}
