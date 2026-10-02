// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

using AdvancedPaste.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ModelsTests;

[TestClass]
public sealed class PasteFormatsTests
{
    [TestMethod]
    public void PersistedFormatIdsRemainStable()
    {
        Assert.AreEqual(0, (int)PasteFormats.PlainText);
        Assert.AreEqual(1, (int)PasteFormats.Markdown);
        Assert.AreEqual(2, (int)PasteFormats.Json);
        Assert.AreEqual(3, (int)PasteFormats.FixSpellingAndGrammar);
        Assert.AreEqual(4, (int)PasteFormats.ImageToText);
        Assert.AreEqual(5, (int)PasteFormats.PasteAsTxtFile);
        Assert.AreEqual(6, (int)PasteFormats.PasteAsPngFile);
        Assert.AreEqual(7, (int)PasteFormats.PasteAsHtmlFile);
        Assert.AreEqual(8, (int)PasteFormats.TranscodeToMp3);
        Assert.AreEqual(9, (int)PasteFormats.TranscodeToMp4);
        Assert.AreEqual(10, (int)PasteFormats.KernelQuery);
        Assert.AreEqual(11, (int)PasteFormats.CustomTextTransformation);
    }

    [DataTestMethod]
    [DataRow(PasteFormats.LowerCase, 13)]
    [DataRow(PasteFormats.UpperCase, 14)]
    [DataRow(PasteFormats.TitleCase, 15)]
    [DataRow(PasteFormats.SentenceCase, 16)]
    [DataRow(PasteFormats.ToggleCase, 17)]
    [DataRow(PasteFormats.CamelCase, 18)]
    [DataRow(PasteFormats.PascalCase, 19)]
    [DataRow(PasteFormats.SnakeCase, 20)]
    [DataRow(PasteFormats.ScreamingSnakeCase, 21)]
    [DataRow(PasteFormats.KebabCase, 22)]
    public void TextCasePersistedFormatIdsRemainStable(PasteFormats format, int expectedPersistedValue)
    {
        Assert.AreEqual(expectedPersistedValue, (int)format);
    }

    [DataTestMethod]
    [DataRow(PasteFormats.LowerCase, "lower-case")]
    [DataRow(PasteFormats.UpperCase, "upper-case")]
    [DataRow(PasteFormats.TitleCase, "title-case")]
    [DataRow(PasteFormats.SentenceCase, "sentence-case")]
    [DataRow(PasteFormats.ToggleCase, "toggle-case")]
    [DataRow(PasteFormats.CamelCase, "camel-case")]
    [DataRow(PasteFormats.PascalCase, "pascal-case")]
    [DataRow(PasteFormats.SnakeCase, "snake-case")]
    [DataRow(PasteFormats.ScreamingSnakeCase, "screaming-snake-case")]
    [DataRow(PasteFormats.KebabCase, "kebab-case")]
    public void TextCaseFormatsUseExpectedAdditionalActionIpcKeys(PasteFormats format, string expectedIpcKey)
    {
        Assert.AreEqual(expectedIpcKey, PasteFormat.MetadataDict[format].IPCKey);
    }

    [DataTestMethod]
    [DataRow(PasteFormats.LowerCase, "LowerCase", "lower-case")]
    [DataRow(PasteFormats.UpperCase, "UpperCase", "upper-case")]
    [DataRow(PasteFormats.TitleCase, "TitleCase", "title-case")]
    [DataRow(PasteFormats.SentenceCase, "SentenceCase", "sentence-case")]
    [DataRow(PasteFormats.ToggleCase, "ToggleCase", "toggle-case")]
    [DataRow(PasteFormats.CamelCase, "CamelCase", "camel-case")]
    [DataRow(PasteFormats.PascalCase, "PascalCase", "pascal-case")]
    [DataRow(PasteFormats.SnakeCase, "SnakeCase", "snake-case")]
    [DataRow(PasteFormats.ScreamingSnakeCase, "ScreamingSnakeCase", "screaming-snake-case")]
    [DataRow(PasteFormats.KebabCase, "KebabCase", "kebab-case")]
    public void TextCaseFormatsUseExpectedMetadata(PasteFormats format, string expectedResourceId, string expectedIpcKey)
    {
        var metadata = PasteFormat.MetadataDict[format];

        Assert.IsFalse(metadata.IsCoreAction);
        Assert.IsFalse(metadata.RequiresAIService);
        Assert.IsFalse(metadata.CanPreview);
        Assert.AreEqual(ClipboardFormat.Text, metadata.SupportedClipboardFormats);
        Assert.AreEqual(expectedResourceId, metadata.ResourceId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.ResourceId));
        Assert.AreEqual(expectedIpcKey, metadata.IPCKey);
        Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.IPCKey));
        Assert.IsNull(metadata.KernelFunctionDescription);
        Assert.IsFalse(metadata.RequiresPrompt);
        Assert.AreEqual("\uE8E9", metadata.IconGlyph);
    }

    [TestMethod]
    public void AllAdditionalActionIpcKeysAreUnique()
    {
        var duplicateKeys = PasteFormat.MetadataDict
            .Where(entry => entry.Value.IPCKey is not null)
            .GroupBy(entry => entry.Value.IPCKey)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(entry => entry.Key))}")
            .ToArray();

        Assert.AreEqual(0, duplicateKeys.Length, $"Duplicate PasteFormat IPC keys: {string.Join("; ", duplicateKeys)}");
    }

    [TestMethod]
    public void TextCaseAdditionalActionsAreDisabledByDefault()
    {
        var textCase = new AdvancedPasteTextCaseAction();

        Assert.IsTrue(textCase.IsShown);
        Assert.IsFalse(textCase.LowerCase.IsShown);
        Assert.IsFalse(textCase.UpperCase.IsShown);
        Assert.IsFalse(textCase.TitleCase.IsShown);
        Assert.IsFalse(textCase.SentenceCase.IsShown);
        Assert.IsFalse(textCase.ToggleCase.IsShown);
        Assert.IsFalse(textCase.CamelCase.IsShown);
        Assert.IsFalse(textCase.PascalCase.IsShown);
        Assert.IsFalse(textCase.SnakeCase.IsShown);
        Assert.IsFalse(textCase.ScreamingSnakeCase.IsShown);
        Assert.IsFalse(textCase.KebabCase.IsShown);
    }
}
