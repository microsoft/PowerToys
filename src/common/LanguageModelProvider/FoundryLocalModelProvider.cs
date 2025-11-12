// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ClientModel;
using LanguageModelProvider.FoundryLocal;
using ManagedCommon;
using Microsoft.Extensions.AI;
using OpenAI;

namespace LanguageModelProvider;

public sealed class FoundryLocalModelProvider : ILanguageModelProvider
{
    private IEnumerable<ModelDetails>? _downloadedModels;
    private FoundryClient? _foundryManager;
    private string? _serviceUrl;

    public static FoundryLocalModelProvider Instance { get; } = new();

    public string Name => "FoundryLocal";

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public string UrlPrefix => "fl://";

    public IChatClient? GetIChatClient(string url)
    {
        try
        {
            Logger.LogInfo($"[FoundryLocal] GetIChatClient called with url: {url}");
            InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryLocal] Failed to initialize: {ex.Message}");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_serviceUrl) || _foundryManager == null)
        {
            Logger.LogError("[FoundryLocal] Service URL or manager is null");
            return null;
        }

        // Extract model ID from URL (format: fl://modelname)
        var modelId = url.Replace(UrlPrefix, string.Empty).Trim('/');
        if (string.IsNullOrWhiteSpace(modelId))
        {
            Logger.LogError("[FoundryLocal] Model ID is empty after extraction");
            return null;
        }

        Logger.LogInfo($"[FoundryLocal] Extracted model ID: {modelId}");

        // Ensure the model is loaded before returning chat client
        try
        {
            var isLoaded = _foundryManager.EnsureModelLoaded(modelId).GetAwaiter().GetResult();
            if (!isLoaded)
            {
                Logger.LogError($"[FoundryLocal] Failed to load model: {modelId}");
                return null;
            }

            Logger.LogInfo($"[FoundryLocal] Model is loaded: {modelId}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryLocal] Exception ensuring model loaded: {ex.Message}");
            return null;
        }

        // Use ServiceUri instead of Endpoint since Endpoint already includes /v1
        var baseUri = _foundryManager.GetServiceUri();
        if (baseUri == null)
        {
            Logger.LogError("[FoundryLocal] Service URI is null");
            return null;
        }

        var endpointUri = new Uri($"{baseUri.ToString().TrimEnd('/')}/v1");
        Logger.LogInfo($"[FoundryLocal] Creating OpenAI client with endpoint: {endpointUri}");
        Logger.LogInfo($"[FoundryLocal] Model ID for chat client: {modelId}");

        return new OpenAIClient(
            new ApiKeyCredential("none"),
            new OpenAIClientOptions { Endpoint = endpointUri })
            .GetChatClient(modelId)
            .AsIChatClient();
    }

    public string GetIChatClientString(string url)
    {
        try
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return string.Empty;
        }

        var modelId = url.Split('/').LastOrDefault();

        if (string.IsNullOrWhiteSpace(_serviceUrl) || string.IsNullOrWhiteSpace(modelId))
        {
            return string.Empty;
        }

        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{_serviceUrl}/v1\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
    }

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(bool ignoreCached = false, CancellationToken cancelationToken = default)
    {
        if (ignoreCached)
        {
            Logger.LogInfo("[FoundryLocal] Ignoring cached models, resetting");
            Reset();
        }

        await InitializeAsync(cancelationToken);

        Logger.LogInfo($"[FoundryLocal] Returning {_downloadedModels?.Count() ?? 0} downloaded models");
        return _downloadedModels ?? [];
    }

    private void Reset()
    {
        _downloadedModels = null;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryManager != null && _downloadedModels != null && _downloadedModels.Any())
        {
            return;
        }

        Logger.LogInfo("[FoundryLocal] Initializing provider");
        _foundryManager ??= await FoundryClient.CreateAsync();

        if (_foundryManager == null)
        {
            Logger.LogError("[FoundryLocal] Failed to create Foundry client");
            return;
        }

        _serviceUrl ??= await _foundryManager.GetServiceUrl();
        Logger.LogInfo($"[FoundryLocal] Service URL: {_serviceUrl}");

        var cachedModels = await _foundryManager.ListCachedModels();
        Logger.LogInfo($"[FoundryLocal] Found {cachedModels.Count} cached models");

        List<ModelDetails> downloadedModels = [];

        foreach (var model in cachedModels)
        {
            Logger.LogInfo($"[FoundryLocal] Adding unmatched cached model: {model.Name}");
            downloadedModels.Add(new ModelDetails
            {
                Id = $"fl-{model.Name}",
                Name = model.Name,
                Url = $"{UrlPrefix}{model.Name}",
                Description = $"{model.Name} running locally with Foundry Local",
                HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
                SupportedOnQualcomm = true,
                ProviderModelDetails = model,
            });
        }

        _downloadedModels = downloadedModels;
        Logger.LogInfo($"[FoundryLocal] Initialization complete. Total downloaded models: {downloadedModels.Count}");
    }

    public async Task<bool> IsAvailable()
    {
        Logger.LogInfo("[FoundryLocal] Checking availability");
        await InitializeAsync();
        var available = _foundryManager != null;
        Logger.LogInfo($"[FoundryLocal] Available: {available}");
        return available;
    }
}
