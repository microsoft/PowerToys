// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.DevDocs.Models;
using Wox.Infrastructure;

namespace Community.PowerToys.Run.Plugin.DevDocs
{
    public class DevDocsService
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static List<Language> _cachedLanguages = new List<Language>();
        private static Dictionary<string, List<Entry>> _cachedDocs = new Dictionary<string, List<Entry>>();

        public static bool IsLanguagesLoaded => _cachedLanguages.Count > 0;

        public static List<Language> GetLanguages()
        {
            if (_cachedLanguages.Count == 0)
            {
                // Fallback for synchronous fetching if data was not preloaded
                LoadLanguagesAsync().GetAwaiter().GetResult();
            }

            return _cachedLanguages;
        }

        public static async Task PreloadLanguagesAsync()
        {
            if (_cachedLanguages.Count == 0)
            {
                await LoadLanguagesAsync();
            }
        }

        /// <summary>
        /// Fetches the master list of available documentations from the DevDocs API.
        /// Caches the result to prevent redundant network calls.
        /// </summary>
        /// <returns>A list of available documentations.</returns>
        public static async Task<List<Language>> LoadLanguagesAsync()
        {
            if (_cachedLanguages.Count > 0)
            {
                return _cachedLanguages;
            }

            try
            {
                var response = await _client.GetStringAsync(DevDocsConfig.DevDocsApiUrl);
                var languages = JsonSerializer.Deserialize<List<Language>>(response, _jsonOptions);
                _cachedLanguages = languages ?? new List<Language>();
                return _cachedLanguages;
            }
            catch (Exception)
            {
                return new List<Language>();
            }
        }

        /// <summary>
        /// Retrieves documentation entries for a specific language slug.
        /// Uses Wox's built-in StringMatcher for consistent fuzzy search scoring across PowerToys.
        /// </summary>
        /// <param name="languageSlug">The exact slug of the requested language.</param>
        /// <param name="searching">The query string to filter the documentation entries.</param>
        /// <returns>A list of up to 50 matched documentation entries.</returns>
        public static async Task<List<Entry>> GetDocPathCached(string languageSlug, string searching)
        {
            if (!_cachedDocs.ContainsKey(languageSlug))
            {
                try
                {
                    string url = $"{DevDocsConfig.DocumentsBaseUrl}/{languageSlug}/index.json";
                    string response = await _client.GetStringAsync(url);
                    var rootObject = JsonSerializer.Deserialize<DocIndex>(response, _jsonOptions);

                    if (rootObject != null)
                    {
                        _cachedDocs[languageSlug] = rootObject.Entries;
                    }
                }
                catch
                {
                    return new List<Entry>();
                }
            }

            var entries = _cachedDocs.TryGetValue(languageSlug, out List<Entry> value) ? value : new List<Entry>();

            if (string.IsNullOrEmpty(searching))
            {
                return entries.Take(50).ToList();
            }

            return entries
                .Select(entry =>
                {
                    var match = StringMatcher.Instance.FuzzyMatch(searching, entry.Name);
                    return new { Entry = entry, Match = match };
                })
                .Where(x => x.Match.Success)
                .OrderByDescending(x => x.Match.Score)
                .Take(50) // Limiting results to prevent UI freezing
                .Select(x => x.Entry)
                .ToList();
        }
    }
}
