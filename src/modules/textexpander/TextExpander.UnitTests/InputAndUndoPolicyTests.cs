// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InputAndUndoPolicyTests
{
    [TestMethod]
    public void PendingExpansionCarriesTypedTextForOneStepUndo()
    {
        var expansion = new PendingExpansion("btw", "btw", "by the way ", IntPtr.Zero, TypedText: "btw ");
        Assert.AreEqual("btw ", expansion.TypedText);
        Assert.IsFalse(expansion.IsUndo);

        var undo = new PendingExpansion("btw ", string.Empty, "by the way ", IntPtr.Zero, IsUndo: true);
        Assert.IsTrue(undo.IsUndo);
    }

    [TestMethod]
    public void CursorMarkerParsingUsesDeletableUnitsAfterMarker()
    {
        string text = "abc$|$de😀";
        int marker = text.IndexOf("$|$", StringComparison.Ordinal);
        int left = Injector.CountDeletableUnits(text[(marker + 3)..]);
        Assert.AreEqual(3, left);
        Assert.AreEqual("abcde😀", text.Remove(marker, 3));
    }

    [TestMethod]
    public void IntegrityCeilingDependsOnElevationAndUserWritableConfigLocation()
    {
        string profileFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TextExpander", "snippets.txt");
        Assert.AreEqual(InputTarget.MediumIntegrity, InputTarget.CeilingFor(elevated: true, profileFile));
        Assert.AreEqual(uint.MaxValue, InputTarget.CeilingFor(elevated: false, profileFile));
        Assert.AreEqual(uint.MaxValue, InputTarget.CeilingFor(elevated: true, @"C:\Program Files\PowerToys\snippets.txt"));
        Assert.IsTrue(InputTarget.IsUserWritableLocation("::\0 not a path"));
    }

    [TestMethod]
    public void InputTargetClassifiesProfileAndProtectedPathsForElevationCap()
    {
        string profileFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TextExpander", "snippets.txt");
        Assert.AreEqual(InputTarget.MediumIntegrity, InputTarget.CeilingFor(elevated: true, profileFile));
        Assert.AreEqual(uint.MaxValue, InputTarget.CeilingFor(elevated: true, @"C:\Program Files\PowerToys\snippets.txt"));
        Assert.AreEqual(uint.MaxValue, InputTarget.CeilingFor(elevated: false, profileFile));
        Assert.IsTrue(InputTarget.IsUserWritableLocation("::\0 not a path"));
    }

    [TestMethod]
    public void CursorMarkerLeavesCleanTextAndCountsSurrogatePairAsTwoDeletes()
    {
        string text = "abc$|$de😀";
        int marker = text.IndexOf("$|$", StringComparison.Ordinal);
        Assert.AreEqual(3, Injector.CountDeletableUnits(text[(marker + 3)..]));
        Assert.AreEqual("abcde😀", text.Remove(marker, 3));
    }
}
