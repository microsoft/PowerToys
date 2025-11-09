// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using ReverseMarkdown;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using static System.Net.WebRequestMethods;
using static System.Resources.ResXFileRef;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ReleaseNotePage : Page
    {
        private static readonly HttpClient HttpClient = new();

        public ReleaseNotePage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ReleaseNotesItem item)
            {
                MarkdownBlock.Text = item.Markdown ?? string.Empty;

                /* if (!string.IsNullOrWhiteSpace(item.HeaderImageUri) &&
                    Uri.TryCreate(item.HeaderImageUri, UriKind.Absolute, out var uri))
                {
                    HeaderImage.Source = new BitmapImage(uri);
                    HeaderImage.Visibility = Visibility.Visible;
                }
                else
                {
                    HeaderImage.Source = null;
                    HeaderImage.Visibility = Visibility.Collapsed;
                } */

                GetBlogData();
            }

            base.OnNavigatedTo(e);
        }

        private static string NormalizePreCodeToFences(string html)
        {
            // ```lang\ncode\n```
            var rx = new Regex(
                @"<pre[^>]*>\s*<code(?:(?:\s+class=""[^""]*language-([a-z0-9+\-]+)[^""]*"")|[^>]*)>([\s\S]*?)</code>\s*</pre>",
                RegexOptions.IgnoreCase);

            string Repl(Match m)
            {
                var lang = m.Groups[1].Success ? m.Groups[1].Value : string.Empty;
                var code = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
                return $"```{lang}\n{code.TrimEnd()}\n```\n\n";
            }

            // Also handle <pre>…</pre> without inner <code>
            var rxPre = new Regex(@"<pre[^>]*>([\s\S]*?)</pre>", RegexOptions.IgnoreCase);

            html = rx.Replace(html, Repl);
            html = rxPre.Replace(html, m =>
            {
                var txt = System.Net.WebUtility.HtmlDecode(
                    Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty, RegexOptions.Singleline));
                return $"```\n{txt.TrimEnd()}\n```\n\n";
            });

            return html;
        }

        private async void GetBlogData()
        {
            try
            {
                var url = "https://devblogs.microsoft.com/commandline/powertoys-0-94-is-here-settings-search-shortcut-conflict-detection-and-more/".Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    return;
                }

                // 1) Figure out the site base + slug from the URL
                // Example: https://devblogs.microsoft.com/commandline/<slug>/
                var uri = new Uri(url);
                var basePath = uri.AbsolutePath.Trim('/'); // "commandline/powertoys-0-94-..."
                var firstSlash = basePath.IndexOf('/');
                if (firstSlash < 0)
                {
                    throw new InvalidOperationException("Unexpected DevBlogs URL.");
                }

                var site = basePath[..firstSlash];                 // "commandline"
                var slug = basePath[(firstSlash + 1)..].Trim('/'); // "powertoys-0-94-..."

                // 2) Call WordPress REST API for the sub-site
                var api = $"https://devblogs.microsoft.com/{site}/wp-json/wp/v2/posts?slug={Uri.EscapeDataString(slug)}&_fields=title,content,link,date,slug,id";
                var json = await HttpClient.GetStringAsync(api);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("Post not found. Check the URL/slug.");
                }

                var post = root[0];
                var html = post.GetProperty("content").GetProperty("rendered").GetString() ?? string.Empty;

                // 3) Make image/anchor URLs absolute where needed
                html = RewriteRelativeUrls(html, $"https://devblogs.microsoft.com/{site}");
                html = EnforceImageMaxWidth(html);

                // 3.1) Normalize <pre><code class="language-xxx">…</code></pre> into fenced blocks
                html = NormalizePreCodeToFences(html);

                // 4) HTML → Markdown
                var config = new Config
                {
                    GithubFlavored = true,
                    RemoveComments = true,
                    SmartHrefHandling = true,
                };
                var converter = new ReverseMarkdown.Converter(config);

                var markdown = converter.Convert(html);
                BlogTextBlock.Text = markdown;
            }
            catch (Exception ex)
            {
                BlogTextBlock.Text = $"**Error:** {ex.Message}";
            }
        }

        private static string RewriteRelativeUrls(string html, string siteBase)
        {
            // Convert src/href that start with "/" or are site-relative to absolute
#pragma warning disable CA1310 // Specify StringComparison for correctness
#pragma warning disable CA1866 // Use char overload
            string ToAbs(string url) =>
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    ? url
                    : (url.StartsWith("/") ? $"https://devblogs.microsoft.com{url}" :
                       url.StartsWith("./") || url.StartsWith("../") ? new Uri(new Uri(siteBase + "/"), url).ToString()
                                                                     : new Uri(new Uri(siteBase + "/"), url).ToString());
#pragma warning restore CA1866 // Use char overload
#pragma warning restore CA1310 // Specify StringComparison for correctness

#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
#pragma warning disable SA1117 // Parameters should be on same line or separate lines
            html = Regex.Replace(html, "(?<attr>(?:src|href))=(\"|')(?<url>[^\"']+)(\"|')",
                m => $"{m.Groups["attr"].Value}=\"{ToAbs(m.Groups["url"].Value)}\"",
                RegexOptions.IgnoreCase);
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

            return html;
        }

        private static string EnforceImageMaxWidth(string html, int maxWidth = 600)
        {
            return Regex.Replace(
                html,
                @"<img([^>]*?)>",
                m =>
                {
                    var tag = m.Value;

                    // Skip if a style already contains max-width
                    if (Regex.IsMatch(tag, @"max-width\s*:\s*\d+", RegexOptions.IgnoreCase))
                    {
                        return tag;
                    }

                    // Inject style or append to existing one
                    if (Regex.IsMatch(tag, @"style\s*=", RegexOptions.IgnoreCase))
                    {
                        return Regex.Replace(
                            tag,
                            @"style\s*=\s*(['""])(.*?)\1",
                            m2 => $"style={m2.Groups[1].Value}{m2.Groups[2].Value}; max-width:{maxWidth}px;{m2.Groups[1].Value}",
                            RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        return tag.Insert(tag.Length - 1, $" style=\"max-width:{maxWidth}px; height:auto;\"");
                    }
                },
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        private async void MarkdownView_OnLinkClicked(object sender, CommunityToolkit.WinUI.Controls.LinkClickedEventArgs e)
        {
            // Open links externally
            await Launcher.LaunchUriAsync(e.Uri);
            e.Handled = true;
        }
    }
}
