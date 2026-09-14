// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.UnitTests;

[TestClass]
public class PassthroughTranslationProviderTests
{
    [TestMethod]
    public void ProviderProperties_AreLabeledExplicitly()
    {
        var provider = new PassthroughTranslationProvider();
        Assert.AreEqual("passthrough-prototype", provider.ProviderId);
        Assert.IsTrue(provider.DisplayName.Contains("Prototype", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task TranslateAsync_EmptyLines_ReturnsEmptySuccess()
    {
        var provider = new PassthroughTranslationProvider();
        var request = new TranslationRequest(Array.Empty<TranslationLine>());

        var result = await provider.TranslateAsync(request);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.TranslatedLines.Count);
    }

    [TestMethod]
    public async Task TranslateAsync_PrependsPrefixToEachLine()
    {
        var provider = new PassthroughTranslationProvider("[Trans] ");
        var lines = new List<TranslationLine>
        {
            new("Hello world", new PhysicalRect(10, 20, 100, 30)),
            new("PowerToys Screen Translator", new PhysicalRect(10, 60, 200, 30)),
        };

        var request = new TranslationRequest(lines);
        var result = await provider.TranslateAsync(request);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.TranslatedLines.Count);
        Assert.AreEqual("Hello world", result.TranslatedLines[0].OriginalText);
        Assert.AreEqual("[Trans] Hello world", result.TranslatedLines[0].TranslatedText);
        Assert.AreEqual(10.0, result.TranslatedLines[0].BoundingBox.X);

        Assert.AreEqual("PowerToys Screen Translator", result.TranslatedLines[1].OriginalText);
        Assert.AreEqual("[Trans] PowerToys Screen Translator", result.TranslatedLines[1].TranslatedText);
    }

    [TestMethod]
    public async Task TranslateAsync_HonorsCancellationToken()
    {
        var provider = new PassthroughTranslationProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new TranslationRequest(new List<TranslationLine>
        {
            new("Sample", new PhysicalRect(0, 0, 10, 10)),
        });

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
        {
            await provider.TranslateAsync(request, cts.Token);
        });
    }
}
