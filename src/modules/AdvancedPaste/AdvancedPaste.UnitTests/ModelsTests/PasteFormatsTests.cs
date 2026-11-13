// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using AdvancedPaste.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ModelsTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class PasteFormatsTests
{
    // PasteFormats values are written to telemetry as numbers, so they must never change.
    // When adding a member, append it here with the next unused value instead of editing existing entries.
    private static readonly Dictionary<PasteFormats, int> ExpectedValues = new()
    {
        [PasteFormats.PlainText] = 0,
        [PasteFormats.Markdown] = 1,
        [PasteFormats.Json] = 2,
        [PasteFormats.FixSpellingAndGrammar] = 3,
        [PasteFormats.ImageToText] = 4,
        [PasteFormats.PasteAsTxtFile] = 5,
        [PasteFormats.PasteAsPngFile] = 6,
        [PasteFormats.PasteAsHtmlFile] = 7,
        [PasteFormats.TranscodeToMp3] = 8,
        [PasteFormats.TranscodeToMp4] = 9,
        [PasteFormats.KernelQuery] = 10,
        [PasteFormats.CustomTextTransformation] = 11,
        [PasteFormats.PasteAsJpgFile] = 12,
    };

    [TestMethod]
    public void PasteFormatValuesAreStable()
    {
        foreach (var (format, expectedValue) in ExpectedValues)
        {
            Assert.AreEqual(expectedValue, (int)format, $"Numeric value of {format} changed; telemetry relies on it staying the same.");
        }
    }

    [TestMethod]
    public void EveryPasteFormatHasAPinnedValue()
    {
        var unpinned = Enum.GetValues<PasteFormats>().Where(format => !ExpectedValues.ContainsKey(format)).ToList();

        Assert.IsEmpty(unpinned, $"Add these members to {nameof(ExpectedValues)} with new, unused values: {string.Join(", ", unpinned)}");
    }

    [TestMethod]
    public void PasteFormatValuesAreUnique()
    {
        var values = Enum.GetValues<PasteFormats>().Select(format => (int)format).ToList();

        Assert.HasCount(values.Count, values.Distinct(), "Two PasteFormats members share a numeric value.");
    }
}
