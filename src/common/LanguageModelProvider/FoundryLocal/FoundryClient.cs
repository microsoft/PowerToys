// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.AI.Foundry.Local;

namespace LanguageModelProvider.FoundryLocal;

internal sealed class FoundryClient
{
    public static async Task<FoundryClient?> CreateAsync()
    {
        try
        {
            Logger.LogInfo("[FoundryClient] Creating Foundry Local client");

            // Workaround for SDK issue: FoundryLocalManager.StartServiceAsync() uses UseShellExecute=false
            // which cannot handle Windows App Execution Aliases (foundry.exe in WindowsApps)
            // Pre-start the service using UseShellExecute=true
            await EnsureFoundryServiceStarted().ConfigureAwait(false);

            var manager = new FoundryLocalManager();

            // Check if service is running
            if (!manager.IsServiceRunning)
            {
                Logger.LogInfo("[FoundryClient] Service not running, attempting to start via SDK");
                await manager.StartServiceAsync().ConfigureAwait(false);

                if (!manager.IsServiceRunning)
                {
                    Logger.LogError("[FoundryClient] Failed to start Foundry Local service");
                    return null;
                }
            }

            Logger.LogInfo("[FoundryClient] Foundry Local service is running");
            return new FoundryClient(manager);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error creating client: {ex.Message}");
            if (ex.InnerException != null)
            {
                Logger.LogError($"[FoundryClient] Inner exception: {ex.InnerException.Message}");
            }

            return null;
        }
    }

    private static async Task EnsureFoundryServiceStarted()
    {
        try
        {
            Logger.LogInfo("[FoundryClient] Pre-starting foundry service with UseShellExecute=true");

            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "foundry";
            process.StartInfo.Arguments = "service start";
            process.StartInfo.UseShellExecute = true; // Critical: allows App Execution Alias to work
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

            process.Start();

            // Give the service a moment to start, but don't wait for completion
            // as the service runs in the background
            await Task.Delay(2000).ConfigureAwait(false);

            Logger.LogInfo("[FoundryClient] Foundry service start command completed");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[FoundryClient] Failed to pre-start foundry service: {ex.Message}");
        }
    }

    private readonly FoundryLocalManager _foundryManager;
    private readonly List<FoundryCatalogModel> _catalogModels = [];

    private FoundryClient(FoundryLocalManager foundryManager)
    {
        _foundryManager = foundryManager;
    }

    public Task<string?> GetServiceUrl()
    {
        try
        {
            return Task.FromResult(_foundryManager.Endpoint?.ToString());
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Uri? GetServiceUri()
    {
        try
        {
            return _foundryManager.ServiceUri;
        }
        catch
        {
            return null;
        }
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
            var models = await _foundryManager.ListCatalogModelsAsync().ConfigureAwait(false);

            if (models != null)
            {
                foreach (var model in models)
                {
                    _catalogModels.Add(new FoundryCatalogModel
                    {
                        Name = model.ModelId ?? string.Empty,
                        DisplayName = model.DisplayName ?? string.Empty,
                        ProviderType = model.ProviderType ?? string.Empty,
                        Uri = model.Uri ?? string.Empty,
                        Version = model.Version ?? string.Empty,
                        ModelType = model.ModelType ?? string.Empty,
                        Publisher = model.Publisher ?? string.Empty,
                        Task = model.Task ?? string.Empty,
                        FileSizeMb = model.FileSizeMb,
                        Alias = model.Alias ?? string.Empty,
                        License = model.License ?? string.Empty,
                        LicenseDescription = model.LicenseDescription ?? string.Empty,
                        ParentModelUri = model.ParentModelUri ?? string.Empty,
                        SupportsToolCalling = model.SupportsToolCalling,
                    });
                }

                Logger.LogInfo($"[FoundryClient] Found {_catalogModels.Count} catalog models");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error listing catalog models: {ex.Message}");

            // Surfacing errors here prevents listing other providers; swallow and return cached list instead.
        }

        return _catalogModels;
    }

    public async Task<List<FoundryCachedModel>> ListCachedModels()
    {
        try
        {
            Logger.LogInfo("[FoundryClient] Listing cached models");
            var cachedModels = await _foundryManager.ListCachedModelsAsync().ConfigureAwait(false);
            var catalogModels = await ListCatalogModels().ConfigureAwait(false);

            List<FoundryCachedModel> models = [];

            foreach (var model in cachedModels)
            {
                var catalogModel = catalogModels.FirstOrDefault(m => m.Name == model.ModelId);
                var alias = catalogModel?.Alias ?? model.Alias;
                models.Add(new FoundryCachedModel(model.ModelId ?? string.Empty, alias));
            }

            Logger.LogInfo($"[FoundryClient] Found {models.Count} cached models");
            return models;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error listing cached models: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> IsModelLoaded(string modelId)
    {
        try
        {
            var loadedModels = await _foundryManager.ListLoadedModelsAsync().ConfigureAwait(false);
            var isLoaded = loadedModels.Any(m => m.ModelId == modelId);
            Logger.LogInfo($"[FoundryClient] IsModelLoaded({modelId}): {isLoaded}");
            Logger.LogInfo($"[FoundryClient] Loaded models: {string.Join(", ", loadedModels.Select(m => m.ModelId))}");
            return isLoaded;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] IsModelLoaded exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> EnsureModelLoaded(string modelId)
    {
        try
        {
            Logger.LogInfo($"[FoundryClient] EnsureModelLoaded called with: {modelId}");

            // Check if already loaded
            if (await IsModelLoaded(modelId).ConfigureAwait(false))
            {
                Logger.LogInfo($"[FoundryClient] Model already loaded: {modelId}");
                return true;
            }

            // Check if model exists in cache
            var cachedModels = await ListCachedModels().ConfigureAwait(false);
            Logger.LogInfo($"[FoundryClient] Cached models: {string.Join(", ", cachedModels.Select(m => m.Name))}");

            if (!cachedModels.Any(m => m.Name == modelId))
            {
                Logger.LogWarning($"[FoundryClient] Model not found in cache: {modelId}");
                return false;
            }

            // Load the model
            Logger.LogInfo($"[FoundryClient] Loading model: {modelId}");
            await _foundryManager.LoadModelAsync(modelId).ConfigureAwait(false);

            // Verify it's loaded
            var loaded = await IsModelLoaded(modelId).ConfigureAwait(false);
            Logger.LogInfo($"[FoundryClient] Model load result: {loaded}");
            return loaded;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] EnsureModelLoaded exception: {ex.Message}");
            return false;
        }
    }

    public async Task<FoundryDownloadResult> DownloadModel(FoundryCatalogModel model, IProgress<float>? progress, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInfo($"[FoundryClient] Downloading model: {model.Name}");
            var models = await ListCachedModels().ConfigureAwait(false);

            if (models.Any(m => m.Name == model.Name))
            {
                Logger.LogInfo($"[FoundryClient] Model already downloaded: {model.Name}");
                return new(true, "Model already downloaded");
            }

            // Use the SDK's download with progress
            // CA2016: The cancellationToken is properly forwarded via WithCancellation extension method
#pragma warning disable CA2016
            await foreach (var downloadProgress in _foundryManager.DownloadModelWithProgressAsync(model.Name).WithCancellation(cancellationToken).ConfigureAwait(false))
#pragma warning restore CA2016
            {
                if (downloadProgress.Percentage >= 0 && downloadProgress.Percentage <= 100)
                {
                    progress?.Report((float)(downloadProgress.Percentage / 100));
                }

                if (downloadProgress.IsCompleted)
                {
                    Logger.LogInfo($"[FoundryClient] Download completed: {model.Name}");
                    return new FoundryDownloadResult(true, "Download completed successfully");
                }

                if (!string.IsNullOrEmpty(downloadProgress.ErrorMessage))
                {
                    Logger.LogError($"[FoundryClient] Download error: {downloadProgress.ErrorMessage}");
                    return new FoundryDownloadResult(false, downloadProgress.ErrorMessage);
                }
            }

            Logger.LogInfo($"[FoundryClient] Download completed: {model.Name}");
            return new FoundryDownloadResult(true, "Download completed successfully");
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo($"[FoundryClient] Download cancelled: {model.Name}");
            return new FoundryDownloadResult(false, "Download was cancelled");
        }
        catch (Exception e)
        {
            Logger.LogError($"[FoundryClient] Download exception: {e.Message}");
            return new FoundryDownloadResult(false, e.Message);
        }
    }
}
