// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class SettingsPageControl : UserControl
    {
        private readonly Dictionary<string, FrameworkElement> _anchors = new();

        public SettingsPageControl()
        {
            this.InitializeComponent();
            PrimaryLinks = new ObservableCollection<PageLink>();
            SecondaryLinks = new ObservableCollection<PageLink>();
        }

        public string ModuleTitle
        {
            get { return (string)GetValue(ModuleTitleProperty); }
            set { SetValue(ModuleTitleProperty, value); }
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
            get { return (string)GetValue(SecondaryLinksHeaderProperty); }
            set { SetValue(SecondaryLinksHeaderProperty, value); }
        }

        public ObservableCollection<PageLink> SecondaryLinks
        {
            get => (ObservableCollection<PageLink>)GetValue(SecondaryLinksProperty);
            set => SetValue(SecondaryLinksProperty, value);
        }

        public object ModuleContent
        {
            get { return (object)GetValue(ModuleContentProperty); }
            set { SetValue(ModuleContentProperty, value); }
        }

        public static readonly DependencyProperty ModuleTitleProperty = DependencyProperty.Register(nameof(ModuleTitle), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(defaultValue: null));
        public static readonly DependencyProperty ModuleDescriptionProperty = DependencyProperty.Register(nameof(ModuleDescription), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(defaultValue: null));
        public static readonly DependencyProperty ModuleImageSourceProperty = DependencyProperty.Register(nameof(ModuleImageSource), typeof(Uri), typeof(SettingsPageControl), new PropertyMetadata(null));
        public static readonly DependencyProperty PrimaryLinksProperty = DependencyProperty.Register(nameof(PrimaryLinks), typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));
        public static readonly DependencyProperty SecondaryLinksHeaderProperty = DependencyProperty.Register(nameof(SecondaryLinksHeader), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty SecondaryLinksProperty = DependencyProperty.Register(nameof(SecondaryLinks), typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));
        public static readonly DependencyProperty ModuleContentProperty = DependencyProperty.Register(nameof(ModuleContent), typeof(object), typeof(SettingsPageControl), new PropertyMetadata(new Grid()));

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // PrimaryLinksControl.Focus(FocusState.Programmatic);
            var requestUrl = "https://raw.githubusercontent.com/MicrosoftDocs/windows-dev-docs/refs/heads/docs/hub/powertoys/advanced-paste.md";

            using var client = new HttpClient();
            var response = await client.GetAsync(requestUrl);
            string content = await response.Content.ReadAsStringAsync();
            docsTextBlock.Text = PreprocessMarkdown(content);
        }

        private sealed class TocItem
        {
            public string Id { get; init; } = string.Empty;

            public string Title { get; init; } = string.Empty;

            public int Level { get; init; }
        }

        private List<TocItem> BuildDocumentAndAnchors(string md)
        {
            DocHost.Children.Clear();
            _anchors.Clear();

            // Grab H1/H2
            var headings = doc.Descendants<HeadingBlock>()
                              .Where(h => h.Level is 1 or 2)
                              .ToList();

            var toc = new List<TocItem>();
            if (headings.Count == 0)
            {
                DocHost.Children.Add(new MarkdownTextBlock { Text = md });
                return toc;
            }

            // De-duplicate slugs like GitHub does
            var seen = new Dictionary<string, int>();

            for (int i = 0; i < headings.Count; i++)
            {
                var hb = headings[i];

                // Char ranges for the slice belonging to this heading
                int start = hb.Span.Start;
                int end = (i + 1 < headings.Count) ? headings[i + 1].Span.Start : md.Length;
                string sectionMd = md.Substring(start, end - start);

                // Heading text
                string title = hb.Inline?.FirstChild?.ToString() ?? "Section";

                // Slug/anchor id
                string id = MakeSlug(title);
                if (seen.TryGetValue(id, out int n)) { n++; seen[id] = n; id = $"{id}-{n}"; }
                else { seen[id] = 1; }

                toc.Add(new TocItem { Id = id, Title = title, Level = hb.Level });

                // Invisible anchor right before the slice so BringIntoView hits the correct spot
                var anchor = new Border { Height = 1, Opacity = 0, Tag = id };
                DocHost.Children.Add(anchor);
                _anchors[id] = anchor;

                // Render the section’s markdown
                var mdtb = new MarkdownTextBlock { Text = sectionMd };
                mdtb.LinkClicked += Markdown_LinkClicked;  // handle [links to #anchors] inside the doc
                DocHost.Children.Add(mdtb);
            }

            return toc;
        }

        // Click in ToC -> scroll to anchor
        private void TocList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TextBlock tb && tb.Tag is string id && _anchors.TryGetValue(id, out var target))
            {
                target.StartBringIntoView(); // Scrolls nearest ScrollViewer
            }
        }

        public static string PreprocessMarkdown(string markdown)
        {
            markdown = Regex.Replace(markdown, @"\A---\n[\s\S]*?---\n", string.Empty, RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!IMPORTANT\]\s*> - Phi Silica is not available in China.\s*", string.Empty, RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!IMPORTANT\]", "> **ℹ️ Important:**", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!NOTE\]", "> **❗ Note:**", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!TIP\]", "> **💡 Tip:**", RegexOptions.Multiline);

            return markdown;
        }
    }
}
