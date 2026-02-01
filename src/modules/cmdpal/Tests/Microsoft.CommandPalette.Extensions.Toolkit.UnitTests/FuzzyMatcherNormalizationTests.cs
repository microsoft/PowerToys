// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public sealed class FuzzyMatcherNormalizationTests
{
    [TestMethod]
    public void Normalization_ShouldBeLengthPreserving_GermanEszett()
    {
        // "Straße" (6 chars)
        // Standard "SS" expansion would change length to 7.
        // Our normalizer must preserve length.
        var input = "Straße";
        var expectedLength = input.Length;

        // Case 1: Remove Diacritics = true
        var normalized = Fold(input, removeDiacritics: true);
        Assert.AreEqual(expectedLength, normalized.Length, "Normalization (removeDiacritics=true) must be length preserving for 'Straße'");

        // Verify expected mapping: ß -> ß (length 1)
        Assert.AreEqual("STRAßE", normalized);

        // Case 2: Remove Diacritics = false
        var normalizedKeep = Fold(input, removeDiacritics: false);
        Assert.AreEqual(expectedLength, normalizedKeep.Length, "Normalization (removeDiacritics=false) must be length preserving for 'Straße'");

        // ß uppercases to ß in invariant culture (length 1)
        Assert.AreEqual("STRAßE", normalizedKeep);
    }

    [TestMethod]
    public void Normalization_ShouldBeLengthPreserving_CommonDiacritics()
    {
        var input = "Crème Brûlée";
        var expected = "CREME BRULEE";

        var normalized = Fold(input, removeDiacritics: true);

        Assert.AreEqual(input.Length, normalized.Length);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalization_ShouldBeLengthPreserving_MixedComposed()
    {
        // "Ångström" -> A + ring, o + umlaut
        var input = "Ångström";
        var expected = "ANGSTROM";

        var normalized = Fold(input, removeDiacritics: true);

        Assert.AreEqual(input.Length, normalized.Length);
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    public void Normalization_ShouldNormalizeSlashes()
    {
        var input = @"Folder\File.txt";
        var expected = "FOLDER/FILE.TXT";

        var normalized = Fold(input, removeDiacritics: true);

        Assert.AreEqual(input.Length, normalized.Length);
        Assert.AreEqual(expected, normalized);
    }

    private string Fold(string input, bool removeDiacritics)
    {
        return FuzzyStringMatcher.Folding.FoldForComparison(input, removeDiacritics);
    }
}
