// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ScoobeReleaseNotesPage : Page
    {
        private IList<PowerToysReleaseInfo> _currentReleases;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScoobeReleaseNotesPage"/> class.
        /// </summary>
        public ScoobeReleaseNotesPage()
        {
            this.InitializeComponent();
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
            foreach (var release in releases)
            {
                releaseNotesHtmlBuilder.AppendLine("# " + release.Name);
                var notes = removeHashRegex.Replace(release.ReleaseNotes, "\r\n### Highlights");
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

                var (releaseNotesMarkdown, heroImageUrl) = ProcessReleaseNotesMarkdown(_currentReleases);

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayReleaseNotes();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is IList<PowerToysReleaseInfo> releases)
            {
                _currentReleases = releases;
            }
        }
    }
}
