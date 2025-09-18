// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Markdig;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class SettingsPageControl : UserControl
    {
        private readonly Dictionary<string, FrameworkElement> _anchors = new();
        private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UsePreciseSourceLocation().Build();

        // For section flyouts
        private string _fullMarkdown = string.Empty;

        private sealed class HeadingInfo
        {
#pragma warning disable SA1401 // Fields should be private
            public string Id = string.Empty;
            public string Title = string.Empty;
            public int Level;
            public int Start;
            public int End;
#pragma warning restore SA1401 // Fields should be private
        }

        private readonly List<HeadingInfo> _allHeadings = new();

        public SettingsPageControl()
        {
            InitializeComponent();
            PrimaryLinks = new ObservableCollection<PageLink>();
            SecondaryLinks = new ObservableCollection<PageLink>();
        }

        public string ModuleTitle
        {
            get => (string)GetValue(ModuleTitleProperty);
            set => SetValue(ModuleTitleProperty, value);
        }

        public string ModuleDescription
        {
            get => (string)GetValue(ModuleDescriptionProperty);
            set => SetValue(ModuleDescriptionProperty, value);
        }

        public Uri ModuleImageSource
        {
            get => (Uri)GetValue(ModuleImageSourceProperty);
            set => SetValue(ModuleImageSourceProperty, value);
        }

        public ObservableCollection<PageLink> PrimaryLinks
        {
            get => (ObservableCollection<PageLink>)GetValue(PrimaryLinksProperty);
            set => SetValue(PrimaryLinksProperty, value);
        }

        public string SecondaryLinksHeader
        {
            get => (string)GetValue(SecondaryLinksHeaderProperty);
            set => SetValue(SecondaryLinksHeaderProperty, value);
        }

        public ObservableCollection<PageLink> SecondaryLinks
        {
            get => (ObservableCollection<PageLink>)GetValue(SecondaryLinksProperty);
            set => SetValue(SecondaryLinksProperty, value);
        }

        public object ModuleContent
        {
            get => GetValue(ModuleContentProperty);
            set => SetValue(ModuleContentProperty, value);
        }

        public static readonly DependencyProperty ModuleTitleProperty =
            DependencyProperty.Register(nameof(ModuleTitle), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ModuleDescriptionProperty =
            DependencyProperty.Register(nameof(ModuleDescription), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ModuleImageSourceProperty =
            DependencyProperty.Register(nameof(ModuleImageSource), typeof(Uri), typeof(SettingsPageControl), new PropertyMetadata(null));

        public static readonly DependencyProperty PrimaryLinksProperty =
            DependencyProperty.Register(nameof(PrimaryLinks), typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));

        public static readonly DependencyProperty SecondaryLinksHeaderProperty =
            DependencyProperty.Register(nameof(SecondaryLinksHeader), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty SecondaryLinksProperty =
            DependencyProperty.Register(nameof(SecondaryLinks), typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));

        public static readonly DependencyProperty ModuleContentProperty =
            DependencyProperty.Register(nameof(ModuleContent), typeof(object), typeof(SettingsPageControl), new PropertyMetadata(new Grid()));

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _ = LoadAndRenderAsync("https://raw.githubusercontent.com/MicrosoftDocs/windows-dev-docs/refs/heads/docs/hub/powertoys/advanced-paste.md");
        }

        private sealed class TocItem
        {
            public string Id { get; init; } = string.Empty;

            public string Title { get; init; } = string.Empty;

            public int Level { get; init; }
        }

        private async Task LoadAndRenderAsync(string requestUrl)
        {
            using var client = new HttpClient();
            var raw = await client.GetStringAsync(requestUrl);

            // Preprocess with knowledge of the file URL (so we resolve ../images/...)
            var md = PreprocessMarkdown(raw, requestUrl);

            var tocItems = BuildDocumentAndAnchors(md);

            // Bind ToC (indent H2/H3 a bit)
            TocList.ItemsSource = tocItems.Select(i => new
            {
                i.Id,
                i.Title,
                Indent = new Thickness((i.Level - 1) * 12, 6, 8, 6),
            }).ToList();
        }

        private List<TocItem> BuildDocumentAndAnchors(string md)
        {
            _fullMarkdown = md;
            DocHost.Children.Clear();
            _anchors.Clear();
            _allHeadings.Clear();

            var doc = Markdig.Markdown.Parse(md, _pipeline);

            // Build slugs for ALL headings (H1..H6) so section flyouts can target any level
            var rawHeadings = doc.Descendants<HeadingBlock>().ToList();
            var seen = new Dictionary<string, int>();

            foreach (var hb in rawHeadings)
            {
                string title = hb.Inline?.FirstChild?.ToString() ?? "Section";
                string id = MakeSlug(title);

                if (seen.TryGetValue(id, out int n))
                {
                    n++;
                    seen[id] = n;
                    id = $"{id}-{n}";
                }
                else
                {
                    seen[id] = 1;
                }

                _allHeadings.Add(new HeadingInfo
                {
                    Id = id,
                    Title = title,
                    Level = hb.Level,
                    Start = hb.Span.Start,
                    End = md.Length, // fixed below
                });
            }

            // Compute section End = next heading with level <= current level (or EOF)
            for (int i = 0; i < _allHeadings.Count; i++)
            {
                for (int j = i + 1; j < _allHeadings.Count; j++)
                {
                    if (_allHeadings[j].Level <= _allHeadings[i].Level)
                    {
                        _allHeadings[i].End = _allHeadings[j].Start;
                        break;
                    }
                }
            }

            // Render the document in H2/H3 chunks for this UI
            var headings = _allHeadings.Where(h => h.Level is 2 or 3).ToList();
            var toc = new List<TocItem>();

            if (headings.Count == 0)
            {
                DocHost.Children.Add(new MarkdownTextBlock { Text = md });
                return toc;
            }

            foreach (var h in headings)
            {
                toc.Add(new TocItem { Id = h.Id, Title = h.Title, Level = h.Level });

                // Invisible anchor just before the rendered section
                var anchor = new Border { Height = 0, Opacity = 0, Tag = h.Id };
                DocHost.Children.Add(anchor);
                _anchors[h.Id] = anchor;

                // Render this section’s markdown (include heading line)
                string sectionMd = md.Substring(h.Start, h.End - h.Start);
                var mdtb = new MarkdownTextBlock { Text = sectionMd };

                // NOTE: some toolkit versions use LinkClicked; you used OnLinkClicked in your snippet.
                // Keep your version:
                mdtb.OnLinkClicked += Markdown_LinkClicked;

                DocHost.Children.Add(mdtb);
            }

            return toc;
        }

        private void Markdown_LinkClicked(object sender, CommunityToolkit.WinUI.Controls.LinkClickedEventArgs e)
        {
            var uri = e.Uri;
            if (uri is null)
            {
                return;
            }

            string anchorId = null;

#pragma warning disable CA1310 // Specify StringComparison for correctness
#pragma warning disable CA1866 // Use char overload
            if (!uri.IsAbsoluteUri && uri.OriginalString.StartsWith("#"))
            {
                anchorId = uri.OriginalString.TrimStart('#');
            }
            else if (uri.IsAbsoluteUri && !string.IsNullOrEmpty(uri.Fragment))
            {
                anchorId = uri.Fragment.TrimStart('#');
            }
#pragma warning restore CA1866 // Use char overload
#pragma warning restore CA1310 // Specify StringComparison for correctness

            if (!string.IsNullOrEmpty(anchorId) && _anchors.TryGetValue(anchorId, out _))
            {
                ScrollToAnchor(anchorId, TopOffset);
                return;
            }

            _ = Launcher.LaunchUriAsync(uri);
        }

        private const double TopOffset = 40; // pixels

        // Click in ToC -> scroll to anchor
        private void TocList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FrameworkElement fe && fe.Tag is string id)
            {
                ScrollToAnchor(id, TopOffset);
                return;
            }

            var idProp = e.ClickedItem?.GetType().GetProperty("Id")?.GetValue(e.ClickedItem) as string;
            if (!string.IsNullOrEmpty(idProp))
            {
                ScrollToAnchor(idProp!, TopOffset);
            }
        }

        private void ScrollToAnchor(string id, double topOffset = 0)
        {
            if (_anchors.TryGetValue(id, out var target))
            {
                var opts = new BringIntoViewOptions
                {
                    VerticalAlignmentRatio = 0.0,
                    HorizontalAlignmentRatio = 0.0,
                    AnimationDesired = true,
                };
                target.StartBringIntoView(opts);
            }
        }

        // ----------------------------
        // Public API: show a section in a flyout (for any H1..H6)
        // ----------------------------
        public bool TryShowSectionFlyout(FrameworkElement placementTarget, string sectionId, bool includeHeading = false, FlyoutPlacementMode placement = FlyoutPlacementMode.Bottom)
        {
            if (placementTarget is null || string.IsNullOrWhiteSpace(sectionId))
            {
                return false;
            }

            var h = _allHeadings.FirstOrDefault(x => string.Equals(x.Id, sectionId, StringComparison.OrdinalIgnoreCase));
            if (h is null)
            {
                return false;
            }

            var slice = _fullMarkdown.Substring(h.Start, h.End - h.Start);
            if (!includeHeading)
            {
                int nl = slice.IndexOf('\n');
                slice = nl >= 0 ? slice[(nl + 1)..] : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(slice))
            {
                return false;
            }

            var title = new TextBlock
            {
                Text = h.Title,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            };

            var body = new MarkdownTextBlock { Text = slice };
            body.OnLinkClicked += Markdown_LinkClicked;

            var content = new StackPanel { MinWidth = 320, MaxWidth = 560 };
            content.Children.Add(title);
            content.Children.Add(new ScrollViewer
            {
                Content = body,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });

            var flyout = new Microsoft.UI.Xaml.Controls.Flyout { Content = content, Placement = placement };
            flyout.ShowAt(placementTarget);
            return true;
        }

        // ----------------------------
        // Markdown preprocessor (MS Learn → standard Markdown)
        // ----------------------------
        public static string PreprocessMarkdown(string markdown, string sourceFileUrl)
        {
            // Compute the *directory* of the md file (guaranteed trailing slash)
            var baseDir = new Uri(new Uri(sourceFileUrl), ".");

            string Resolve(string url)
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }

                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    return url;
                }

                return new Uri(baseDir, url).ToString();
            }

            // 1) Strip YAML front matter at the very top
            markdown = Regex.Replace(
                markdown,
                pattern: @"\A---\s*[\s\S]*?^\s*---\s*$\r?\n?",
                replacement: string.Empty,
                options: RegexOptions.Multiline);

            // 2) Remove specific Learn notice (example you had)
            markdown = Regex.Replace(
                markdown,
                @"^>\s*\[!IMPORTANT\]\s*> - Phi Silica is not available in China\.\s*$\r?\n?",
                string.Empty,
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // 3) Convert Learn admonitions to simpler blockquotes with icons
            var admonitions = new (string Pattern, string Replacement)[]
            {
                (@"^>\s*\[!IMPORTANT\]", "> **ℹ️ Important:**"),
                (@"^>\s*\[!NOTE\]",      "> **❗ Note:**"),
                (@"^>\s*\[!TIP\]",       "> **💡 Tip:**"),
                (@"^>\s*\[!WARNING\]",   "> **⚠️ Warning:**"),
                (@"^>\s*\[!CAUTION\]",   "> **⚠️ Caution:**"),
            };
            foreach (var (pat, rep) in admonitions)
                markdown = Regex.Replace(markdown, pat, rep, RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // 4) Convert :::image ... ::: blocks
            markdown = Regex.Replace(
                markdown,
                @":::image\s+(?<attrs>.*?):::",
                m =>
                {
                    string attrs = m.Groups["attrs"].Value;

                    static string A(string attrs, string name)
                    {
                        var mm = Regex.Match(attrs, $@"\b{name}\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
                        return mm.Success ? mm.Groups[1].Value : string.Empty;
                    }

                    string src = A(attrs, "source");
                    string alt = A(attrs, "alt-text");
                    string lightbox = A(attrs, "lightbox");
                    string link = A(attrs, "link");

                    src = Resolve(src);
                    lightbox = Resolve(lightbox);
                    link = Resolve(link);

                    var img = $"![{alt}]";
                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        img += $"({src})";
                    }

                    var href = !string.IsNullOrWhiteSpace(link) ? link : lightbox;
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        img = $"[{img}]({href})";
                    }

                    return img + "\n";
                },
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // 5) Resolve relative links in standard markdown
            markdown = Regex.Replace(
                markdown,
                @"\]\((?!https?://|mailto:|data:|#)(?<rel>[^)]+)\)",
                m =>
                {
                    var rel = m.Groups["rel"].Value.Trim();
                    var abs = Resolve(rel);
                    return $"]({abs})";
                });

            return markdown;
        }

        private static string MakeSlug(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return "section";
            }

            var slug = s.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^\p{L}\p{Nd}\s-]", string.Empty);
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug;
        }
    }

    // ------------------------------------------------------------
    // Attached property: set DocSectionFlyout.SectionId on any Button
    // inside ModuleContent, and it will open a flyout for that section.
    // ------------------------------------------------------------
#pragma warning disable SA1402 // File may only contain a single type
    public static class DocSectionFlyout
#pragma warning restore SA1402 // File may only contain a single type
    {
        public static readonly DependencyProperty SectionIdProperty =
            DependencyProperty.RegisterAttached(
                "SectionId",
                typeof(string),
                typeof(DocSectionFlyout),
                new PropertyMetadata(null, OnSectionIdChanged));

        public static void SetSectionId(DependencyObject obj, string value) => obj.SetValue(SectionIdProperty, value);

        public static string GetSectionId(DependencyObject obj) => (string)obj.GetValue(SectionIdProperty);

        public static readonly DependencyProperty IncludeHeadingProperty =
            DependencyProperty.RegisterAttached(
                "IncludeHeading",
                typeof(bool),
                typeof(DocSectionFlyout),
                new PropertyMetadata(false));

        public static void SetIncludeHeading(DependencyObject obj, bool value) => obj.SetValue(IncludeHeadingProperty, value);

        public static bool GetIncludeHeading(DependencyObject obj) => (bool)obj.GetValue(IncludeHeadingProperty);

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.RegisterAttached(
                "Placement",
                typeof(FlyoutPlacementMode),
                typeof(DocSectionFlyout),
                new PropertyMetadata(FlyoutPlacementMode.Bottom));

        public static void SetPlacement(DependencyObject obj, FlyoutPlacementMode value) => obj.SetValue(PlacementProperty, value);

        public static FlyoutPlacementMode GetPlacement(DependencyObject obj) => (FlyoutPlacementMode)obj.GetValue(PlacementProperty);

        private static void OnSectionIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ButtonBase btn)
            {
                btn.Click -= Button_Click;
                if (e.NewValue is string { Length: > 0 })
                {
                    btn.Click += Button_Click;
                }
            }
        }

        private static void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe)
            {
                return;
            }

            // Find nearest SettingsPageControl ancestor
            var parent = fe as DependencyObject;
            SettingsPageControl host = null;
            while (parent is not null)
            {
                parent = VisualTreeHelper.GetParent(parent);
                if (parent is SettingsPageControl spc)
                {
                    host = spc;
                    break;
                }
            }

            if (host is null)
            {
                return;
            }

            var id = GetSectionId(fe);
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var includeHeading = GetIncludeHeading(fe);
            var placement = GetPlacement(fe);

            host.TryShowSectionFlyout(fe, id, includeHeading, placement);
        }
    }
}
