// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ReleaseNotesPage : Page
    {
        public ReleaseNotesPage()
        {
            InitializeComponent();
        }

        // Your original regex constants
        private const string RemoveInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+## Highlights";
        private const string RemoveHotFixInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+$";
        private const RegexOptions RemoveInstallerHashesRegexOptions =
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Image extraction regexes (Markdown image + HTML <img>)
        private static readonly Regex MdImageRegex =
            new(
                @"!\[(?:[^\]]*)\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HtmlImageRegex =
            new(
                @"<img[^>]*\s+src\s*=\s*[""'](?<url>[^""']+)[""'][^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // PR URL normalization:
        // 1) Markdown links whose *text* is the full PR URL -> make text "#12345"
        private static readonly Regex MdLinkWithPrUrlTextRegex =
            new(
                @"\[(?<url>https?://github\.com/microsoft/PowerToys/pull/(?<id>\d+))\]\(\k<url>\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 2) Bare PR URLs -> turn into "[#12345](url)"
        private static readonly Regex BarePrUrlRegex =
            new(
                @"(?<!\()(?<url>https?://github\.com/microsoft/PowerToys/pull/(?<id>\d+))(?!\))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ObservableCollection<ReleaseNotesItem> ReleaseItems { get; } = new();

        // Fetch, group (by Major.Minor), clean, extract first image, and build items
        private async Task<IList<ReleaseNotesItem>> GetGroupedReleaseNotesAsync()
        {
            // Fetch GitHub releases using system proxy & user-agent
            using var proxyClientHandler = new HttpClientHandler
            {
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                Proxy = WebRequest.GetSystemWebProxy(),
                PreAuthenticate = true,
            };

            using var client = new HttpClient(proxyClientHandler);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");

            string json = await client.GetStringAsync("https://api.github.com/repos/microsoft/PowerToys/releases");

            // NOTE: PowerToysReleaseInfo + SourceGenerationContextContext are assumed to exist in your project
            IList<PowerToysReleaseInfo> releases =
                JsonSerializer.Deserialize<IList<PowerToysReleaseInfo>>(
                    json, SourceGenerationContextContext.Default.IListPowerToysReleaseInfo)!;

            // Prepare hash-removal regexes
            var removeHashRegex = new Regex(RemoveInstallerHashesRegex, RemoveInstallerHashesRegexOptions);
            var removeHotfixHashRegex = new Regex(RemoveHotFixInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            // Parse versions; keep ones that contain x.y.z (handles "v0.93.2" etc.)
            var parsed = releases
                .Select(r => new
                {
                    Release = r,
                    Version = TryParseSemVer(r.TagName ?? r.Name, out var v) ? v : null,
                })
                .Where(x => x.Version is not null)
                .ToList();

            // Group by Major.Minor (e.g., "0.93"), order groups by newest published date
            var groups = parsed
                .GroupBy(x => $"{x.Version!.Major}.{x.Version!.Minor}")
                .OrderByDescending(g => g.Max(x => x.Release.PublishedDate))
                .ToList();

            var items = new List<ReleaseNotesItem>();

            foreach (var g in groups)
            {
                // Order subreleases by version (patch desc), then date desc
                var ordered = g.OrderByDescending(x => x.Version)
                               .ThenByDescending(x => x.Release.PublishedDate)
                               .ToList();

                // Title is the highest patch tag (e.g., "0.93.2"), trimmed of any leading 'v'
                var top = ordered.First();
                var title = TrimLeadingV(top.Release.TagName ?? top.Release.Name);

                var sb = new StringBuilder();
                int counter = 0;
                string headerImage = null;

                for (int i = 0; i < ordered.Count; i++)
                {
                    var r = ordered[i].Release;

                    // Clean installer hash sections
                    var cleaned = removeHashRegex.Replace(r.ReleaseNotes ?? string.Empty, "\r\n### Highlights");
                    cleaned = cleaned.Replace("[github-current-release-work]", $"[github-current-release-work{++counter}]");
                    cleaned = removeHotfixHashRegex.Replace(cleaned, string.Empty);

                    // Capture & remove FIRST image across the whole group (only once)
                    if (headerImage is null)
                    {
                        var (withoutFirstImage, foundUrl) = RemoveFirstImageAndGetUrl(cleaned);
                        if (!string.IsNullOrWhiteSpace(foundUrl))
                        {
                            headerImage = foundUrl;
                            cleaned = withoutFirstImage;
                        }
                    }

                    // Normalize PR links to show "#12345" like GitHub
                    cleaned = NormalizeGitHubPrLinks(cleaned);

                    if (i > 0)
                    {
                        // Horizontal rule between subreleases within the same group
                        sb.AppendLine("\r\n---\r\n");
                    }

                    // Keep a per-subrelease header for context (optional)
                    var header = $"# {TrimLeadingV(r.TagName ?? r.Name)}";
                    sb.AppendLine(header);
                    sb.AppendLine(cleaned);
                }

                items.Add(new ReleaseNotesItem
                {
                    Title = title,
                    VersionGroup = g.Key,
                    PublishedDate = ordered.Max(x => x.Release.PublishedDate),
                    Markdown = sb.ToString(),
                    HeaderImageUri = headerImage,
                });
            }

            return items;
        }

        // Turn "https://github.com/microsoft/PowerToys/pull/41853" into "[#41853](...)".
        // Also, if the markdown link text equals the full URL, rewrite it to "#41853".
        private static string NormalizeGitHubPrLinks(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return markdown;
            }

            // Case 1: [https://github.com/.../pull/12345](https://github.com/.../pull/12345)
            markdown = MdLinkWithPrUrlTextRegex.Replace(
                markdown,
                m =>
                {
                    var id = m.Groups["id"].Value;
                    var url = m.Groups["url"].Value;
                    return $"[#{id}]({url})";
                });

            // Case 2: bare https://github.com/.../pull/12345  (not already inside link markup)
            markdown = BarePrUrlRegex.Replace(
                markdown,
                m =>
                {
                    var id = m.Groups["id"].Value;
                    var url = m.Groups["url"].Value;
                    return $"[#{id}]({url})";
                });

            return markdown;
        }

        // Extract and remove the first image (Markdown or HTML) from a markdown string
        private static (string Cleaned, string Url) RemoveFirstImageAndGetUrl(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return (markdown, null);
            }

            // Markdown image first: ![alt](url "title")
            var m = MdImageRegex.Match(markdown);
            if (m.Success)
            {
                var url = m.Groups["url"].Value.Trim();
                var cleaned = MdImageRegex.Replace(markdown, string.Empty, 1);
                cleaned = CollapseExtraBlankLines(cleaned);
                return (cleaned, url);
            }

            // Fallback: HTML <img src="...">
            m = HtmlImageRegex.Match(markdown);
            if (m.Success)
            {
                var url = m.Groups["url"].Value.Trim();
                var cleaned = HtmlImageRegex.Replace(markdown, string.Empty, 1);
                cleaned = CollapseExtraBlankLines(cleaned);
                return (cleaned, url);
            }

            return (markdown, null);
        }

        private static string CollapseExtraBlankLines(string s)
        {
            s = s.Trim();
            s = Regex.Replace(s, @"(\r?\n){3,}", "\r\n\r\n");
            return s;
        }

        // Try to parse the first x.y.z version found in a string (supports leading 'v')
        private static bool TryParseSemVer(string s, out Version v)
        {
            v = null;
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            var m = Regex.Match(s, @"(?<!\d)(\d+)\.(\d+)\.(\d+)");
            if (!m.Success)
            {
                return false;
            }

            if (int.TryParse(m.Groups[1].Value, out var major) &&
                int.TryParse(m.Groups[2].Value, out var minor) &&
                int.TryParse(m.Groups[3].Value, out var patch))
            {
                v = new Version(major, minor, patch);
                return true;
            }

            return false;
        }

        private static string TrimLeadingV(string s) =>
            string.IsNullOrEmpty(s) ? s : (s.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? s[1..] : s);

        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (ReleaseItems.Count == 0)
            {
                var items = await GetGroupedReleaseNotesAsync();
                foreach (var item in items)
                {
                    ReleaseItems.Add(item);
                }

                if (ReleaseItems.Count > 0 && ReleasesList is not null)
                {
                    ReleasesList.SelectedIndex = 0;
                }
            }
        }

        private void ReleasesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReleasesList.SelectedItem is ReleaseNotesItem item)
            {
                ContentFrame.Navigate(typeof(ReleaseNotePage), item);
            }
        }
    }
}
