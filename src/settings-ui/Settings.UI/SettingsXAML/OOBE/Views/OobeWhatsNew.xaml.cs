// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.UI.Controls;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class OobeWhatsNew : Page
    {
        // Contains information for a release. Used to deserialize release JSON info from GitHub.
        private sealed class PowerToysReleaseInfo
        {
            [JsonPropertyName("published_at")]
            public DateTimeOffset PublishedDate { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("tag_name")]
            public string TagName { get; set; }

            [JsonPropertyName("body")]
            public string ReleaseNotes { get; set; }
        }

        public OobePowerToysModule ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OobeWhatsNew"/> class.
        /// </summary>
        public OobeWhatsNew()
        {
            this.InitializeComponent();
            ViewModel = new OobePowerToysModule(OobeShellPage.OobeShellHandler.Modules[(int)PowerToysModules.WhatsNew]);
            DataContext = ViewModel;
        }

        /// <summary>
        /// Regex to remove installer hash sections from the release notes.
        /// </summary>
        private const string RemoveInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+## Highlights";
        private const string RemoveHotFixInstallerHashesRegex = @"(\r\n)+## Installer Hashes(\r\n.*)+$";
        private const RegexOptions RemoveInstallerHashesRegexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static async Task<string> GetReleaseNotesMarkdown()
        {
            string releaseNotesJSON = string.Empty;

            // Let's use system proxy
            using var proxyClientHandler = new HttpClientHandler
            {
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                Proxy = WebRequest.GetSystemWebProxy(),
                PreAuthenticate = true,
            };

            using var getReleaseInfoClient = new HttpClient(proxyClientHandler);

            // GitHub APIs require sending an user agent
            // https://docs.github.com/rest/overview/resources-in-the-rest-api#user-agent-required
            getReleaseInfoClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");
            releaseNotesJSON = await getReleaseInfoClient.GetStringAsync("https://api.github.com/repos/microsoft/PowerToys/releases");
            IList<PowerToysReleaseInfo> releases = JsonSerializer.Deserialize<IList<PowerToysReleaseInfo>>(releaseNotesJSON);

            // Get the latest releases
            var latestReleases = releases.OrderByDescending(release => release.PublishedDate).Take(5);

            StringBuilder releaseNotesHtmlBuilder = new StringBuilder(string.Empty);

            // Regex to remove installer hash sections from the release notes.
            Regex removeHashRegex = new Regex(RemoveInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            // Regex to remove installer hash sections from the release notes, since there'll be no Highlights section for hotfix releases.
            Regex removeHotfixHashRegex = new Regex(RemoveHotFixInstallerHashesRegex, RemoveInstallerHashesRegexOptions);

            foreach (var release in latestReleases)
            {
                releaseNotesHtmlBuilder.AppendLine("# " + release.Name);
                var notes = removeHashRegex.Replace(release.ReleaseNotes, "\r\n## Highlights");
                notes = removeHotfixHashRegex.Replace(notes, string.Empty);
                releaseNotesHtmlBuilder.AppendLine(notes);
                releaseNotesHtmlBuilder.AppendLine("&nbsp;");
            }

            return releaseNotesHtmlBuilder.ToString();
        }

        private async Task Reload()
        {
            try
            {
                string releaseNotesMarkdown = await GetReleaseNotesMarkdown();

                ProxyWarningInfoBar.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                ErrorInfoBar.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

                ReleaseNotesMarkdown.Text = releaseNotesMarkdown;
                ReleaseNotesMarkdown.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                LoadingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            catch (HttpRequestException httpEx)
            {
                Logger.LogError("Exception when loading the release notes", httpEx);
                if (httpEx.Message.Contains("407", StringComparison.CurrentCulture))
                {
                    ProxyWarningInfoBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
                else
                {
                    ErrorInfoBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception when loading the release notes", ex);
                ErrorInfoBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            finally
            {
                LoadingProgressRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }

        private async void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await Reload();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.LogOpeningModuleEvent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.LogClosingModuleEvent();
        }

        private void ReleaseNotesMarkdown_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            if (Uri.TryCreate(e.Link, UriKind.Absolute, out Uri link))
            {
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    Process.Start(new ProcessStartInfo(link.ToString()) { UseShellExecute = true });
                });
            }
        }
    }
}
