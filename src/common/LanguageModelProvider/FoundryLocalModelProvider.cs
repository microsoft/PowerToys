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
    private FoundryClient? _foundryClient;
    private IEnumerable<FoundryCatalogModel>? _catalogModels;
    private string? _serviceUrl;

    public static FoundryLocalModelProvider Instance { get; } = new();

    public string Name => "FoundryLocal";

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public IChatClient? GetIChatClient(string modelId)
    {
        Logger.LogInfo($"[FoundryLocal] GetIChatClient called with url: {modelId}");

        // InitializeAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            Logger.LogError("[FoundryLocal] Model ID is empty after extraction");
            return null;
        }

        // Check if model is in catalog
        if (!EnsureModelInCatalog(modelId))
        {
            var errorMessage = $"{modelId} is not supported in Foundry Local. Please configure supported models in Settings.";
            Logger.LogError($"[FoundryLocal] {errorMessage}");
            throw new InvalidOperationException(errorMessage);
        }

        // Ensure the model is loaded before returning chat client
        var isLoaded = EnsureModelLoadedWithRefresh(modelId);
        if (!isLoaded)
        {
            Logger.LogError($"[FoundryLocal] Failed to load model: {modelId}");
            throw new InvalidOperationException($"Failed to load the model '{modelId}'.");
        }

        var client = _foundryClient;
        if (client == null)
        {
            const string message = "Foundry Local client could not be created. Please make sure Foundry Local is installed and running.";
            Logger.LogError($"[FoundryLocal] {message}");
            throw new InvalidOperationException(message);
        }

        // Use ServiceUri instead of Endpoint since Endpoint already includes /v1
        var baseUri = client.GetServiceUri();
        if (baseUri == null && TryRefreshClient("Service URI was not available"))
        {
            baseUri = _foundryClient?.GetServiceUri();
        }

        if (baseUri == null)
        {
            const string message = "Foundry Local service URL is not available. Please make sure Foundry Local is installed and running.";
            Logger.LogError($"[FoundryLocal] {message}");
            throw new InvalidOperationException(message);
        }

        var endpointUri = new Uri($"{baseUri.ToString().TrimEnd('/')}/v1");
        Logger.LogInfo($"[FoundryLocal] Creating OpenAI client with endpoint: {endpointUri}");

        // The OpenAI-compatible endpoint only recognizes fully qualified variant names, so an alias
        // that passed the catalog and load checks would still come back as HTTP 404 "Model not found".
        var requestModelId = ResolveCatalogName(modelId);
        if (!string.Equals(requestModelId, modelId, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogInfo($"[FoundryLocal] Resolved model alias '{modelId}' to catalog name '{requestModelId}'");
        }

        return new OpenAIClient(
            new ApiKeyCredential("none"),
            new OpenAIClientOptions { Endpoint = endpointUri, NetworkTimeout = TimeSpan.FromMinutes(5) })
            .GetChatClient(requestModelId)
            .AsIChatClient();
    }

    /// <summary>
    /// Maps a model reference to the catalog's fully qualified name, which is what the inference
    /// endpoint expects. Returns the input unchanged when no catalog entry matches.
    /// </summary>
    private string ResolveCatalogName(string modelId)
    {
        if (_catalogModels is null)
        {
            return modelId;
        }

        var exact = _catalogModels.FirstOrDefault(m => string.Equals(m.Name, modelId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Name;
        }

        var byAlias = _catalogModels.FirstOrDefault(m => string.Equals(m.Alias, modelId, StringComparison.OrdinalIgnoreCase));
        if (byAlias is not null)
        {
            return byAlias.Name;
        }

        var separator = modelId.LastIndexOf(':');
        if (separator > 0)
        {
            var withoutVersion = modelId[..separator];
            var byName = _catalogModels.FirstOrDefault(m => string.Equals(m.Name, withoutVersion, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName.Name;
            }
        }

        return modelId;
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

    public async Task<IEnumerable<ModelDetails>> GetModelsAsync(CancellationToken cancelationToken = default)
    {
        await InitializeAsync(cancelationToken);

        if (_foundryClient == null)
        {
            return Array.Empty<ModelDetails>();
        }

        var cachedModels = await _foundryClient.ListCachedModels();
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
                ProviderModelDetails = model,
            });
        }

        return downloadedModels;
    }

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryClient != null && _catalogModels != null && _catalogModels.Any())
        {
            await _foundryClient.EnsureRunning().ConfigureAwait(false);
            _serviceUrl = await _foundryClient.GetServiceUrl().ConfigureAwait(false);
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
    }

    public async Task<bool> IsAvailable()
    {
        Logger.LogInfo("[FoundryLocal] Checking availability");
        await InitializeAsync();
        var available = _foundryClient != null;
        Logger.LogInfo($"[FoundryLocal] Available: {available}");
        return available;
    }

    /// <summary>
    /// Confirms the model reference names something in the catalog.
    /// </summary>
    /// <remarks>
    /// A catalog entry is addressable either by its fully qualified variant <c>Name</c>
    /// (<c>qwen2.5-coder-0.5b-instruct-qnn-npu:1</c>) or by its shorter <c>Alias</c>
    /// (<c>qwen2.5-coder-0.5b</c>). Matching only on <c>Name</c> rejected every alias as unsupported,
    /// so accept both, along with a variant name whose trailing <c>:version</c> has been dropped.
    /// </remarks>
    private bool EnsureModelInCatalog(string modelId)
    {
        if (MatchesCatalog(modelId))
        {
            return true;
        }

        Logger.LogWarning($"[FoundryLocal] Model not found in catalog. Refreshing client for model: {modelId}");
        if (!TryRefreshClient("Model not in catalog"))
        {
            return false;
        }

        return MatchesCatalog(modelId);
    }

    private bool MatchesCatalog(string modelId)
    {
        if (_catalogModels is null)
        {
            return false;
        }

        var separator = modelId.LastIndexOf(':');
        var withoutVersion = separator > 0 ? modelId[..separator] : modelId;

        return _catalogModels.Any(m =>
            string.Equals(m.Name, modelId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Alias, modelId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Name, withoutVersion, StringComparison.OrdinalIgnoreCase));
    }

    private bool EnsureModelLoadedWithRefresh(string modelId)
    {
        var isLoaded = false;

        try
        {
            isLoaded = _foundryClient!.EnsureModelLoaded(modelId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[FoundryLocal] EnsureModelLoaded failed: {ex.Message}");
        }

        if (isLoaded)
        {
            return true;
        }

        if (!TryRefreshClient("EnsureModelLoaded failed"))
        {
            return false;
        }

        try
        {
            return _foundryClient!.EnsureModelLoaded(modelId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryLocal] EnsureModelLoaded failed after refresh: {ex.Message}", ex);
            return false;
        }
    }

    private bool TryRefreshClient(string reason)
    {
        Logger.LogInfo($"[FoundryLocal] Refreshing Foundry Local client: {reason}");

        try
        {
            _foundryClient = null;
            _catalogModels = null;
            _serviceUrl = null;

            InitializeAsync().GetAwaiter().GetResult();
            return _foundryClient != null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryLocal] Failed to refresh Foundry Local client: {ex.Message}", ex);
            return false;
        }
    }
}
