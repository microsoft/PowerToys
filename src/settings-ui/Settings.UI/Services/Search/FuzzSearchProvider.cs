// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Search.FuzzSearch;
using Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Services.Search
{
    /// <summary>
    /// A search provider that uses fuzzy string matching for settings search.
    /// </summary>
    public sealed class FuzzSearchProvider : ISearchProvider
    {
        private readonly object _lockObject = new();
        private readonly Dictionary<string, (string HeaderNorm, string DescNorm)> _normalizedTextCache = new();
        private IReadOnlyList<SettingEntry> _entries = Array.Empty<SettingEntry>();
        private bool _isReady;

        /// <inheritdoc/>
        public bool IsReady
        {
            get
            {
                lock (_lockObject)
                {
                    return _isReady;
                }
            }
        }

        /// <inheritdoc/>
        public Task InitializeAsync(IReadOnlyList<SettingEntry> entries, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);

            lock (_lockObject)
            {
                _normalizedTextCache.Clear();
                _entries = entries;
                _isReady = true;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public List<SettingEntry> Search(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            IReadOnlyList<SettingEntry> currentEntries;
            lock (_lockObject)
            {
                if (!_isReady || _entries.Count == 0)
                {
                    return [];
                }

                currentEntries = _entries;
            }

            var normalizedQuery = NormalizeString(query);
            var bag = new ConcurrentBag<(SettingEntry Hit, double Score)>();
            var po = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
            };

            try
            {
                Parallel.ForEach(currentEntries, po, entry =>
                {
                    var (headerNorm, descNorm) = GetNormalizedTexts(entry);
                    var captionScoreResult = StringMatcher.FuzzyMatch(normalizedQuery, headerNorm);
                    double score = captionScoreResult.Score;

                    if (!string.IsNullOrEmpty(descNorm))
                    {
                        var descriptionScoreResult = StringMatcher.FuzzyMatch(normalizedQuery, descNorm);
                        if (descriptionScoreResult.Success)
                        {
                            score = Math.Max(score, descriptionScoreResult.Score * 0.8);
                        }
                    }

                    if (score > 0)
                    {
                        bag.Add((entry, score));
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return [];
            }

            return bag
                .OrderByDescending(r => r.Score)
                .Select(r => r.Hit)
                .ToList();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            lock (_lockObject)
            {
                _normalizedTextCache.Clear();
                _entries = Array.Empty<SettingEntry>();
                _isReady = false;
            }
        }

        private (string HeaderNorm, string DescNorm) GetNormalizedTexts(SettingEntry entry)
        {
            if (entry.ElementUid == null && entry.Header == null)
            {
                return (NormalizeString(entry.Header), NormalizeString(entry.Description));
            }

            var key = entry.ElementUid ?? $"{entry.PageTypeName}|{entry.ElementName}";
            lock (_lockObject)
            {
                if (_normalizedTextCache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            var headerNorm = NormalizeString(entry.Header);
            var descNorm = NormalizeString(entry.Description);
            lock (_lockObject)
            {
                _normalizedTextCache[key] = (headerNorm, descNorm);
            }

            return (headerNorm, descNorm);
        }

        private static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var normalized = input.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
            var stringBuilder = new StringBuilder();
            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }
    }
}
