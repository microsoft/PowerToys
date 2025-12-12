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
        // First attempt with current environment
        var client = await TryCreateClientAsync().ConfigureAwait(false);
        if (client != null)
        {
            return client;
        }

        // If failed, refresh PATH from registry and retry once
        // This handles cases where PowerToys was launched by MSI installer.
        Logger.LogInfo("[FoundryClient] First attempt failed, refreshing PATH and retrying");
        RefreshEnvironmentPath();

        return await TryCreateClientAsync().ConfigureAwait(false);
    }

    private static async Task<FoundryClient?> TryCreateClientAsync()
    {
        try
        {
            Logger.LogInfo("[FoundryClient] Creating Foundry Local client");

            var manager = new FoundryLocalManager();

            // Check if service is already running
            if (manager.IsServiceRunning)
            {
                Logger.LogInfo("[FoundryClient] Foundry service is already running");
                return new FoundryClient(manager);
            }

            // Start the service using SDK's method
            Logger.LogInfo("[FoundryClient] Starting Foundry service using manager.StartServiceAsync()");
            await manager.StartServiceAsync().ConfigureAwait(false);

            Logger.LogInfo("[FoundryClient] Foundry service started successfully");
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
        Logger.LogInfo($"[FoundryClient] EnsureModelLoaded called with: {modelId}");

        // Check if already loaded
        if (await IsModelLoaded(modelId).ConfigureAwait(false))
        {
            Logger.LogInfo($"[FoundryClient] Model already loaded: {modelId}");
            return true;
        }

        // Load the model
        Logger.LogInfo($"[FoundryClient] Loading model: {modelId}");
        await _foundryManager.LoadModelAsync(modelId).ConfigureAwait(false);

        // Verify it's loaded
        var loaded = await IsModelLoaded(modelId).ConfigureAwait(false);
        Logger.LogInfo($"[FoundryClient] Model load result: {loaded}");
        return loaded;
    }

    public async Task EnsureRunning()
    {
        if (!_foundryManager.IsServiceRunning)
        {
            await _foundryManager.StartServiceAsync();
        }
    }

    /// <summary>
    /// Refreshes the PATH environment variable from the system registry.
    /// This is necessary when tools are installed while PowerToys is running,
    /// as the installer updates the system PATH but running processes don't see the change.
    /// </summary>
    private static void RefreshEnvironmentPath()
    {
        try
        {
            Logger.LogInfo("[FoundryClient] Refreshing PATH environment variable from system");

            var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
            var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;
            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;

            var pathsToAdd = new List<string>();

            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                pathsToAdd.AddRange(currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            }

            if (!string.IsNullOrWhiteSpace(userPath))
            {
                var userPaths = userPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in userPaths)
                {
                    if (!pathsToAdd.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        pathsToAdd.Add(path);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(machinePath))
            {
                var machinePaths = machinePath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in machinePaths)
                {
                    if (!pathsToAdd.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        pathsToAdd.Add(path);
                    }
                }
            }

            var newPath = string.Join(Path.PathSeparator.ToString(), pathsToAdd);

            if (currentPath != newPath)
            {
                Logger.LogInfo("[FoundryClient] Updating process PATH with latest system values");
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
            }
            else
            {
                Logger.LogInfo("[FoundryClient] PATH is already up to date");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Failed to refresh PATH: {ex.Message}");
        }
    }
}
