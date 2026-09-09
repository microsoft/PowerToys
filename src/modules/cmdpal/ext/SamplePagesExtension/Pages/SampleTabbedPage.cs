// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// Demonstrates a <see cref="TabbedPage"/>: a single command that renders a strip
/// of tabs, where each tab is its own independent page. This mirrors something
/// like a GitHub repository view (Issues / Pull Requests / Readme), including
/// count badges that populate asynchronously after the page is shown.
/// </summary>
internal sealed partial class SampleTabbedPage : TabbedPage
{
    private readonly Tab _issuesTab;
    private readonly Tab _pullRequestsTab;
    private readonly Tab _readmeTab;
    private readonly Tab[] _tabs;

    public SampleTabbedPage()
    {
        Name = "Open";
        Title = "Sample Tabbed Page";
        Icon = new IconInfo("\uE737"); // Favorite (repo-like)

        _issuesTab = new Tab("Issues", new SampleListPage() { Id = "sample.tabbed.issues", Name = "Issues" })
        {
            Icon = new IconInfo("\uE946"), // Info
        };

        _pullRequestsTab = new Tab("Pull Requests", new SampleDynamicListPage() { Id = "sample.tabbed.prs", Name = "Pull Requests" })
        {
            Icon = new IconInfo("\uE8AB"), // Switch
        };

        _readmeTab = new Tab("Readme", new SampleContentPage() { Id = "sample.tabbed.readme", Name = "Readme" })
        {
            Icon = new IconInfo("\uE8A5"), // Document
        };

        _tabs = [_issuesTab, _pullRequestsTab, _readmeTab];

        // Badges start empty and are populated in the background, after the
        // first tab is already interactive, to demonstrate lazy badge loading.
        _ = PopulateBadgesAsync();
    }

    public override ITab[] GetTabs() => _tabs;

    private async Task PopulateBadgesAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5)).ConfigureAwait(false);
            _issuesTab.Badge = "12";

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            _pullRequestsTab.Badge = "3";
        }
        catch (Exception)
        {
            // Sample code; badge population is best-effort.
        }
    }
}
