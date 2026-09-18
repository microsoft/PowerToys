// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using ManagedCommon;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace LanguageModelProvider.FoundryLocal;

internal sealed class FoundryClient
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _epsRegistered;

    private readonly ICatalog _catalog;
    private readonly List<FoundryCatalogModel> _catalogModels = [];

    private FoundryClient(ICatalog catalog)
    {
        _catalog = catalog;
    }

    public static async Task<FoundryClient?> CreateAsync()
    {
        await InitLock.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!FoundryLocalManager.IsInitialized)
            {
                Logger.LogInfo("[FoundryClient] Initializing Foundry Local manager");

                // AppDataDir/ModelCacheDir/LogsDir are intentionally left unset so that Foundry Local
                // uses its default per-user cache directory, shared with other Foundry Local clients.
                //
                // Web is also left unset. Pinning Urls to a fixed port makes startup fail outright with
                // "Couldn't bind" whenever anything else already holds that port - including the Foundry
                // Local CLI or another PowerToys process. Letting the SDK choose a free port and reading
                // it back from Urls keeps us compatible with an already-running service.
                var configuration = new Configuration
                {
                    AppName = "PowerToys",
                    AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foundry"),
                };

                await FoundryLocalManager.CreateAsync(configuration, NullLogger.Instance).ConfigureAwait(false);
            }

            await EnsureExecutionProvidersAsync().ConfigureAwait(false);

            var catalog = await FoundryLocalManager.Instance.GetCatalogAsync().ConfigureAwait(false);
            return new FoundryClient(catalog);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error creating client: {ex.Message}", ex);
            return null;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static async Task EnsureExecutionProvidersAsync()
    {
        if (_epsRegistered)
        {
            return;
        }

        try
        {
            Logger.LogInfo("[FoundryClient] Downloading and registering execution providers");
            var result = await FoundryLocalManager.Instance.DownloadAndRegisterEpsAsync().ConfigureAwait(false);
            Logger.LogInfo($"[FoundryClient] Execution provider registration completed: {result}");
            _epsRegistered = true;
        }
        catch (Exception ex)
        {
            // Inference can still work on the default CPU execution provider, so this is not fatal.
            Logger.LogWarning($"[FoundryClient] Failed to download/register execution providers: {ex.Message}");
        }
    }

    public async Task<string?> GetServiceUrl()
    {
        try
        {
            await EnsureRunning().ConfigureAwait(false);
            return FoundryLocalManager.Instance.Urls?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error getting service URL: {ex.Message}", ex);
            return null;
        }
    }

    public Uri? GetServiceUri()
    {
        var serviceUrl = GetServiceUrl().GetAwaiter().GetResult();
        return string.IsNullOrWhiteSpace(serviceUrl) ? null : new Uri(serviceUrl);
    }

    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        if (_catalogModels.Count > 0)
        {
            return _catalogModels;
        }

        try
        {
            Logger.LogInfo("[FoundryClient] Listing catalog models");
            var models = await _catalog.ListModelsAsync().ConfigureAwait(false);

            foreach (var model in models)
            {
                var info = model.Info;
                _catalogModels.Add(new FoundryCatalogModel
                {
                    Name = model.Id ?? string.Empty,
                    DisplayName = info?.DisplayName ?? model.Alias ?? model.Id ?? string.Empty,
                    ProviderType = info?.ProviderType ?? string.Empty,
                    Uri = info?.Uri ?? string.Empty,
                    Version = info?.Version.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    ModelType = info?.ModelType ?? string.Empty,
                    Publisher = info?.Publisher ?? string.Empty,
                    Task = info?.Task ?? string.Empty,
                    FileSizeMb = info?.FileSizeMb ?? 0,
                    Alias = model.Alias ?? string.Empty,
                    License = info?.License ?? string.Empty,
                    LicenseDescription = info?.LicenseDescription ?? string.Empty,
                    ParentModelUri = info?.Uri ?? string.Empty,
                    SupportsToolCalling = info?.SupportsToolCalling ?? false,
                });
            }

            Logger.LogInfo($"[FoundryClient] Found {_catalogModels.Count} catalog models");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error listing catalog models: {ex.Message}", ex);
        }

        return _catalogModels;
    }

    public async Task<List<FoundryCachedModel>> ListCachedModels()
    {
        try
        {
            Logger.LogInfo("[FoundryClient] Listing cached models");
            var cachedModels = await _catalog.GetCachedModelsAsync().ConfigureAwait(false);
            var models = cachedModels
                .Select(model => new FoundryCachedModel(model.Id ?? string.Empty, model.Alias ?? string.Empty))
                .ToList();

            Logger.LogInfo($"[FoundryClient] Found {models.Count} cached models");
            return models;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error listing cached models: {ex.Message}", ex);
            return [];
        }
    }

    /// <summary>
    /// Resolves a model reference to a catalog entry.
    /// </summary>
    /// <remarks>
    /// The two catalog lookups accept different kinds of identifier and neither one covers both:
    /// <c>GetModelVariantAsync</c> matches a fully qualified variant id such as
    /// <c>qwen2.5-coder-0.5b-instruct-qnn-npu:1</c>, while <c>GetModelAsync</c> matches only an alias
    /// such as <c>qwen2.5-coder-0.5b</c>. Whichever one we picked, the other form returned null and the
    /// caller reported the model as unsupported. Settings can persist either form, so try both, and also
    /// retry a variant id without its trailing <c>:version</c> in case the cached version has moved on.
    /// </remarks>
    private async Task<IModel?> ResolveModelAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        var model = await _catalog.GetModelVariantAsync(modelId).ConfigureAwait(false);
        if (model != null)
        {
            return model;
        }

        model = await _catalog.GetModelAsync(modelId).ConfigureAwait(false);
        if (model != null)
        {
            return model;
        }

        var separator = modelId.LastIndexOf(':');
        if (separator > 0)
        {
            var withoutVersion = modelId[..separator];

            model = await _catalog.GetModelVariantAsync(withoutVersion).ConfigureAwait(false);
            if (model != null)
            {
                return model;
            }

            model = await _catalog.GetModelAsync(withoutVersion).ConfigureAwait(false);
            if (model != null)
            {
                return model;
            }
        }

        return null;
    }

    public async Task<bool> IsModelLoaded(string modelId)
    {
        try
        {
            var model = await ResolveModelAsync(modelId).ConfigureAwait(false);
            if (model == null)
            {
                return false;
            }

            var loaded = await model.IsLoadedAsync().ConfigureAwait(false);
            Logger.LogInfo($"[FoundryClient] IsModelLoaded({modelId}): {loaded}");
            return loaded;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] IsModelLoaded exception: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> EnsureModelLoaded(string modelId)
    {
        Logger.LogInfo($"[FoundryClient] EnsureModelLoaded called with: {modelId}");

        try
        {
            var model = await ResolveModelAsync(modelId).ConfigureAwait(false);
            if (model == null)
            {
                Logger.LogWarning($"[FoundryClient] Model not found in catalog: {modelId}");
                return false;
            }

            if (await model.IsLoadedAsync().ConfigureAwait(false))
            {
                Logger.LogInfo($"[FoundryClient] Model already loaded: {modelId}");
                return true;
            }

            if (await model.IsCachedAsync().ConfigureAwait(false) is false)
            {
                Logger.LogInfo($"[FoundryClient] Downloading model: {modelId}");
                await model.DownloadAsync(progress => { }).ConfigureAwait(false);
            }

            Logger.LogInfo($"[FoundryClient] Loading model: {modelId}");
            await model.LoadAsync().ConfigureAwait(false);

            var loaded = await model.IsLoadedAsync().ConfigureAwait(false);
            Logger.LogInfo($"[FoundryClient] Model load result: {loaded}");
            return loaded;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] EnsureModelLoaded failed: {ex.Message}", ex);
            return false;
        }
    }

    public async Task EnsureRunning()
    {
        if (!FoundryLocalManager.IsInitialized)
        {
            throw new InvalidOperationException("Foundry Local manager is not initialized.");
        }

        if (FoundryLocalManager.Instance.Urls is { Length: > 0 })
        {
            return;
        }

        Logger.LogInfo("[FoundryClient] Starting Foundry Local web service");
        await FoundryLocalManager.Instance.StartWebServiceAsync().ConfigureAwait(false);
    }
}
