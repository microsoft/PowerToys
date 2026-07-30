// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.DevDocs.Models;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.DevDocs
{
    public class Main : IPlugin, IPluginI18n
    {
        public static string PluginID => "835960FE-ECD0-49D2-A940-76EBD8B0891E";

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        private PluginInitContext _context;

        public void Init(PluginInitContext context)
        {
            _context = context;

            // Fire and forget data loading on a background thread to prevent blocking the UI during initialization.
            _ = Task.Run(DevDocsService.LoadLanguagesAsync);
        }

        public List<Result> Query(Query query)
        {
            return SearchDevDocsAsync(query).GetAwaiter().GetResult();
        }

        private async Task<List<Result>> SearchDevDocsAsync(Query query)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(query.Search))
            {
                return results;
            }

            if (!DevDocsService.IsLanguagesLoaded)
            {
                results.Add(new Result { Title = Properties.Resources.plugin_loading, IcoPath = "Images/devdocs.dark.png" });
                return results;
            }

            var parts = query.Search.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string rawLanguage = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            string rawProperties = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            string languageSlug = rawLanguage;
            if (DevDocsConfig.AliasMap.TryGetValue(rawLanguage, out var mapped))
            {
                languageSlug = mapped;
            }

            Language bestMatch = null;

            if (languageSlug.Contains('~'))
            {
                // Exact version requested (e.g. "python~3.8")
                bestMatch = DevDocsService.GetLanguages().FirstOrDefault(l => l.Slug.Equals(languageSlug, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // Latest version requested. Find all matching versions and select the newest one.
                var candidates = DevDocsService.GetLanguages().Where(lang =>
                    lang.Name.Equals(languageSlug, StringComparison.OrdinalIgnoreCase) ||
                    lang.Slug.Equals(languageSlug, StringComparison.OrdinalIgnoreCase) ||
                    (lang.Alias != null && lang.Alias.Equals(languageSlug, StringComparison.OrdinalIgnoreCase)));

                bestMatch = candidates.OrderByDescending(lang =>
                {
                    // Custom parsing logic to ensure standard Version comparison (e.g. 3.11 > 3.9)
                    if (Version.TryParse(lang.Release, out var v))
                    {
                        return v;
                    }

                    return new Version(0, 0);
                })
                .FirstOrDefault();
            }

            if (bestMatch == null)
            {
                return results;
            }

            string selectedSlug = bestMatch.Slug;
            var docsList = await DevDocsService.GetDocPathCached(selectedSlug, rawProperties);

            foreach (var doc in docsList)
            {
                results.Add(new Result
                {
                    Title = doc.Name,
                    SubTitle = $"{bestMatch.Name}{(string.IsNullOrWhiteSpace(bestMatch.Release) ? string.Empty : $" ({bestMatch.Release})")}",
                    IcoPath = "Images/devdocs.dark.png",
                    Action = _ =>
                    {
                        string target = $"https://devdocs.io/{selectedSlug}/{doc.Path}";
                        if (!Helper.OpenInShell(target))
                        {
                            return false;
                        }

                        return true;
                    },
                });
            }

            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }
    }
}
