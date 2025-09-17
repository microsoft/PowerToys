// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleStartPage : ContentPage
{
    private readonly SampleAppListContent _appListContent = new();
    private readonly MarkdownContent sampleMarkdown = new() { Body = "# Microsoft PowerToys\n\n![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)\n\n[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)\n\n## About\n\nMicrosoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://learn.microsoft.com/windows/dev-environment/overview), head over to [learn.microsoft.com][usingPowerToys-docs-link]!\n\n|              | Current utilities: |              |\n|--------------|--------------------|--------------|\n| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |\n| [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Command Palette](https://aka.ms/PowerToysOverview_CmdPal) |\n| [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |\n| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) |\n| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse Utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |\n| [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [New+](https://aka.ms/PowerToysOverview_NewPlus) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |\n| [Peek](https://aka.ms/PowerToysOverview_Peek) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |\n| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |\n| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Workspaces](https://aka.ms/PowerToysOverview_Workspaces) |\n| [ZoomIt](https://aka.ms/PowerToysOverview_ZoomIt) |\n\n## Installing and running Microsoft PowerToys\n\n### Requirements\n\n- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.\n- x64 or ARM64 processor\n- Our installer will install the following items:\n   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version." };

    public override IContent[] GetContent() => [_appListContent, sampleMarkdown];

    public SampleStartPage()
    {
        Name = "Open";
        Title = "Sample Repository";
        Icon = GitHubIcon.IconDictionary["logo"];

        Commands = [
            new CommandContextItem(new OpenUrlCommand("https://github.com/microsoft/powertoys")
            {
                Name = "Open repository",
                Icon = GitHubIcon.IconDictionary["logo"],
            })];
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
public static class GitHubIcon
{
    public static Dictionary<string, IconInfo> IconDictionary { get; private set; }

    static GitHubIcon()
    {
        IconDictionary = new Dictionary<string, IconInfo>
            {
                { "issues", IconHelpers.FromRelativePath("Assets\\github\\issues.svg") },
                { "pr", IconHelpers.FromRelativePath("Assets\\github\\pulls.svg") },
                { "release", IconHelpers.FromRelativePath("Assets\\github\\releases.svg") },
                { "logo", IconHelpers.FromRelativePaths("Assets\\github\\github.light.svg", "Assets\\github\\github.dark.svg") },
            };
    }

    public static string GetBase64Icon(string iconPath)
    {
        if (!string.IsNullOrEmpty(iconPath))
        {
            var bytes = File.ReadAllBytes(iconPath);
            return Convert.ToBase64String(bytes);
        }

        return string.Empty;
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class SampleAppListContent : ListContent
{
    private string baseUrl = "https://github.com/microsoft/powertoys/";

    public SampleAppListContent()
    {
        GridProperties = new MediumGridLayout() { ShowTitle = true };
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new OpenUrlCommand($"{baseUrl}/issues")
            {
                Name = "Issues\n5,320",
                Icon = GitHubIcon.IconDictionary["issues"],
            }),
            new ListItem(new OpenUrlCommand($"{baseUrl}/pulls")
            {
                Name = "Pull Requests\n91",
                Icon = GitHubIcon.IconDictionary["pr"],
            }),
            new ListItem(new OpenUrlCommand($"{baseUrl}/releases")
            {
                Name = "Releases\nv0.94",
                Icon = GitHubIcon.IconDictionary["release"],
            }),
        ];
    }
}
