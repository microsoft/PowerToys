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
/// Translation provider for self-hosted or remote LibreTranslate instances.
/// </summary>
public sealed class LibreTranslateProvider : ITranslationProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly bool _cloudConsentEnabled;

    public string ProviderId => "LibreTranslate";

    public string DisplayName => "LibreTranslate (Self-Hosted / Remote)";

    public LibreTranslateProvider(
        string endpoint,
        string apiKey,
        bool cloudConsentEnabled,
        HttpClient? customHttpClient = null)
    {
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:5000" : endpoint.Trim().TrimEnd('/');
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

        if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var endpointUri))
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: $"Invalid LibreTranslate endpoint URL: {_endpoint}");
        }

        bool isLocal = endpointUri.IsLoopback || string.Equals(endpointUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        if (!isLocal && endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: "Remote LibreTranslate endpoints must use HTTPS.");
        }

        if (!isLocal && !_cloudConsentEnabled)
        {
            return new TranslationResult(
                Array.Empty<TranslatedLine>(),
                Success: false,
                ErrorMessage: "Cloud translation consent is required for remote translation endpoints. Please enable cloud consent in settings.");
        }

        try
        {
            var requestUri = new Uri(endpointUri, "/translate");
            var sourceLang = NormalizeLanguageTag(request.SourceLanguage);
            var targetLang = NormalizeLanguageTag(request.TargetLanguage);

            var queryLines = new List<string>(request.Lines.Count);
            foreach (var line in request.Lines)
            {
                queryLines.Add(line.Text);
            }

            var requestPayload = new LibreTranslateRequestBody
            {
                Queries = queryLines,
                Source = string.IsNullOrEmpty(sourceLang) ? "auto" : sourceLang,
                Target = targetLang,
                Format = "text",
                ApiKey = string.IsNullOrWhiteSpace(_apiKey) ? null : _apiKey,
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json"),
            };

            using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Logger.LogWarning($"LibreTranslate request failed with HTTP {(int)httpResponse.StatusCode}");
                return new TranslationResult(
                    Array.Empty<TranslatedLine>(),
                    Success: false,
                    ErrorMessage: $"LibreTranslate error (HTTP {(int)httpResponse.StatusCode}): {SanitizeErrorMessage(errorBody)}");
            }

            var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(responseJson);

            var translatedTexts = new List<string>();
            if (doc.RootElement.TryGetProperty("translatedText", out var translatedTextElement))
            {
                if (translatedTextElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in translatedTextElement.EnumerateArray())
                    {
                        translatedTexts.Add(item.GetString() ?? string.Empty);
                    }
                }
                else if (translatedTextElement.ValueKind == JsonValueKind.String)
                {
                    translatedTexts.Add(translatedTextElement.GetString() ?? string.Empty);
                }
            }

            if (translatedTexts.Count != request.Lines.Count)
            {
                return new TranslationResult(
                    Array.Empty<TranslatedLine>(),
                    Success: false,
                    ErrorMessage: "LibreTranslate response size did not match requested line count.");
            }

            var translatedLines = new List<TranslatedLine>(request.Lines.Count);
            for (int i = 0; i < request.Lines.Count; i++)
            {
                var originalLine = request.Lines[i];
                var translatedText = translatedTexts[i];

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
            Logger.LogWarning($"LibreTranslate exception: {ex.Message}");
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
                return trimmed.ToLowerInvariant();
            }

            return prefix;
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
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                return errorElement.GetString() ?? raw;
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

    private sealed class LibreTranslateRequestBody
    {
        [JsonPropertyName("q")]
        public List<string> Queries { get; set; } = new();

        [JsonPropertyName("source")]
        public string Source { get; set; } = "auto";

        [JsonPropertyName("target")]
        public string Target { get; set; } = "en";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "text";

        [JsonPropertyName("api_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApiKey { get; set; }
    }
}
