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
    private static readonly string DefaultCacheDir = Path.Combine(FoundryDir, "cache", "models");

    private List<FoundryCatalogModel> _catalogModels = [];
    private Uri _baseUri;
    private string? _cacheDir;

    private FoundryClient(Uri baseUri)
    {
        _baseUri = baseUri;
    }

    public Uri BaseUri => _baseUri;

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

    // The daemon picks a free port and writes it to ~/.foundry/daemon.json; fall back to CLI if missing.
    private static async Task<Uri?> FindDaemonAsync() =>
        await FindDaemonFromFileAsync().ConfigureAwait(false) ?? await FindDaemonFromCliAsync().ConfigureAwait(false);

    private static async Task<Uri?> FindDaemonFromFileAsync()
    {
        var path = Path.Combine(FoundryDir, "daemon.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
            if (doc.RootElement.TryGetProperty("web_urls", out var urls))
            {
                return await ProbeDaemonUrlsAsync(urls).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or UriFormatException)
        {
            Logger.LogWarning($"[FoundryClient] Could not read daemon.json: {ex.Message}");
        }

        return null;
    }

    private static async Task<Uri?> FindDaemonFromCliAsync()
    {
        using var statusDoc = await RunFoundryJsonAsync("server status -o json").ConfigureAwait(false);
        if (statusDoc != null &&
            statusDoc.RootElement.TryGetProperty("running", out var running) &&
            running.ValueKind == JsonValueKind.True &&
            statusDoc.RootElement.TryGetProperty("webUrls", out var statusUrls))
        {
            return await ProbeDaemonUrlsAsync(statusUrls).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<Uri?> ProbeDaemonUrlsAsync(JsonElement urls)
    {
        foreach (var url in urls.EnumerateArray())
        {
            if (Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) &&
                await ProbeUrlAsync(uri).ConfigureAwait(false))
            {
                return uri;
            }
        }

        return null;
    }

    private static async Task<bool> ProbeUrlAsync(Uri uri)
    {
        // daemon.json outlives the daemon, so make sure something answers.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var response = await Http.GetAsync(new Uri(uri, "models/loaded"), cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<Uri?> StartDaemonAsync()
    {
        Logger.LogInfo("[FoundryClient] Starting Foundry service using 'foundry server start'");

        try
        {
            // `foundry server start` launches the background daemon and exits.
            using var process = Process.Start(new ProcessStartInfo("foundry", "server start")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process == null)
            {
                return null;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            for (var i = 0; i < 30; i++)
            {
                var uri = await FindDaemonFromFileAsync().ConfigureAwait(false);
                if (uri != null)
                {
                    return uri;
                }

                if (i < 29)
                {
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }

            return await FindDaemonFromCliAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error starting Foundry daemon: {ex.Message}");
        }

        return null;
    }

    // The daemon's own catalog cache holds the model id <-> alias mapping that loading needs.
    public async Task<List<FoundryCatalogModel>> ListCatalogModels()
    {
        var cached = _catalogModels;
        if (cached.Count > 0)
        {
            return cached;
        }

        try
        {
            Logger.LogInfo("[FoundryClient] Listing catalog models");
            var cacheDir = await GetCacheDirAsync().ConfigureAwait(false);
            var path = Path.Combine(cacheDir, "foundry.modelinfo.json");
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));

            List<FoundryCatalogModel> models = [];
            foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                var deviceType = model.TryGetProperty("runtime", out var runtime) && runtime.ValueKind == JsonValueKind.Object
                    ? GetString(runtime, "deviceType")
                    : string.Empty;
                models.Add(new FoundryCatalogModel(
                    GetString(model, "id"),
                    GetString(model, "alias"),
                    model.TryGetProperty("fileSizeMb", out var size) && size.TryGetInt64(out var mb) ? mb : 0,
                    GetString(model, "license"),
                    deviceType));
            }

            if (models.Count > 0)
            {
                _catalogModels = models;
            }

            Logger.LogInfo($"[FoundryClient] Found {models.Count} catalog models");
            return models;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FoundryClient] Error listing catalog models: {ex.Message}");

            // Surfacing errors here prevents listing other providers; swallow and return cached list instead.
            return _catalogModels;
        }
    }

    // Downloaded models live in <cache>/<publisher>/<model id with ':' as '-'>; a download.tmp means unfinished.
    public async Task<List<FoundryCatalogModel>> ListCachedModels()
    {
        var catalogModels = await ListCatalogModels().ConfigureAwait(false);
        var cacheDir = await GetCacheDirAsync().ConfigureAwait(false);
        List<FoundryCatalogModel> models = [];

        if (!Directory.Exists(cacheDir))
        {
            return models;
        }

        foreach (var dir in Directory.EnumerateDirectories(cacheDir).SelectMany(Directory.EnumerateDirectories))
        {
            var hasFiles = false;
            var isDownloading = false;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                hasFiles = true;
                if (Path.GetFileName(file) == "download.tmp")
                {
                    isDownloading = true;
                    break;
                }
            }

            if (!hasFiles || isDownloading)
            {
                continue;
            }

            var name = Path.GetFileName(dir);
            var catalogModel = catalogModels.FirstOrDefault(m => string.Equals(m.Name.Replace(':', '-'), name, StringComparison.OrdinalIgnoreCase));
            if (catalogModel != null)
            {
                models.Add(catalogModel);
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

        // Try loading by exact model id first so variant selection isn't lost; fall back to alias if needed.
        Logger.LogInfo($"[FoundryClient] Loading model: {modelId}");
        using var response = await Http.GetAsync(new Uri(_baseUri, $"models/load/{Uri.EscapeDataString(modelId)}")).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var catalogModels = await ListCatalogModels().ConfigureAwait(false);
            var alias = catalogModels.FirstOrDefault(m => string.Equals(m.Name, modelId, StringComparison.OrdinalIgnoreCase))?.Alias;
            if (string.IsNullOrEmpty(alias))
            {
                Logger.LogError($"[FoundryClient] Loading {modelId} failed ({response.StatusCode}) and no alias known");
                return false;
            }

            Logger.LogInfo($"[FoundryClient] Retrying load with alias {alias} for {modelId}");
            using var aliasResponse = await Http.GetAsync(new Uri(_baseUri, $"models/load/{Uri.EscapeDataString(alias)}")).ConfigureAwait(false);
            if (!aliasResponse.IsSuccessStatusCode)
            {
                Logger.LogError($"[FoundryClient] Loading {modelId} (alias {alias}) failed: {aliasResponse.StatusCode}");
                return false;
            }
        }

        return await IsModelLoaded(modelId).ConfigureAwait(false);
    }

    public async Task EnsureRunning()
    {
        if (await ProbeUrlAsync(_baseUri).ConfigureAwait(false))
        {
            return;
        }

        var uri = await FindDaemonAsync().ConfigureAwait(false) ?? await StartDaemonAsync().ConfigureAwait(false);
        if (uri != null)
        {
            _baseUri = uri;
        }
    }

    private async Task<string> GetCacheDirAsync()
    {
        if (_cacheDir != null)
        {
            return _cacheDir;
        }

        using var doc = await RunFoundryJsonAsync("cache location -o json").ConfigureAwait(false);
        var path = doc != null ? GetString(doc.RootElement, "path") : string.Empty;
        if (!string.IsNullOrWhiteSpace(path))
        {
            _cacheDir = path;
            return _cacheDir;
        }

        return DefaultCacheDir;
    }

    private static async Task<JsonDocument?> RunFoundryJsonAsync(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("foundry", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            });

            if (process == null)
            {
                return null;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? JsonDocument.Parse(output)
                : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[FoundryClient] 'foundry {arguments}' failed: {ex.Message}");
            return null;
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
