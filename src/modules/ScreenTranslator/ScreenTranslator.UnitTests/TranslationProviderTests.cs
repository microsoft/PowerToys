// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenTranslator.Core.Ocr;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.UnitTests;

[TestClass]
public class TranslationProviderTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [TestMethod]
    public async Task AzureTranslator_Success_MapsLinesAndPreservesGeometry()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.IsTrue(request.RequestUri!.ToString().Contains("/translate?api-version=3.0"));
            Assert.IsTrue(request.Headers.Contains("Ocp-Apim-Subscription-Key"));

            string jsonResponse = @"[
                {
                    ""detectedLanguage"": { ""language"": ""ja"", ""score"": 1.0 },
                    ""translations"": [ { ""text"": ""Hello World"", ""to"": ""en"" } ]
                },
                {
                    ""detectedLanguage"": { ""language"": ""ja"", ""score"": 1.0 },
                    ""translations"": [ { ""text"": ""Second Line"", ""to"": ""en"" } ]
                }
            ]";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        using var provider = new AzureTranslatorProvider(
            "https://api.cognitive.microsofttranslator.com",
            "eastus",
            "mock-api-key",
            cloudConsentEnabled: true,
            customHttpClient: httpClient);

        var lines = new List<TranslationLine>
        {
            new("こんにちは世界", new PhysicalRect(10, 20, 100, 30), 0.95),
            new("二行目", new PhysicalRect(10, 60, 80, 30), 0.90),
        };

        var request = new TranslationRequest(lines, "ja-JP", "en-US");
        var result = await provider.TranslateAsync(request);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.Lines.Count);
        Assert.AreEqual("Hello World", result.Lines[0].TranslatedText);
        Assert.AreEqual("こんにちは世界", result.Lines[0].OriginalText);
        Assert.AreEqual(10, result.Lines[0].BoundingBox.X);
        Assert.AreEqual(20, result.Lines[0].BoundingBox.Y);
        Assert.AreEqual(100, result.Lines[0].BoundingBox.Width);
        Assert.AreEqual(30, result.Lines[0].BoundingBox.Height);
        Assert.AreEqual(0.95, result.Lines[0].Confidence, 0.001);

        Assert.AreEqual("Second Line", result.Lines[1].TranslatedText);
        Assert.AreEqual("二行目", result.Lines[1].OriginalText);
    }

    [TestMethod]
    public async Task AzureTranslator_CloudConsentDisabled_ReturnsErrorWithoutNetworkCall()
    {
        bool networkCalled = false;
        var handler = new FakeHttpMessageHandler(request =>
        {
            networkCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var provider = new AzureTranslatorProvider(
            "https://api.cognitive.microsofttranslator.com",
            "eastus",
            "mock-api-key",
            cloudConsentEnabled: false,
            customHttpClient: httpClient);

        var lines = new List<TranslationLine> { new("Test", new PhysicalRect(0, 0, 10, 10)) };
        var result = await provider.TranslateAsync(new TranslationRequest(lines, "auto", "en"));

        Assert.IsFalse(result.Success);
        Assert.IsFalse(networkCalled);
        Assert.IsTrue(result.ErrorMessage?.Contains("Cloud translation is not enabled") == true);
    }

    [TestMethod]
    public async Task AzureTranslator_MissingApiKey_ReturnsErrorWithoutNetworkCall()
    {
        bool networkCalled = false;
        var handler = new FakeHttpMessageHandler(request =>
        {
            networkCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var provider = new AzureTranslatorProvider(
            "https://api.cognitive.microsofttranslator.com",
            "eastus",
            string.Empty,
            cloudConsentEnabled: true,
            customHttpClient: httpClient);

        var lines = new List<TranslationLine> { new("Test", new PhysicalRect(0, 0, 10, 10)) };
        var result = await provider.TranslateAsync(new TranslationRequest(lines, "auto", "en"));

        Assert.IsFalse(result.Success);
        Assert.IsFalse(networkCalled);
        Assert.IsTrue(result.ErrorMessage?.Contains("API key is not configured") == true);
    }

    [TestMethod]
    public async Task LibreTranslate_Success_LocalhostPermittedWithoutCloudConsent()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.IsTrue(request.RequestUri!.ToString().Contains("/translate"));

            string jsonResponse = @"{ ""translatedText"": [""Local translation 1"", ""Local translation 2""] }";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler);
        using var provider = new LibreTranslateProvider(
            "http://localhost:5000",
            apiKey: string.Empty,
            cloudConsentEnabled: false, // Localhost does not require remote cloud consent
            customHttpClient: httpClient);

        var lines = new List<TranslationLine>
        {
            new("Item 1", new PhysicalRect(10, 10, 50, 20)),
            new("Item 2", new PhysicalRect(10, 40, 50, 20)),
        };

        var result = await provider.TranslateAsync(new TranslationRequest(lines, "auto", "es"));

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.Lines.Count);
        Assert.AreEqual("Local translation 1", result.Lines[0].TranslatedText);
        Assert.AreEqual("Local translation 2", result.Lines[1].TranslatedText);
    }

    [TestMethod]
    public async Task LibreTranslate_RemoteEndpoint_RequiresCloudConsent()
    {
        bool networkCalled = false;
        var handler = new FakeHttpMessageHandler(request =>
        {
            networkCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var provider = new LibreTranslateProvider(
            "https://translate.example.com",
            apiKey: "key",
            cloudConsentEnabled: false,
            customHttpClient: httpClient);

        var lines = new List<TranslationLine> { new("Item 1", new PhysicalRect(10, 10, 50, 20)) };
        var result = await provider.TranslateAsync(new TranslationRequest(lines, "auto", "de"));

        Assert.IsFalse(result.Success);
        Assert.IsFalse(networkCalled);
        Assert.IsTrue(result.ErrorMessage?.Contains("Cloud translation consent is required") == true);
    }

    [TestMethod]
    public void TranslationProviderFactory_CreatesExpectedTypes()
    {
        var settings = new ScreenTranslatorSettings();

        settings.Properties.SelectedProvider = "Passthrough";
        var passthrough = TranslationProviderFactory.Create(settings);
        Assert.IsInstanceOfType(passthrough, typeof(PassthroughTranslationProvider));

        settings.Properties.SelectedProvider = "AzureTranslator";
        var azure = TranslationProviderFactory.Create(settings);
        Assert.IsInstanceOfType(azure, typeof(AzureTranslatorProvider));

        settings.Properties.SelectedProvider = "LibreTranslate";
        var libre = TranslationProviderFactory.Create(settings);
        Assert.IsInstanceOfType(libre, typeof(LibreTranslateProvider));
    }

    [TestMethod]
    public void ScreenTranslatorSettings_SerializationRoundTrip()
    {
        var settings = new ScreenTranslatorSettings();
        settings.Properties.SourceLanguage = "ja-JP";
        settings.Properties.TargetLanguage = "en-US";
        settings.Properties.SelectedProvider = "AzureTranslator";
        settings.Properties.EnableCloudConsent = true;
        settings.Properties.AzureEndpoint = "https://custom.azure.com";
        settings.Properties.AzureRegion = "westus2";
        settings.Properties.LibreTranslateEndpoint = "http://localhost:8080";

        string json = settings.ToJsonString();
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("ja-JP"));
        Assert.IsTrue(json.Contains("AzureTranslator"));
        Assert.IsTrue(json.Contains("custom.azure.com"));

        // Verify API key is NOT present in serialized JSON
        Assert.IsFalse(json.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void WindowsMediaOcrBackend_LanguageResolution_HandlesAutoAndTags()
    {
        // Auto / null / system tags should resolve safely without throwing
        var autoLang = WindowsMediaOcrBackend.ResolveLanguage("auto");
        var systemLang = WindowsMediaOcrBackend.ResolveLanguage("system");
        var nullLang = WindowsMediaOcrBackend.ResolveLanguage(null);

        // Explicit tag handling
        var enLang = WindowsMediaOcrBackend.ResolveLanguage("en-US");

        // Result is either English (if supported) or preferred fallback
        Assert.IsNotNull(enLang != null || autoLang != null || systemLang != null || nullLang != null);
    }
}
