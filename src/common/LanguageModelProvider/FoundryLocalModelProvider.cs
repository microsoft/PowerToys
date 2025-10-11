// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LanguageModelProvider.FoundryLocal;
using Microsoft.Extensions.AI;
using OpenAI;

namespace LanguageModelProvider;

public sealed class FoundryLocalModelProvider : ILanguageModelProvider
{
    private IEnumerable<ModelDetails>? _downloadedModels;
    private IEnumerable<ModelDetails>? _catalogModels;
    private FoundryClient? _foundryManager;
    private string? _serviceUrl;

    public static FoundryLocalModelProvider Instance { get; } = new();

    public string Name => "FoundryLocal";

    public HardwareAccelerator ModelHardwareAccelerator => HardwareAccelerator.FOUNDRYLOCAL;

    public List<string> NugetPackageReferences { get; } = ["Microsoft.Extensions.AI.OpenAI"];

    public string ProviderDescription => "The model will run locally via Foundry Local";

    public string UrlPrefix => "fl://";

    public string Icon => $"fl{AppUtils.GetThemeAssetSuffix()}.svg";

    public string Url => _serviceUrl ?? string.Empty;

    public string IChatClientImplementationNamespace { get; } = "OpenAI";

    public string GetDetailsUrl(ModelDetails details)
    {
        throw new NotImplementedException();
    }

    public IChatClient? GetIChatClient(string url)
    {
        try
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_serviceUrl))
        {
            return null;
        }

        var modelId = url.Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        return new OpenAIClient(
            new ApiKeyCredential("none"),
            new OpenAIClientOptions { Endpoint = new Uri($"{_serviceUrl}/v1") })
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
            Reset();
        }

        await InitializeAsync(cancelationToken);

        return _downloadedModels ?? [];
    }

    public IEnumerable<ModelDetails> GetAllModelsInCatalog()
    {
        return _catalogModels ?? [];
    }

    public async Task<bool> DownloadModel(ModelDetails modelDetails, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        if (_foundryManager == null)
        {
            return false;
        }

        if (modelDetails.ProviderModelDetails is not FoundryCatalogModel model)
        {
            return false;
        }

        return (await _foundryManager.DownloadModel(model, progress, cancellationToken)).Success;
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

        _foundryManager ??= await FoundryClient.CreateAsync();

        if (_foundryManager == null)
        {
            return;
        }

        _serviceUrl ??= await _foundryManager.ServiceManager.GetServiceUrl();

        if (_catalogModels == null || !_catalogModels.Any())
        {
            _catalogModels = (await _foundryManager.ListCatalogModels()).Select(ToModelDetails).ToArray();
        }

        var cachedModels = await _foundryManager.ListCachedModels();

        List<ModelDetails> downloadedModels = [];

        foreach (var model in _catalogModels)
        {
            var cachedModel = cachedModels.FirstOrDefault(m => m.Name == model.Name);

            if (cachedModel != default)
            {
                model.Id = $"{UrlPrefix}{cachedModel.Id}";
                downloadedModels.Add(model);
                cachedModels.Remove(cachedModel);
            }
        }

        foreach (var model in cachedModels)
        {
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
    }

    private ModelDetails ToModelDetails(FoundryCatalogModel model)
    {
        return new ModelDetails
        {
            Id = $"fl-{model.Name}",
            Name = model.Name,
            Url = $"{UrlPrefix}{model.Name}",
            Description = $"{model.Alias} running locally with Foundry Local",
            HardwareAccelerators = [HardwareAccelerator.FOUNDRYLOCAL],
            Size = model.FileSizeMb * 1024 * 1024,
            SupportedOnQualcomm = true,
            License = model.License?.ToLowerInvariant() ?? string.Empty,
            ProviderModelDetails = model,
        };
    }

    public async Task<bool> IsAvailable()
    {
        await InitializeAsync();
        return _foundryManager != null;
    }
}
