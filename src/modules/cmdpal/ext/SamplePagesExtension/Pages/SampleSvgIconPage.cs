// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleSvgIconPage : ListPage
{
    private const string PlainTemplate = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect x="1" y="1" width="30" height="30" rx="8" fill="#E8DEF8" />
          <circle cx="16" cy="16" r="9.5" fill="#7A3E9D" stroke="#33243D" stroke-width="1.5" />
          <path d="M11.5 16.2l3 3 6.4-7" fill="none" stroke="#FFFFFF" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        """;

    private const string ThemedTemplate = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect x="1" y="1" width="30" height="30" rx="8" fill="{{AccentColor}}" fill-opacity="0.18" />
          <circle cx="16" cy="16" r="9.5" fill="{{AccentColor}}" stroke="{{ThemeColor}}" stroke-width="1.5" />
          <path d="M11.5 16.2l3 3 6.4-7" fill="none" stroke="#FFFFFF" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        """;

    private const string ThemedFluentUploadTemplate = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" color="{{ThemeColor}}">
          <path
            fill="currentColor"
            d="M10 18q.128 0 .254-.004a5.5 5.5 0 0 1-.698-1.083c-.536-.207-1.098-.793-1.578-1.821A9.3 9.3 0 0 1 7.42 13.5h1.672q.096-.52.284-1h-2.17A15 15 0 0 1 7 10c0-.883.073-1.725.206-2.5h5.588c.092.541.156 1.115.186 1.713q.48-.138.992-.188a16 16 0 0 0-.165-1.525h2.733c.251.656.406 1.36.448 2.094q.543.276 1.008.66A8 8 0 1 0 10 18M10 3c.657 0 1.407.59 2.022 1.908.217.466.406 1.002.559 1.592H7.419c.153-.59.342-1.126.56-1.592C8.592 3.59 9.342 3 10 3M7.072 4.485A10.5 10.5 0 0 0 6.389 6.5H3.936a7.02 7.02 0 0 1 3.778-3.118c-.241.33-.456.704-.642 1.103M6.192 7.5A16 16 0 0 0 6 10c0 .87.067 1.712.193 2.5H3.46A7 7 0 0 1 3 10c0-.88.163-1.724.46-2.5zm.197 6c.176.743.407 1.422.683 2.015.186.399.401.773.642 1.103A7.02 7.02 0 0 1 3.936 13.5zm5.897-10.118A7.02 7.02 0 0 1 16.064 6.5H13.61a10.5 10.5 0 0 0-.683-2.015 6.6 6.6 0 0 0-.642-1.103" />
          <path
            fill="{{AccentColor}}"
            d="M19 14.5a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0m-4.854-2.353-2 2a.5.5 0 0 0 .708.707L14 13.707V16.5a.5.5 0 0 0 1 0v-2.793l1.146 1.147a.5.5 0 0 0 .708-.708l-2-2a.5.5 0 0 0-.351-.146h-.006a.5.5 0 0 0-.348.144z" />
        </svg>
        """;

    private readonly IListItem[] _items;

    public SampleSvgIconPage()
    {
        Icon = new IconInfo("|ThemedSvg|info|" + ThemedFluentUploadTemplate);
        Name = "SVG Icon Protocols";
        ShowDetails = true;
        _items = CreateItems();
    }

    public override IListItem[] GetItems() => _items;

    private static IListItem[] CreateItems()
    {
        var imageDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Images");
        var plainFile = Path.Combine(imageDirectory, "PlainSampleIcon.svg");
        var themedFile = Path.Combine(imageDirectory, "ThemedSampleIcon.svg");

        return
        [
            BuildIconItem(
                "|Svg|" + PlainTemplate,
                "Plain inline SVG",
                "Passes inline SVG through without placeholder expansion or a theme cache split"),
            BuildIconItem(
                "|Svg|" + plainFile,
                "Plain file SVG",
                "Reads the original SVG bytes from a file on an icon-loader worker"),
            BuildIconItem(
                "|ThemedSvg|" + ThemedTemplate,
                "Themed inline SVG",
                "Uses the default info accent and expands both theme placeholders"),
            BuildIconItem(
                "|ThemedSvg|" + themedFile,
                "Themed file SVG",
                "Reads the template from a file and uses the default info accent"),
            BuildIconItem(
                "|ThemedSvg|" + ThemedFluentUploadTemplate,
                "Fluent upload with default accent",
                "Uses inherited currentColor for the globe and the default info accent for the upload badge"),
            BuildIconItem(
                "|ThemedSvg|success|" + ThemedFluentUploadTemplate,
                "Fluent upload with semantic accent",
                "Uses inherited currentColor for the globe and the semantic success color for the upload badge"),
            BuildIconItem(
                "|ThemedSvg|#7A3E9D|" + ThemedFluentUploadTemplate,
                "Fluent upload with custom accent",
                "Uses inherited currentColor for the globe and custom purple for the upload badge"),
            BuildSemanticItem("danger", "Critical actions and errors"),
            BuildSemanticItem("subtle", "Secondary, lower-emphasis content"),
            BuildSemanticItem("info", "Informational and default accent content"),
            BuildSemanticItem("warning", "Cautions that need attention"),
            BuildSemanticItem("success", "Successful or completed states"),
            BuildSemanticItem("neutral", "Theme-aware neutral gray content"),
            BuildSemanticItem("dark", "A deliberately fixed dark accent in both themes"),
            BuildSemanticItem("normal", "The current theme foreground color"),
            BuildIconItem(
                "|ThemedSvg|#7A3E9D|" + ThemedTemplate,
                "Custom #7A3E9D accent",
                "Uses a custom SVG hex color while ThemeColor still follows the surface theme"),
        ];
    }

    private static ListItem BuildSemanticItem(string semanticAccent, string description) =>
        BuildIconItem(
            $"|ThemedSvg|{semanticAccent}|{ThemedTemplate}",
            $"Semantic {semanticAccent} accent",
            description);

    private static ListItem BuildIconItem(string protocol, string title, string description)
    {
        var icon = new IconInfo(protocol);
        return new ListItem(new CopyTextCommand(protocol) { Name = "Copy SVG protocol" })
        {
            Title = title,
            Subtitle = description,
            Icon = icon,
            Details = new Details
            {
                HeroImage = icon,
                Title = title,
                Body = description,
            },
        };
    }
}
