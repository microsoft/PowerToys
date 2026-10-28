// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

/// <summary>The markup and resources that markup tests read, since the pages can't be built in a unit test.</summary>
internal static class RepositoryFiles
{
    public const string EditorPage = @"src\modules\textexpander\TextExpander.Editor\TextExpanderEditorXAML\Views\TextExpanderPage.xaml";
    public const string EditorResources = @"src\modules\textexpander\TextExpander.Editor\Strings\en-us\Resources.resw";
    public const string SettingsPage = @"src\settings-ui\Settings.UI\SettingsXAML\Views\TextExpanderPage.xaml";
    public const string SettingsResources = @"src\settings-ui\Settings.UI\Strings\en-us\Resources.resw";

    public static XName Uid { get; } = XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml");

    public static XName Name { get; } = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

    public static XDocument Xml(string relativePath) => XDocument.Load(Find(relativePath));

    public static string Text(string relativePath) => File.ReadAllText(Find(relativePath));

    /// <summary>The editor page's snippet list, whose rows are built from its DataTemplate.</summary>
    public static XElement SnippetList()
        => Xml(EditorPage).Descendants().Single(element => element.Name.LocalName == "ItemsRepeater"
            && ((string)element.Attribute("ItemsSource") ?? string.Empty).Contains("FilteredSnippets", StringComparison.Ordinal));

    public static XElement WithUid(XContainer container, string uid)
        => container.Descendants().Single(element => (string)element.Attribute(Uid) == uid);

    public static XElement WithName(XContainer container, string name)
        => container.Descendants().Single(element => (string)element.Attribute(Name) == name);

    private static string Find(string relativePath)
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            string path = Path.Combine(folder.FullName, relativePath);
            if (File.Exists(path))
            {
                return path;
            }
        }

        Assert.Inconclusive(relativePath + " isn't next to the tests here.");
        return null;
    }
}
