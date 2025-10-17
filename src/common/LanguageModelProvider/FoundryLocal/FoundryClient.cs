// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LanguageModelProvider.FoundryLocal;

internal sealed class FoundryClient
{
    public static async Task<FoundryClient?> CreateAsync()
    {
        var serviceManager = FoundryServiceManager.TryCreate();
        if (serviceManager is null)
        {
            return null;
        }

        if (!await serviceManager.IsRunning().ConfigureAwait(false))
        {
            if (!await serviceManager.StartService().ConfigureAwait(false))
            {
                return null;
            }
        }

        var serviceUrl = await serviceManager.GetServiceUrl().ConfigureAwait(false);

        if (string.IsNullOrEmpty(serviceUrl))
        {
            return null;
        }

        var serviceUri = new Uri(serviceUrl, UriKind.Absolute);
        var baseAddress = serviceUri.AbsoluteUri.EndsWith('/')
            ? serviceUri
            : new Uri(serviceUri, "/");

        var httpClient = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromHours(2),
        };

        var assemblyVersion = typeof(FoundryClient).Assembly.GetName().Version?.ToString() ?? "unknown";
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"foundry-local-cs-sdk/{assemblyVersion}");

        return new FoundryClient(serviceManager, httpClient);
    }

    public FoundryServiceManager ServiceManager { get; }

    private readonly HttpClient _httpClient;
    private readonly List<FoundryCatalogModel> _catalogModels = [];

    private FoundryClient(FoundryServiceManager serviceManager, HttpClient httpClient)
    {
        ServiceManager = serviceManager;
        _httpClient = httpClient;
    }

    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        if (_catalogModels.Count > 0)
        {
            return _catalogModels;
        }

        try
        {
            var response = await _httpClient.GetAsync("/foundry/list").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var models = await JsonSerializer.DeserializeAsync(
                response.Content.ReadAsStream(),
                FoundryJsonContext.Default.ListFoundryCatalogModel).ConfigureAwait(false);

            if (models is { Count: > 0 })
            {
                models.ForEach(_catalogModels.Add);
            }
        }
        catch
        {
            // Surfacing errors here prevents listing other providers; swallow and return cached list instead.
        }

        return _catalogModels;
    }

    public async Task<List<FoundryCachedModel>> ListCachedModels()
    {
        var response = await _httpClient.GetAsync("/openai/models").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var catalogModels = await ListCatalogModels().ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var modelIds = content
            .Trim('[', ']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(id => id.Trim('"'));

        List<FoundryCachedModel> models = [];

        foreach (var id in modelIds)
        {
            var model = catalogModels.FirstOrDefault(m => m.Name == id);
            models.Add(model != null ? new FoundryCachedModel(id, model.Alias) : new FoundryCachedModel(id, null));
        }

        return models;
    }

    public async Task<FoundryDownloadResult> DownloadModel(FoundryCatalogModel model, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        var models = await ListCachedModels().ConfigureAwait(false);

        if (models.Any(m => m.Name == model.Name))
        {
            return new(true, "Model already downloaded");
        }

        return await Task.Run(
            async () =>
            {
                try
                {
                    var providerType = model.ProviderType.EndsWith("Local", StringComparison.OrdinalIgnoreCase)
                        ? model.ProviderType
                        : $"{model.ProviderType}Local";

                    var downloadRequest = new FoundryDownloadBody
                    {
                        Model = new FoundryModelDownload
                        {
                            Name = model.Name,
                            Uri = model.Uri,
                            Publisher = model.Publisher,
                            ProviderType = providerType,
                            PromptTemplate = model.PromptTemplate,
                        },
                        Token = string.Empty,
                        IgnorePipeReport = true,
                    };

                    var downloadBodyContext = FoundryJsonContext.Default.FoundryDownloadBody;
                    string body = JsonSerializer.Serialize(downloadRequest, downloadBodyContext);

                    using var request = new HttpRequestMessage(HttpMethod.Post, "/openai/download")
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    };

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var reader = new StreamReader(stream);

                    StringBuilder jsonBuilder = new();
                    var collectingJson = false;
                    var completed = false;

                    while (!completed && (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is string line)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (line.StartsWith("Total", StringComparison.CurrentCultureIgnoreCase) &&
                            line.Contains("Downloading", StringComparison.OrdinalIgnoreCase) &&
                            line.Contains('%'))
                        {
                            var percentStr = line.Split('%')[0].Split(' ').Last();
                            if (double.TryParse(percentStr, NumberStyles.Float, CultureInfo.CurrentCulture, out var percentage))
                            {
                                progress?.Report((float)(percentage / 100));
                            }
                        }
                        else if (line.Contains("[DONE]", StringComparison.OrdinalIgnoreCase) ||
                                 line.Contains("All Completed", StringComparison.OrdinalIgnoreCase))
                        {
                            collectingJson = true;
                        }
                        else if (collectingJson && line.TrimStart().StartsWith('{'))
                        {
                            jsonBuilder.AppendLine(line);
                        }
                        else if (collectingJson && jsonBuilder.Length > 0)
                        {
                            jsonBuilder.AppendLine(line);
                            if (line.Trim() == "}")
                            {
                                completed = true;
                            }
                        }
                    }

                    var downloadResultContext = FoundryJsonContext.Default.FoundryDownloadResult;
                    var jsonPayload = jsonBuilder.Length > 0 ? jsonBuilder.ToString() : null;

                    if (jsonPayload is null)
                    {
                        return new FoundryDownloadResult(false, "No completion response received from server.");
                    }

                    try
                    {
                        return JsonSerializer.Deserialize(jsonPayload, downloadResultContext)
                               ?? new FoundryDownloadResult(false, "Failed to parse completion response.");
                    }
                    catch (JsonException ex)
                    {
                        return new FoundryDownloadResult(false, $"Failed to parse completion response: {ex.Message}");
                    }
                }
                catch (Exception e)
                {
                    return new FoundryDownloadResult(false, e.Message);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }
}
