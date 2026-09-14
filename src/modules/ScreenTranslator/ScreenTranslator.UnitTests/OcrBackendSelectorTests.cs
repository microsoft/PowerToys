// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenTranslator.Core.Ocr;
using ScreenTranslator.Core.Translation;
using Windows.Graphics.Imaging;

namespace ScreenTranslator.UnitTests;

[TestClass]
public class OcrBackendSelectorTests
{
    private sealed class FakeCapabilityDetector : IOcrCapabilityDetector
    {
        public bool Supported { get; set; }

        public bool Ready { get; set; }

        public bool EnsureSucceeds { get; set; }

        public bool ThrowOnProbe { get; set; }

        public bool IsWindowsAiSupported()
        {
            if (ThrowOnProbe)
            {
                throw new InvalidOperationException("Simulated hardware probe failure");
            }

            return Supported;
        }

        public bool IsWindowsAiReady()
        {
            if (ThrowOnProbe)
            {
                throw new InvalidOperationException("Simulated ready check failure");
            }

            return Ready;
        }

        public Task<bool> EnsureWindowsAiReadyAsync()
        {
            if (ThrowOnProbe)
            {
                throw new InvalidOperationException("Simulated download failure");
            }

            return Task.FromResult(EnsureSucceeds);
        }
    }

    private sealed class FakeOcrBackend : IOcrBackend
    {
        public string BackendName { get; }

        public bool IsAvailable => true;

        public FakeOcrBackend(string name)
        {
            BackendName = name;
        }

        public Task<IReadOnlyList<TranslationLine>> RecognizeTextAsync(
            SoftwareBitmap bitmap,
            PhysicalRect capturedRegionPhysical,
            string? sourceLanguageTag = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TranslationLine>>(new List<TranslationLine>
            {
                new("Sample", capturedRegionPhysical),
            });
        }
    }

    [TestMethod]
    public async Task Selector_PrefersWindowsAi_WhenReady()
    {
        var detector = new FakeCapabilityDetector { Supported = true, Ready = true, EnsureSucceeds = true };
        var aiBackend = new FakeOcrBackend("AI Backend");
        var legacyBackend = new FakeOcrBackend("Legacy Backend");

        var selector = new OcrBackendSelector(detector, aiBackend, legacyBackend);
        var backend = await selector.GetOrSelectBackendAsync();

        Assert.AreEqual("AI Backend", backend.BackendName);
    }

    [TestMethod]
    public async Task Selector_PreparesWindowsAi_WhenSupportedButNotReady()
    {
        var detector = new FakeCapabilityDetector { Supported = true, Ready = false, EnsureSucceeds = true };
        var aiBackend = new FakeOcrBackend("AI Backend");
        var legacyBackend = new FakeOcrBackend("Legacy Backend");

        var selector = new OcrBackendSelector(detector, aiBackend, legacyBackend);
        var backend = await selector.GetOrSelectBackendAsync();

        Assert.AreEqual("AI Backend", backend.BackendName);
    }

    [TestMethod]
    public async Task Selector_FallsBackToLegacy_WhenNotSupported()
    {
        var detector = new FakeCapabilityDetector { Supported = false, Ready = false, EnsureSucceeds = false };
        var aiBackend = new FakeOcrBackend("AI Backend");
        var legacyBackend = new FakeOcrBackend("Legacy Backend");

        var selector = new OcrBackendSelector(detector, aiBackend, legacyBackend);
        var backend = await selector.GetOrSelectBackendAsync();

        Assert.AreEqual("Legacy Backend", backend.BackendName);
    }

    [TestMethod]
    public async Task Selector_FallsBackToLegacy_WhenEnsureFails()
    {
        var detector = new FakeCapabilityDetector { Supported = true, Ready = false, EnsureSucceeds = false };
        var aiBackend = new FakeOcrBackend("AI Backend");
        var legacyBackend = new FakeOcrBackend("Legacy Backend");

        var selector = new OcrBackendSelector(detector, aiBackend, legacyBackend);
        var backend = await selector.GetOrSelectBackendAsync();

        Assert.AreEqual("Legacy Backend", backend.BackendName);
    }

    [TestMethod]
    public async Task Selector_HandlesExceptionsGracefully_AndFallsBack()
    {
        var detector = new FakeCapabilityDetector { ThrowOnProbe = true };
        var aiBackend = new FakeOcrBackend("AI Backend");
        var legacyBackend = new FakeOcrBackend("Legacy Backend");

        var selector = new OcrBackendSelector(detector, aiBackend, legacyBackend);
        var backend = await selector.GetOrSelectBackendAsync();

        Assert.AreEqual("Legacy Backend", backend.BackendName);
    }
}
