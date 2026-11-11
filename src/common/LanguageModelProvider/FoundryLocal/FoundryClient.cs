// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using ManagedCommon;

namespace LanguageModelProvider.FoundryLocal;

// Talks to the Foundry Local daemon (`foundry server`) over its REST API, not via the SDK:
// - Microsoft.AI.Foundry.Local 0.3.0 parses `foundry service status` (gone) and calls renamed endpoints.
// - 2.x is an in-process inference engine (native ONNX Runtime, GBs of RAM, minute-long loads) with no
//   client mode: ExternalUrl never delegates load/inference to the daemon.
internal sealed class FoundryClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static readonly string FoundryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foundry");

    // shortcut: assumes the default model cache location, read the daemon's /status modelCachePath if users move it.
    private static readonly string CacheDir = Path.Combine(FoundryDir, "cache", "models");

    private readonly List<FoundryCatalogModel> _catalogModels = [];
    private Uri _baseUri;

    private FoundryClient(Uri baseUri)
    {
        _baseUri = baseUri;
    }

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

            var uri = await FindDaemonAsync().ConfigureAwait(false) ?? await StartDaemonAsync().ConfigureAwait(false);
            return uri == null ? null : new FoundryClient(uri);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error creating client: {ex.Message}");
            return null;
        }
    }

    // The daemon picks a free port and writes it to ~/.foundry/daemon.json.
    private static async Task<Uri?> FindDaemonAsync()
    {
        var path = Path.Combine(FoundryDir, "daemon.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("web_urls", out var urls))
            {
                return null;
            }

            foreach (var url in urls.EnumerateArray())
            {
                // daemon.json outlives the daemon, so make sure something answers.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    var uri = new Uri(url.GetString()!);
                    using var response = await Http.GetAsync(new Uri(uri, "models/loaded"), cts.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return uri;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or UriFormatException)
        {
            Logger.LogWarning($"[FoundryClient] Could not read daemon.json: {ex.Message}");
        }

        return null;
    }

    private static async Task<Uri?> StartDaemonAsync()
    {
        Logger.LogInfo("[FoundryClient] Starting Foundry service using 'foundry server start'");

        // `foundry server start` launches the background daemon and exits.
        using var process = Process.Start(new ProcessStartInfo("foundry", "server start")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        if (process == null)
        {
            return null;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

        for (var i = 0; i < 30; i++)
        {
            var uri = await FindDaemonAsync().ConfigureAwait(false);
            if (uri != null)
            {
                return uri;
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        return null;
    }

    public Task<string?> GetServiceUrl() => Task.FromResult<string?>(_baseUri.ToString().TrimEnd('/'));

    public Uri? GetServiceUri() => _baseUri;

    // The daemon's own catalog cache holds the model id <-> alias mapping that loading needs.
    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        if (_catalogModels.Count > 0)
        {
            return _catalogModels;
        }

        try
        {
            Logger.LogInfo("[FoundryClient] Listing catalog models");
            var path = Path.Combine(CacheDir, "foundry.modelinfo.json");
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));

            foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                _catalogModels.Add(new FoundryCatalogModel
                {
                    Name = GetString(model, "id"),
                    DisplayName = GetString(model, "displayName"),
                    Alias = GetString(model, "alias"),
                    Publisher = GetString(model, "publisher"),
                    Task = GetString(model, "task"),
                    FileSizeMb = model.TryGetProperty("fileSizeMb", out var size) && size.TryGetInt64(out var mb) ? mb : 0,
                });
            }

            Logger.LogInfo($"[FoundryClient] Found {_catalogModels.Count} catalog models");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error listing catalog models: {ex.Message}");

            // Surfacing errors here prevents listing other providers; swallow and return cached list instead.
        }

        return _catalogModels;
    }

    // Downloaded models live in <cache>/<publisher>/<model id with ':' as '-'>; a download.tmp means unfinished.
    public async Task<List<FoundryCachedModel>> ListCachedModels()
    {
        var catalogModels = await ListCatalogModels().ConfigureAwait(false);
        List<FoundryCachedModel> models = [];

        if (!Directory.Exists(CacheDir))
        {
            return models;
        }

        foreach (var dir in Directory.EnumerateDirectories(CacheDir).SelectMany(Directory.EnumerateDirectories))
        {
            var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
            if (files.Count == 0 || files.Any(f => Path.GetFileName(f) == "download.tmp"))
            {
                continue;
            }

            var name = Path.GetFileName(dir);
            var catalogModel = catalogModels.FirstOrDefault(m => string.Equals(m.Name.Replace(':', '-'), name, StringComparison.OrdinalIgnoreCase));
            if (catalogModel != null)
            {
                models.Add(new FoundryCachedModel(catalogModel.Name, catalogModel.Alias));
            }
        }

        Logger.LogInfo($"[FoundryClient] Found {models.Count} cached models");
        return models;
    }

    public async Task<bool> IsModelLoaded(string modelId)
    {
        try
        {
            var json = await Http.GetStringAsync(new Uri(_baseUri, "models/loaded")).ConfigureAwait(false);
            var loadedModels = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            var isLoaded = loadedModels.Contains(modelId, StringComparer.OrdinalIgnoreCase);
            Logger.LogInfo($"[FoundryClient] IsModelLoaded({modelId}): {isLoaded}");
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

        if (await IsModelLoaded(modelId).ConfigureAwait(false))
        {
            return true;
        }

        // The daemon loads by alias, but chat completions need the full model id.
        var catalogModels = await ListCatalogModels().ConfigureAwait(false);
        var alias = catalogModels.FirstOrDefault(m => m.Name == modelId)?.Alias;
        if (string.IsNullOrEmpty(alias))
        {
            Logger.LogError($"[FoundryClient] No alias known for model: {modelId}");
            return false;
        }

        Logger.LogInfo($"[FoundryClient] Loading model: {modelId} (alias {alias})");
        using var response = await Http.GetAsync(new Uri(_baseUri, $"models/load/{Uri.EscapeDataString(alias)}")).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError($"[FoundryClient] Loading {modelId} failed: {response.StatusCode}");
            return false;
        }

        return await IsModelLoaded(modelId).ConfigureAwait(false);
    }

    public async Task EnsureRunning()
    {
        var uri = await FindDaemonAsync().ConfigureAwait(false) ?? await StartDaemonAsync().ConfigureAwait(false);
        if (uri != null)
        {
            _baseUri = uri;
        }
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

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
