// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Cloud translation provider using Azure AI Translator REST API v3.
/// </summary>
public sealed class AzureTranslatorProvider : ITranslationProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly string _endpoint;
    private readonly string _region;
    private readonly string _apiKey;
    private readonly bool _cloudConsentEnabled;

    public string ProviderId => "AzureTranslator";

    public string DisplayName => "Azure AI Translator (Cloud v3)";

    public AzureTranslatorProvider(
        string endpoint,
        string region,
        string apiKey,
        bool cloudConsentEnabled,
        HttpClient? customHttpClient = null)
    {
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? "https://api.cognitive.microsofttranslator.com" : endpoint.Trim().TrimEnd('/');
        _region = region?.Trim() ?? string.Empty;
        _apiKey = apiKey?.Trim() ?? string.Empty;
        _cloudConsentEnabled = cloudConsentEnabled;

        if (customHttpClient != null)
        {
            _httpClient = customHttpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys-ScreenTranslator/1.0");
            _disposeClient = true;
        }
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines == null || request.Lines.Count == 0)
        {
            return new TranslationResult(Array.Empty<TranslatedLine>(), Success: true);
        }

        if (!_cloudConsentEnabled)
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: "Cloud translation is not enabled. Please enable 'Allow cloud translation services' in PowerToys Screen Translator settings.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: "Azure Translator API key is not configured. Please enter your API key in PowerToys Screen Translator settings.");
        }

        if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var endpointUri))
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: $"Invalid Azure Translator endpoint URL: {_endpoint}");
        }

        if (endpointUri.Scheme != Uri.UriSchemeHttps && !endpointUri.IsLoopback)
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: "Azure Translator endpoint must use HTTPS (or localhost for testing).");
        }

        try
        {
            var targetLang = NormalizeLanguageTag(request.TargetLanguage);
            var query = $"/translate?api-version=3.0&to={Uri.EscapeDataString(targetLang)}";

            var sourceLang = NormalizeLanguageTag(request.SourceLanguage);
            if (!string.IsNullOrEmpty(sourceLang) && !string.Equals(sourceLang, "auto", StringComparison.OrdinalIgnoreCase))
            {
                query += $"&from={Uri.EscapeDataString(sourceLang)}";
            }

            var requestUri = new Uri(endpointUri, query);

            var requestBodyList = new List<AzureTranslateRequestBodyItem>(request.Lines.Count);
            foreach (var line in request.Lines)
            {
                requestBodyList.Add(new AzureTranslateRequestBodyItem { Text = line.Text });
            }

            var jsonContent = JsonSerializer.Serialize(requestBodyList);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json"),
            };

            httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", _apiKey);
            if (!string.IsNullOrEmpty(_region))
            {
                httpRequest.Headers.Add("Ocp-Apim-Subscription-Region", _region);
            }

            using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogWarning($"Azure Translator request failed with HTTP {(int)httpResponse.StatusCode}");
                return new TranslationResult(
                    Array.Empty<TranslatedLine>(),
                    Success: false,
                    ErrorMessage: $"Azure Translator error (HTTP {(int)httpResponse.StatusCode}): {SanitizeErrorMessage(errorBody)}");
            }

            var responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var responseItems = await JsonSerializer.DeserializeAsync<List<AzureTranslateResponseItem>>(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (responseItems == null || responseItems.Count != request.Lines.Count)
            {
                return new TranslationResult(
                    Array.Empty<TranslatedLine>(),
                    Success: false,
                    ErrorMessage: "Azure Translator returned an unexpected response structure.");
            }

            var translatedLines = new List<TranslatedLine>(request.Lines.Count);
            for (int i = 0; i < request.Lines.Count; i++)
            {
                var originalLine = request.Lines[i];
                var responseItem = responseItems[i];
                var translatedText = originalLine.Text;

                if (responseItem.Translations != null && responseItem.Translations.Count > 0)
                {
                    translatedText = responseItem.Translations[0].Text ?? originalLine.Text;
                }

                translatedLines.Add(new TranslatedLine(
                    OriginalText: originalLine.Text,
                    TranslatedText: translatedText,
                    BoundingBox: originalLine.BoundingBox,
                    Confidence: originalLine.Confidence,
                    PolygonVertices: originalLine.PolygonVertices,
                    SourceLineCount: originalLine.SourceLineCount));
            }

            return new TranslationResult(translatedLines, Success: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Azure Translator exception: {ex.Message}");
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: $"Translation failed: {ex.Message}");
        }
    }

    private static string NormalizeLanguageTag(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang) || string.Equals(lang, "auto", StringComparison.OrdinalIgnoreCase) || string.Equals(lang, "system", StringComparison.OrdinalIgnoreCase))
        {
            return "auto";
        }

        var trimmed = lang.Trim();
        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex > 0)
        {
            var prefix = trimmed.Substring(0, dashIndex).ToLowerInvariant();
            if (prefix is "zh")
            {
                return trimmed; // zh-Hans or zh-Hant
            }

            return prefix; // en, ja, ko, fr, de, es
        }

        return trimmed.ToLowerInvariant();
    }

    private static string SanitizeErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown server error";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.TryGetProperty("message", out var msgProp))
            {
                return msgProp.GetString() ?? raw;
            }
        }
        catch
        {
            // not json
        }

        return raw.Length > 200 ? string.Concat(raw.AsSpan(0, 200), "...") : raw;
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class AzureTranslateRequestBodyItem
    {
        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class AzureTranslateResponseItem
    {
        [JsonPropertyName("detectedLanguage")]
        public AzureDetectedLanguage? DetectedLanguage { get; set; }

        [JsonPropertyName("translations")]
        public List<AzureTranslationItem>? Translations { get; set; }
    }

    private sealed class AzureDetectedLanguage
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }

    private sealed class AzureTranslationItem
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }
    }
}
