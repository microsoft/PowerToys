// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.WinUI.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ScoobeReleaseNotesPage : Page
    {
        private IList<PowerToysReleaseInfo> _currentReleases;
        private string _releaseNotesMarkdownText;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScoobeReleaseNotesPage"/> class.
        /// </summary>
        public ScoobeReleaseNotesPage()
        {
            this.InitializeComponent();

            // Re-apply the markdown theme workaround when the theme changes at runtime so the
            // headings/links stay readable after the user switches between light and dark.
            this.ActualThemeChanged += OnActualThemeChanged;
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            this.ActualThemeChanged -= OnActualThemeChanged;
            this.Unloaded -= OnUnloaded;
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            RefreshMarkdownTheme();
        }

        private void RefreshMarkdownTheme()
        {
            if (string.IsNullOrEmpty(_releaseNotesMarkdownText))
            {
                return;
            }

            ApplyMarkdownThemeWorkaround();

            // The MarkdownTextBlock captures heading/link brushes when it renders, so re-set the
            // text to force it to rebuild with the brushes for the now-active theme.
            ReleaseNotesMarkdown.Text = string.Empty;
            ReleaseNotesMarkdown.Text = _releaseNotesMarkdownText;
        }

        /// <summary>
        /// Regex to remove installer hash sections from the release notes.
        /// </summary>
        private const string RemoveInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+## Highlights";
        private const string RemoveHotFixInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+$";
        private const RegexOptions RemoveInstallerHashesRegexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>
        /// Regex to match markdown images with 'Hero' in the alt text.
        /// Matches: ![...Hero...](url)
        /// </summary>
        private static readonly Regex HeroImageRegex = new Regex(
            @"!\[([^\]]*Hero[^\]]*)\]\(([^)]+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex to match GitHub PR/Issue references (e.g., #41029).
        /// Only matches # followed by digits that are not already part of a markdown link.
        /// </summary>
        private static readonly Regex GitHubPrReferenceRegex = new Regex(
            @"(?<!\[)#(\d+)(?!\])",
            RegexOptions.Compiled);

        private static readonly CompositeFormat GitHubPrLinkTemplate = CompositeFormat.Parse("[#{0}](https://github.com/microsoft/PowerToys/pull/{0})");
        private static readonly CompositeFormat GitHubReleaseLinkTemplate = CompositeFormat.Parse("https://github.com/microsoft/PowerToys/releases/tag/{0}");

        private static (string Markdown, string HeroImageUrl) ProcessReleaseNotesMarkdown(IList<PowerToysReleaseInfo> releases)
        {
            if (releases == null || releases.Count == 0)
            {
                return (string.Empty, null);
            }

            StringBuilder releaseNotesHtmlBuilder = new StringBuilder(string.Empty);

            // Regex to remove installer hash sections from the release notes.
            Regex removeHashRegex = new Regex(RemoveInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            // Regex to remove installer hash sections from the release notes, since there'll be no Highlights section for hotfix releases.
            Regex removeHotfixHashRegex = new Regex(RemoveHotFixInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            string lastHeroImageUrl = null;

            int counter = 0;
            bool isFirst = true;
            foreach (var release in releases)
            {
                // Add separator between releases
                if (!isFirst)
                {
                    releaseNotesHtmlBuilder.AppendLine("---");
                    releaseNotesHtmlBuilder.AppendLine();
                }

                isFirst = false;

                var releaseUrl = string.Format(CultureInfo.InvariantCulture, GitHubReleaseLinkTemplate, release.TagName);
                releaseNotesHtmlBuilder.AppendLine(CultureInfo.InvariantCulture, $"# {release.Name}");
                string formattedDate = release.PublishedDate.ToString($"{CultureInfo.CurrentCulture.DateTimeFormat.MonthDayPattern}, yyyy", CultureInfo.CurrentCulture);
                releaseNotesHtmlBuilder.AppendLine(CultureInfo.InvariantCulture, $"{formattedDate.Replace(".", "\\.")} • [{ResourceLoaderInstance.ResourceLoader.GetString("ScoobeReleaseNotes_ViewOnGitHub")}]({releaseUrl})");
                releaseNotesHtmlBuilder.AppendLine();
                releaseNotesHtmlBuilder.AppendLine("&nbsp;");
                releaseNotesHtmlBuilder.AppendLine();
                var notes = removeHashRegex.Replace(release.ReleaseNotes, "\r\n## Highlights");
                notes = notes.Replace("[github-current-release-work]", $"[github-current-release-work{++counter}]");
                notes = removeHotfixHashRegex.Replace(notes, string.Empty);

                // Find all Hero images and keep track of the last one
                var heroMatches = HeroImageRegex.Matches(notes);
                foreach (Match match in heroMatches)
                {
                    lastHeroImageUrl = match.Groups[2].Value;
                }

                // Remove Hero images from the markdown
                notes = HeroImageRegex.Replace(notes, string.Empty);

                // Convert GitHub PR/Issue references to hyperlinks
                notes = GitHubPrReferenceRegex.Replace(notes, match =>
                    string.Format(CultureInfo.InvariantCulture, GitHubPrLinkTemplate, match.Groups[1].Value));

                releaseNotesHtmlBuilder.AppendLine(notes);
                releaseNotesHtmlBuilder.AppendLine("&nbsp;");
            }

            return (releaseNotesHtmlBuilder.ToString(), lastHeroImageUrl);
        }

        private void DisplayReleaseNotes()
        {
            if (_currentReleases == null || _currentReleases.Count == 0)
            {
                ReleaseNotesMarkdown.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                LoadingProgressRing.Visibility = Visibility.Collapsed;

                // Workaround: the MarkdownTextBlock control captures its heading foreground
                // brushes from Application.Current.Resources when its theme config is created,
                // which resolves against the OS (application) theme rather than the app's
                // selected theme. When the OS is Light but PowerToys is Dark (or vice versa),
                // headings render with an unreadable color. Force the control's theme and
                // reapply correctly-themed heading brushes before the markdown is rendered.
                // TODO: Remove once the upstream control resolves brushes against the element theme.
                // Upstream fix: https://github.com/CommunityToolkit/Labs-Windows/pull/785
                ApplyMarkdownThemeWorkaround();

                var (releaseNotesMarkdown, heroImageUrl) = ProcessReleaseNotesMarkdown(_currentReleases);
                _releaseNotesMarkdownText = releaseNotesMarkdown;

                // Set the Hero image if found
                if (!string.IsNullOrEmpty(heroImageUrl))
                {
                    HeroImageHolder.Source = new BitmapImage(new Uri(heroImageUrl));
                    HeroImageHolder.Visibility = Visibility.Visible;
                }
                else
                {
                    HeroImageHolder.Visibility = Visibility.Collapsed;
                }

                ReleaseNotesMarkdown.Text = releaseNotesMarkdown;
                ReleaseNotesMarkdown.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception when displaying the release notes", ex);
            }
        }

        /// <summary>
        /// Works around the <see cref="MarkdownTextBlock"/> control pinning its heading and link
        /// brushes to the OS (application) theme instead of the element's selected theme, which makes
        /// titles/links unreadable when the OS and PowerToys themes differ. Pins the control's theme and
        /// reassigns the heading/link brushes resolved for the selected theme before the markdown renders.
        /// TODO: Remove once the upstream control resolves brushes against the element theme.
        /// Upstream fix: https://github.com/CommunityToolkit/Labs-Windows/pull/785
        /// </summary>
        private void ApplyMarkdownThemeWorkaround()
        {
            var elementTheme = App.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
            ReleaseNotesMarkdown.RequestedTheme = elementTheme;
            LinkBrushProvider.RequestedTheme = elementTheme;

            if (Resources["ReleaseNotesMarkdownConfig"] is MarkdownConfig config
                && config.Themes is MarkdownThemes themes)
            {
                // The control's Foreground is bound to TextFillColorPrimaryBrush via ThemeResource,
                // so after setting RequestedTheme it resolves to the brush for the selected theme.
                // Reuse it for the heading brushes, which the control would otherwise pin to the OS theme.
                if (ReleaseNotesMarkdown.Foreground is Brush headingForeground)
                {
                    themes.H1Foreground = headingForeground;
                    themes.H2Foreground = headingForeground;
                    themes.H3Foreground = headingForeground;
                    themes.H4Foreground = headingForeground;
                    themes.H5Foreground = headingForeground;
                    themes.H6Foreground = headingForeground;
                }

                // The link brush is likewise pinned to the OS theme's accent color, which can be
                // unreadable when the app theme differs from the OS theme. Reapply the accent brush
                // resolved for the selected theme using the hidden helper element.
                if (LinkBrushProvider.Foreground is Brush linkForeground)
                {
                    themes.LinkForeground = linkForeground;
                }
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayReleaseNotes();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is IList<PowerToysReleaseInfo> releases)
            {
                _currentReleases = releases;
            }
        }
    }
}
