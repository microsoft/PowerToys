// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class ClipboardVariableTextTests
{
    [TestMethod]
    public void Label_EveryKindOfClipboardVariable_HasItsOwnName()
    {
        ClipboardPart[] parts = [ClipboardPart.All, ClipboardPart.Email, ClipboardPart.Link, ClipboardPart.Phone, ClipboardPart.Number];
        bool[] copies = [false, true];
        var labels = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pickCopy in copies)
        {
            foreach (var part in parts)
            {
                var label = ClipboardVariableText.Label(new ClipboardVariable(pickCopy, part));

                Assert.IsFalse(string.IsNullOrWhiteSpace(label));
                Assert.IsFalse(label.Contains('{', StringComparison.Ordinal), label);
                Assert.IsTrue(labels.Add(label), $"Two clipboard variables are both called \"{label}\".");
            }
        }

        Assert.AreEqual(10, labels.Count);
    }

    [TestMethod]
    [DataRow(false, (int)ClipboardPart.All, "Clipboard")]
    [DataRow(false, (int)ClipboardPart.Phone, "Phone number from clipboard")]
    [DataRow(true, (int)ClipboardPart.All, "Copy you pick")]
    [DataRow(true, (int)ClipboardPart.Link, "Link from a copy you pick")]
    public void Label_Variable_UsesTheSnippetEditorsWords(bool pickCopy, int part, string expected)
        => Assert.AreEqual(expected, ClipboardVariableText.Label(new ClipboardVariable(pickCopy, (ClipboardPart)part)));

    [TestMethod]
    public void NothingFound_Gaps_NamesWhatWasntThereForOneSpotAndSaysSomeForMore()
    {
        ClipboardGap[] none = [];
        ClipboardGap[] oneLink = [new(ClipboardPart.Link, ClipboardGapReason.NotFound)];
        ClipboardGap[] wholeCopy = [new(ClipboardPart.All, ClipboardGapReason.NotFound)];
        ClipboardGap[] twoEmails = [new(ClipboardPart.Email, ClipboardGapReason.NotFound), new(ClipboardPart.Email, ClipboardGapReason.NotFound)];

        Assert.IsNull(ClipboardVariableText.NothingFound(none));
        Assert.AreEqual("No link found in what you copied, so that spot was left empty.", ClipboardVariableText.NothingFound(oneLink));
        Assert.AreEqual("There's no text on the clipboard, so that spot was left empty.", ClipboardVariableText.NothingFound(wholeCopy));
        Assert.AreEqual("Some parts weren't found in what you copied, so those spots were left empty.", ClipboardVariableText.NothingFound(twoEmails));
    }

    [TestMethod]
    [DataRow((int)ClipboardPart.Email, "Pick the email address to insert")]
    [DataRow((int)ClipboardPart.Link, "Pick the link to insert")]
    [DataRow((int)ClipboardPart.Phone, "Pick the phone number to insert")]
    [DataRow((int)ClipboardPart.Number, "Pick the number to insert")]
    public void PickTitle_Part_AsksForItAsTextExpandersWindowDoes(int part, string expected)
        => Assert.AreEqual(expected, ClipboardVariableText.PickTitle((ClipboardPart)part));
}
