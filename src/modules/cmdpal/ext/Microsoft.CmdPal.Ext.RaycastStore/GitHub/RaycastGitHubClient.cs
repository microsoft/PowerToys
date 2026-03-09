// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.RaycastStore.GitHub;

internal sealed class RaycastGitHubClient : IDisposable
{
    private const string RepoOwner = "raycast";
    private const string RepoName = "extensions";
    private const string DefaultBranch = "main";
    private const string ExtensionsPrefix = "extensions/";
    private const string PackageJsonSuffix = "/package.json";
    private const string GitHubApiBase = "https://api.github.com";
    private const string RawGitHubBase = "https://raw.githubusercontent.com/raycast/extensions/main/extensions";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ManifestCacheTtl = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly Lock _cacheLock = new();
    private readonly Dictionary<string, (RaycastExtensionInfo Info, DateTimeOffset CachedAt)> _manifestCache = new(StringComparer.OrdinalIgnoreCase);

    private List<string>? _extensionDirectories;
    private DateTimeOffset _directoriesCachedAt;
    private GitHubRateLimit? _lastRateLimit;

    public GitHubRateLimit? RateLimit => _lastRateLimit;

    public RaycastGitHubClient()
    {
        HttpClientHandler handler = new()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _httpClient = new HttpClient(handler, disposeHandler: true);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PowerToys-CmdPal/1.0");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<List<string>> GetExtensionDirectoriesAsync(CancellationToken ct = default)
    {
        using (_cacheLock.EnterScope())
        {
            if (_extensionDirectories != null && DateTimeOffset.UtcNow - _directoriesCachedAt < CacheTtl)
            {
                return _extensionDirectories;
            }
        }

        string treeUrl = GitHubApiBase + "/repos/" + RepoOwner + "/" + RepoName + "/git/trees/" + DefaultBranch + "?recursive=1";
        string? response = await SendGitHubRequestAsync(treeUrl, ct);
        if (response == null)
        {
            return new List<string>();
        }

        GitTreeResponse? treeResponse = JsonSerializer.Deserialize(response, RaycastStoreJsonContext.Default.GitTreeResponse);
        if (treeResponse?.Tree == null)
        {
            return new List<string>();
        }

        List<string> directories = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < treeResponse.Tree.Count; i++)
        {
            GitTreeEntry entry = treeResponse.Tree[i];
            string? path = entry.Path;
            if (path == null || !path.StartsWith(ExtensionsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string remaining = path.Substring(ExtensionsPrefix.Length);
            int slashIndex = remaining.IndexOf('/');
            string dirName;

            if (slashIndex > 0)
            {
                dirName = remaining.Substring(0, slashIndex);
            }
            else
            {
                if (entry.Type != "tree")
                {
                    continue;
                }

                dirName = remaining;
            }

            if (dirName.Length > 0 && !dirName.StartsWith('.') && seen.Add(dirName))
            {
                directories.Add(dirName);
            }
        }

        using (_cacheLock.EnterScope())
        {
            _extensionDirectories = directories;
            _directoriesCachedAt = DateTimeOffset.UtcNow;
        }

        return directories;
    }

    public async Task<RaycastExtensionInfo?> GetExtensionInfoAsync(string extensionDir, CancellationToken ct = default)
    {
        using (_cacheLock.EnterScope())
        {
            if (_manifestCache.TryGetValue(extensionDir, out var cached) && DateTimeOffset.UtcNow - cached.CachedAt < ManifestCacheTtl)
            {
                return cached.Info;
            }
        }

        string contentsUrl = $"{GitHubApiBase}/repos/{RepoOwner}/{RepoName}/contents/extensions/{extensionDir}/package.json?ref={DefaultBranch}";
        string? response = await SendGitHubRequestAsync(contentsUrl, ct);
        if (response == null)
        {
            return null;
        }

        GitHubContentResponse? contentResponse = JsonSerializer.Deserialize(response, RaycastStoreJsonContext.Default.GitHubContentResponse);
        if (contentResponse?.Content == null || contentResponse.Encoding != "base64")
        {
            return null;
        }

        string packageJsonText;
        try
        {
            string cleanBase64 = contentResponse.Content.Replace("\n", string.Empty);
            byte[] bytes = Convert.FromBase64String(cleanBase64);
            packageJsonText = Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            Logger.LogError("Failed to decode base64 for " + extensionDir);
            return null;
        }

        RaycastPackageJson? packageJson;
        try
        {
            packageJson = JsonSerializer.Deserialize(packageJsonText, RaycastStoreJsonContext.Default.RaycastPackageJson);
        }
        catch (JsonException ex)
        {
            Logger.LogError("Failed to parse package.json for " + extensionDir + ": " + ex.Message);
            return null;
        }

        if (packageJson == null)
        {
            return null;
        }

        // Windows filtering: if platforms specified, must include "windows"
        bool isWindowsConfirmed = false;
        bool isNonWindows = false;
        if (packageJson.Platforms != null && packageJson.Platforms.Count > 0)
        {
            for (int i = 0; i < packageJson.Platforms.Count; i++)
            {
                if (string.Equals(packageJson.Platforms[i], "windows", StringComparison.OrdinalIgnoreCase))
                {
                    isWindowsConfirmed = true;
                    break;
                }
            }

            if (!isWindowsConfirmed)
            {
                isNonWindows = true;
            }
        }

        if (isNonWindows)
        {
            return null;
        }

        string iconUrl = string.Empty;
        if (!string.IsNullOrEmpty(packageJson.Icon))
        {
            iconUrl = $"{RawGitHubBase}/{extensionDir}/assets/{packageJson.Icon}";
        }

        RaycastExtensionInfo info = new()
        {
            Name = packageJson.Name ?? extensionDir,
            Title = packageJson.Title ?? extensionDir,
            Description = packageJson.Description ?? string.Empty,
            Author = packageJson.Author ?? string.Empty,
            Icon = packageJson.Icon ?? string.Empty,
            Version = packageJson.Version ?? string.Empty,
            License = packageJson.License ?? string.Empty,
            DirectoryName = extensionDir,
            Categories = packageJson.Categories ?? new List<string>(),
            Contributors = packageJson.Contributors ?? new List<string>(),
            IsWindowsConfirmed = isWindowsConfirmed,
            IconUrl = iconUrl,
        };

        if (packageJson.Commands != null)
        {
            for (int j = 0; j < packageJson.Commands.Count; j++)
            {
                RaycastPackageCommand cmd = packageJson.Commands[j];
                info.Commands.Add(new RaycastCommand
                {
                    Name = cmd.Name ?? string.Empty,
                    Title = cmd.Title ?? string.Empty,
                    Description = cmd.Description ?? string.Empty,
                    Mode = cmd.Mode ?? string.Empty,
                });
            }
        }

        using (_cacheLock.EnterScope())
        {
            _manifestCache[extensionDir] = (info, DateTimeOffset.UtcNow);
        }

        return info;
    }

    public async Task<List<RaycastExtensionInfo>> GetExtensionInfoBatchAsync(List<string> extensionDirs, CancellationToken ct = default)
    {
        List<RaycastExtensionInfo> results = new();
        for (int batchStart = 0; batchStart < extensionDirs.Count; batchStart += 10)
        {
            ct.ThrowIfCancellationRequested();
            int batchEnd = Math.Min(batchStart + 10, extensionDirs.Count);
            Task<RaycastExtensionInfo?>[] tasks = new Task<RaycastExtensionInfo?>[batchEnd - batchStart];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = GetExtensionInfoAsync(extensionDirs[batchStart + i], ct);
            }

            await Task.WhenAll(tasks);
            for (int j = 0; j < tasks.Length; j++)
            {
                RaycastExtensionInfo? info = tasks[j].Result;
                if (info != null)
                {
                    results.Add(info);
                }
            }
        }

        return results;
    }

    public async Task<List<RaycastExtensionInfo>> SearchExtensionsAsync(string query, int maxResults = 25, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<RaycastExtensionInfo>();
        }

        List<string> allDirs = await GetExtensionDirectoriesAsync(ct);
        if (allDirs.Count == 0)
        {
            return new List<RaycastExtensionInfo>();
        }

        List<string> matchingDirs = new();
        for (int i = 0; i < allDirs.Count; i++)
        {
            if (matchingDirs.Count >= maxResults)
            {
                break;
            }

            if (allDirs[i].Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                matchingDirs.Add(allDirs[i]);
            }
        }

        if (matchingDirs.Count == 0)
        {
            return new List<RaycastExtensionInfo>();
        }

        return await GetExtensionInfoBatchAsync(matchingDirs, ct);
    }

    public async Task<string?> GetExtensionReadmeAsync(string extensionDir, CancellationToken ct = default)
    {
        string contentsUrl = $"{GitHubApiBase}/repos/{RepoOwner}/{RepoName}/contents/extensions/{extensionDir}/README.md?ref={DefaultBranch}";
        string? response = await SendGitHubRequestAsync(contentsUrl, ct);
        if (response == null)
        {
            return null;
        }

        GitHubContentResponse? contentResponse = JsonSerializer.Deserialize(response, RaycastStoreJsonContext.Default.GitHubContentResponse);
        if (contentResponse?.Content == null || contentResponse.Encoding != "base64")
        {
            return null;
        }

        try
        {
            string cleanBase64 = contentResponse.Content.Replace("\n", string.Empty);
            byte[] bytes = Convert.FromBase64String(cleanBase64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public bool IsRateLimited()
    {
        GitHubRateLimit? rateLimit = _lastRateLimit;
        return rateLimit != null && rateLimit.Remaining <= 1 && DateTimeOffset.UtcNow < rateLimit.ResetTime;
    }

    public void ClearCache()
    {
        using (_cacheLock.EnterScope())
        {
            _extensionDirectories = null;
            _manifestCache.Clear();
        }
    }

    private async Task<string?> SendGitHubRequestAsync(string url, CancellationToken ct)
    {
        try
        {
            if (IsRateLimited())
            {
                Logger.LogWarning("GitHub API rate limit reached, skipping request");
                return null;
            }

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            UpdateRateLimit(response.Headers);

            if (response.StatusCode == HttpStatusCode.Forbidden && IsRateLimited())
            {
                Logger.LogWarning("GitHub API rate limit hit (403)");
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError("GitHub API request failed: " + url + " — " + ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private void UpdateRateLimit(HttpResponseHeaders headers)
    {
        GitHubRateLimit rateLimit = new();
        bool hasRemaining = false;

        if (headers.TryGetValues("x-ratelimit-remaining", out var remainingValues))
        {
            using var enumerator = remainingValues.GetEnumerator();
            if (enumerator.MoveNext())
            {
                if (int.TryParse(enumerator.Current, out int remaining))
                {
                    rateLimit.Remaining = remaining;
                    hasRemaining = true;
                }
            }
        }

        if (headers.TryGetValues("x-ratelimit-limit", out var limitValues))
        {
            using var enumerator = limitValues.GetEnumerator();
            if (enumerator.MoveNext())
            {
                if (int.TryParse(enumerator.Current, out int limit))
                {
                    rateLimit.Limit = limit;
                }
            }
        }

        if (headers.TryGetValues("x-ratelimit-reset", out var resetValues))
        {
            using var enumerator = resetValues.GetEnumerator();
            if (enumerator.MoveNext())
            {
                if (long.TryParse(enumerator.Current, out long resetUnix))
                {
                    rateLimit.ResetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
                }
            }
        }

        if (hasRemaining)
        {
            _lastRateLimit = rateLimit;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
