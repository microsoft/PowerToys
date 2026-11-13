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

        // Use BaseUri instead of Endpoint since Endpoint already includes /v1
        var endpointUri = new Uri($"{client.BaseUri.ToString().TrimEnd('/')}/v1");
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
        var serviceUrl = _foundryClient?.BaseUri.ToString().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(serviceUrl) || string.IsNullOrWhiteSpace(modelId))
        {
            return string.Empty;
        }

        return $"new OpenAIClient(new ApiKeyCredential(\"none\"), new OpenAIClientOptions{{ Endpoint = new Uri(\"{serviceUrl}/v1\") }}).GetChatClient(\"{modelId}\").AsIChatClient()";
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
            List<HardwareAccelerator> accelerators = [HardwareAccelerator.FOUNDRYLOCAL];
            if (TryMapDeviceType(model.DeviceType) is { } deviceAccelerator)
            {
                accelerators.Add(deviceAccelerator);
            }

            downloadedModels.Add(new ModelDetails
            {
                Id = $"fl-{model.Name}",
                Name = model.Name,
                Url = $"fl://{model.Name}",
                Description = $"{model.Name} running locally with Foundry Local",
                Size = model.FileSizeMb * 1024L * 1024L,
                License = model.License,
                HardwareAccelerators = accelerators,
                ProviderModelDetails = model,
            });
        }

        return downloadedModels;
    }

    private static HardwareAccelerator? TryMapDeviceType(string deviceType) =>
        deviceType.ToUpperInvariant() switch
        {
            "CPU" => HardwareAccelerator.CPU,
            "GPU" => HardwareAccelerator.GPU,
            "NPU" => HardwareAccelerator.NPU,
            _ => null,
        };

    private async Task InitializeAsync(CancellationToken cancelationToken = default)
    {
        if (_foundryClient != null && _catalogModels != null && _catalogModels.Any())
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

        Logger.LogInfo($"[FoundryLocal] Service URL: {_foundryClient.BaseUri}");

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

    private bool EnsureModelInCatalog(string modelId)
    {
        var isInCatalog = _catalogModels?.Any(m => m.Name == modelId) ?? false;
        if (isInCatalog)
        {
            return true;
        }

        Logger.LogWarning($"[FoundryLocal] Model not found in catalog. Refreshing client for model: {modelId}");
        if (!TryRefreshClient("Model not in catalog"))
        {
            return false;
        }

        return _catalogModels?.Any(m => m.Name == modelId) ?? false;
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
