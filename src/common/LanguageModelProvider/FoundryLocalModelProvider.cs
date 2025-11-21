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
    private IEnumerable<FoundryCatalogModel>? _catalogModels;
    private FoundryClient? _foundryClient;
    private string? _serviceUrl;

    public static FoundryLocalModelProvider Instance { get; } = new();

    public string Name => "FoundryLocal";

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public IChatClient? GetIChatClient(string modelId)
    {
        Logger.LogInfo($"[FoundryLocal] GetIChatClient called with url: {modelId}");
        InitializeAsync().GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(modelId))
        {
            Logger.LogError("[FoundryLocal] Model ID is empty after extraction");
            return null;
        }

        // Check if model is in catalog
        var isInCatalog = _catalogModels?.Any(m => m.Name == modelId) ?? false;
        if (!isInCatalog)
        {
            var errorMessage = $"{modelId} is not supported in Foundry Local. Please configure supported models in Settings.";
            Logger.LogError($"[FoundryLocal] {errorMessage}");
            throw new InvalidOperationException(errorMessage);
        }

        // Check if model is cached
        var isInCache = _downloadedModels?.Any(m => m.ProviderModelDetails is FoundryCachedModel cached && cached.Name == modelId) ?? false;
        if (!isInCache)
        {
            var errorMessage = $"The requested model '{modelId}' is not cached. Please download it using Foundry Local.";
            Logger.LogError($"[FoundryLocal] {errorMessage}");
            throw new InvalidOperationException(errorMessage);
        }

        // Ensure the model is loaded before returning chat client
        var isLoaded = _foundryClient!.EnsureModelLoaded(modelId).GetAwaiter().GetResult();
        if (!isLoaded)
        {
            Logger.LogError($"[FoundryLocal] Failed to load model: {modelId}");
            throw new InvalidOperationException($"Failed to load the model '{modelId}'.");
        }

        // Use ServiceUri instead of Endpoint since Endpoint already includes /v1
        var baseUri = _foundryClient.GetServiceUri();
        if (baseUri == null)
        {
            const string message = "Foundry Local service URL is not available. Please make sure Foundry Local is installed and running.";
            Logger.LogError($"[FoundryLocal] {message}");
            throw new InvalidOperationException(message);
        }

        var endpointUri = new Uri($"{baseUri.ToString().TrimEnd('/')}/v1");
        Logger.LogInfo($"[FoundryLocal] Creating OpenAI client with endpoint: {endpointUri}");

        return new OpenAIClient(
            new ApiKeyCredential("none"),
            new OpenAIClientOptions { Endpoint = endpointUri, NetworkTimeout = TimeSpan.FromMinutes(5) })
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
        _catalogModels = null;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryClient != null && _downloadedModels != null && _downloadedModels.Any() && _catalogModels != null && _catalogModels.Any())
        {
            await _foundryClient.EnsureRunning().ConfigureAwait(false);
            return;
        }

        Logger.LogInfo("[FoundryLocal] Initializing provider");
        _foundryClient ??= await FoundryClient.CreateAsync();

        if (_foundryClient == null)
        {
            const string message = "Foundry Local client could not be created. Please make sure Foundry Local is installed and running.";
            Logger.LogError($"[FoundryLocal] {message}");
            throw new InvalidOperationException(message);
        }

        _serviceUrl ??= await _foundryClient.GetServiceUrl();
        Logger.LogInfo($"[FoundryLocal] Service URL: {_serviceUrl}");

        var catalogModels = await _foundryClient.ListCatalogModels();
        Logger.LogInfo($"[FoundryLocal] Found {catalogModels.Count} catalog models");
        _catalogModels = catalogModels;

        var cachedModels = await _foundryClient.ListCachedModels();
        Logger.LogInfo($"[FoundryLocal] Found {cachedModels.Count} cached models");

        List<ModelDetails> downloadedModels = [];

        foreach (var model in cachedModels)
        {
            Logger.LogInfo($"[FoundryLocal] Adding unmatched cached model: {model.Name}");
            downloadedModels.Add(new ModelDetails
            {
                Id = $"fl-{model.Name}",
                Name = model.Name,
                Url = $"fl://{model.Name}",
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
        var available = _foundryClient != null;
        Logger.LogInfo($"[FoundryLocal] Available: {available}");
        return available;
    }
}
