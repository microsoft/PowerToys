// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// What the snippet list's code relies on in its markup, checked in the page's XAML because
    /// the page itself can't be built in a unit test.
    /// </summary>
    [TestClass]
    public class TextExpanderPageMarkup
    {
        private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static readonly string[] RowHandlers = { "EditSnippet_Click", "EditSnippet_Click", "DuplicateSnippet_Click", "DeleteSnippet_Click" };
        private static readonly string[] MenuOrder = { "TextExpander_EditSnippet", "TextExpander_DuplicateSnippet", "TextExpander_DeleteSnippet" };
        private static readonly string[] SnippetFolderItemOrder = { "TextExpander_SnippetFolder_Current", "TextExpander_SnippetFile_Open", "TextExpander_SnippetFolder_Reload" };

        [TestMethod]
        public void EverythingClickableInARowCarriesItsSnippetInItsTag()
        {
            XElement row = LoadRowTemplate();

            // ItemsRepeater sets no DataContext on rows built with x:Bind, so a handler can only find
            // the row's snippet in Tag. Selecting a row did nothing while the row relied on DataContext.
            var clickable = row.DescendantsAndSelf().Where(element => element.Attribute("Click") != null).ToList();
            CollectionAssert.AreEquivalent(RowHandlers, clickable.Select(element => (string)element.Attribute("Click")).ToArray());
            foreach (XElement element in clickable)
            {
                Assert.AreEqual("{x:Bind}", (string)element.Attribute("Tag"), $"{element.Name.LocalName} ({(string)element.Attribute("Click")})");
            }

            // {Binding} reads DataContext as well, which these rows don't have.
            Assert.IsFalse(row.DescendantsAndSelf().Attributes().Any(attribute => attribute.Value.Contains("{Binding", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void RowsShowAPencilAndEditComesFirstInMoreOptions()
        {
            XElement row = LoadRowTemplate();
            Assert.AreEqual("SettingsCard", row.Name.LocalName);
            Assert.AreEqual("EditSnippet_Click", (string)row.Attribute("Click"));
            StringAssert.Contains((string)row.Attribute("ActionIcon"), "\uE70F", "A pencil instead of the chevron, so it's clear that selecting a row edits it.");

            XElement menu = row.Descendants().Single(element => element.Name.LocalName == "MenuFlyout");
            CollectionAssert.AreEqual(MenuOrder, menu.Elements().Select(item => (string)item.Attribute(XName.Get("Uid", Xaml))).ToArray());
        }

        [TestMethod]
        public void AttributionUsesTheCurrentPublicWebsite()
        {
            XElement link = LoadPage().Descendants()
                .Single(element => element.Name.LocalName == "PageLink"
                    && (string)element.Attribute(XName.Get("Uid", Xaml)) == "Attribution_TextExpander");

            Assert.AreEqual("https://howdygary.github.io/text-expander/", (string)link.Attribute("Link"));
        }

        [TestMethod]
        public void EmptyStatusDoesNotReserveSpaceAboveSnippetRows()
        {
            XDocument page = LoadPage();
            XElement status = page.Descendants()
                .Single(element => (string)element.Attribute(XName.Get("Name", Xaml)) == "SnippetStatus");
            Assert.AreEqual("Collapsed", (string)status.Attribute("Visibility"));

            XElement list = page.Descendants()
                .Single(element => element.Name.LocalName == "ItemsRepeater"
                    && ((string)element.Attribute("ItemsSource") ?? string.Empty).Contains("FilteredSnippets", StringComparison.Ordinal));
            Assert.AreEqual("0,2,0,0", (string)list.Attribute("Margin"));

            XElement emptyState = page.Descendants()
                .Single(element => element.Name.LocalName == "StackPanel"
                    && ((string)element.Attribute("Visibility") ?? string.Empty).Contains("HasNoSnippets", StringComparison.Ordinal));
            Assert.AreEqual("0,12,0,0", (string)emptyState.Attribute("Margin"));
        }

        [TestMethod]
        public void PromptWindowUsesOneAttachedFlyoutWithItsLifecycleAndAssertiveError()
        {
            XDocument page = LoadPage();
            XElement flyout = page.Descendants()
                .Single(element => element.Name.LocalName == "Flyout"
                    && (string)element.Attribute(XName.Get("Name", Xaml)) == "PromptFlyout");

            Assert.AreEqual("FlyoutBase.AttachedFlyout", flyout.Parent.Name.LocalName);
            Assert.AreEqual("PromptFlyout_Opened", (string)flyout.Attribute("Opened"));
            Assert.AreEqual("PromptFlyout_Closing", (string)flyout.Attribute("Closing"));
            Assert.AreEqual("PromptFlyout_Closed", (string)flyout.Attribute("Closed"));
            Assert.AreEqual("BottomEdgeAlignedLeft", (string)flyout.Attribute("Placement"));
            Assert.AreEqual("True", (string)flyout.Attribute("ShouldConstrainToRootBounds"));

            XElement content = flyout.Descendants()
                .Single(element => (string)element.Attribute(XName.Get("Name", Xaml)) == "PromptFlyoutContent");
            Assert.AreEqual("PromptFlyoutContent_PreviewKeyDown", (string)content.Attribute("PreviewKeyDown"));

            XElement title = flyout.Descendants()
                .Single(element => (string)element.Attribute(XName.Get("Uid", Xaml)) == "TextExpander_PromptFlyout_Title");
            Assert.AreEqual("Level2", (string)title.Attribute("AutomationProperties.HeadingLevel"));

            XElement error = flyout.Descendants()
                .Single(element => (string)element.Attribute(XName.Get("Name", Xaml)) == "PromptQuestionError");
            Assert.AreEqual("Assertive", (string)error.Attribute("AutomationProperties.LiveSetting"));
            Assert.AreEqual("Collapsed", (string)error.Attribute("Visibility"));

            Assert.IsFalse(page.Descendants().Any(element => (string)element.Attribute(XName.Get("Name", Xaml)) == "PromptVariableBuilder"));
            Assert.IsFalse(page.Descendants().Attributes(XName.Get("Uid", Xaml)).Any(attribute => attribute.Value.Contains("PromptBuilder", StringComparison.Ordinal)));
            Assert.IsFalse(flyout.Descendants().Any(element => (string)element.Attribute("Click") == "InsertPromptVariable_Click"));
        }

        [TestMethod]
        public void PromptWindowNameHelpAndAccessibilityTextAreExact()
        {
            XDocument resources = LoadResources();
            Assert.AreEqual("Prompt window", ResourceValue(resources, "TextExpander_Variable_Prompt"));
            Assert.AreEqual("Ask for text when the snippet runs.", ResourceValue(resources, "TextExpander_Variable_Prompt_Help"));
            Assert.AreEqual(
                "Prompt window",
                ResourceValue(resources, "TextExpander_PromptFlyout.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"));
            Assert.AreEqual(
                "Click a Prompt window token, or place the caret in it and press F2, to edit it.",
                ResourceValue(resources, "TextExpander_Dialog_ReplacementBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText"));

            Assert.IsFalse(resources.Descendants("data")
                .Select(element => (string)element.Attribute("name"))
                .Any(name => name?.Contains("PromptBuilder", StringComparison.Ordinal) == true));
        }

        [TestMethod]
        public void BoundarySettingUsesExactNameHelpAndGlobalBinding()
        {
            XDocument resources = LoadResources();
            Assert.AreEqual(
                "Wait for space or punctuation",
                ResourceValue(resources, "TextExpander_RequireWordBoundary.Header"));
            Assert.AreEqual(
                "Prevents expansion while you’re still typing a longer trigger.",
                ResourceValue(resources, "TextExpander_RequireWordBoundary.Description"));

            XElement card = LoadPage().Descendants()
                .Single(element => (string)element.Attribute(XName.Get("Uid", Xaml)) == "TextExpander_RequireWordBoundary");
            XElement toggle = card.Descendants().Single(element => element.Name.LocalName == "ToggleSwitch");
            Assert.AreEqual(
                "{x:Bind ViewModel.RequireWordBoundary, Mode=TwoWay}",
                (string)toggle.Attribute("IsOn"));
            Assert.AreEqual(
                "Toggle_TextExpander_RequireWordBoundary",
                (string)toggle.Attribute("AutomationProperties.AutomationId"));
        }

        [TestMethod]
        public void SnippetFolderExpanderShowsSyncGuidanceAndOpenFileRow()
        {
            XDocument page = LoadPage();
            XElement expander = page.Descendants()
                .Single(element => (string)element.Attribute(XName.Get("Uid", Xaml)) == "TextExpander_SnippetFile");
            Assert.AreEqual(
                "{x:Bind ViewModel.SnippetFileSummary, Mode=OneWay}",
                (string)expander.Attribute("Description"),
                "The collapsed expander keeps its dynamic local/custom-folder summary.");

            XElement itemsHeader = expander.Elements()
                .Single(element => element.Name.LocalName == "SettingsExpander.ItemsHeader");
            XElement guidance = itemsHeader.Elements().Single(element => element.Name.LocalName == "TextBlock");
            Assert.AreEqual(
                "TextExpander_SnippetFile_SyncGuidance",
                (string)guidance.Attribute(XName.Get("Uid", Xaml)));
            Assert.AreEqual("Wrap", (string)guidance.Attribute("TextWrapping"));
            Assert.AreEqual("{ThemeResource CaptionTextBlockStyle}", (string)guidance.Attribute("Style"));
            Assert.AreEqual("{ThemeResource TextFillColorSecondaryBrush}", (string)guidance.Attribute("Foreground"));
            Assert.AreEqual("16,12,16,8", (string)guidance.Attribute("Margin"));

            XElement items = expander.Elements()
                .Single(element => element.Name.LocalName == "SettingsExpander.Items");
            CollectionAssert.AreEqual(
                SnippetFolderItemOrder,
                items.Elements().Select(element => (string)element.Attribute(XName.Get("Uid", Xaml))).ToArray());

            XElement openCard = items.Elements().ElementAt(1);
            XElement openButton = openCard.Descendants().Single(element => element.Name.LocalName == "Button");
            Assert.AreEqual("TextExpander_SnippetFile_OpenButton", (string)openButton.Attribute(XName.Get("Uid", Xaml)));
            Assert.AreEqual("OpenSnippetsFile_Click", (string)openButton.Attribute("Click"));
            Assert.AreEqual("Button_TextExpander_OpenSnippetFile", (string)openButton.Attribute("AutomationProperties.AutomationId"));
            Assert.AreEqual(
                "{x:Bind ViewModel.CanEditSnippets, Mode=OneWay}",
                (string)expander.Attribute("IsEnabled"));

            XDocument resources = LoadResources();
            Assert.AreEqual(
                "To use the same snippets across devices, choose a folder synced by OneDrive or another service.",
                ResourceValue(resources, "TextExpander_SnippetFile_SyncGuidance.Text"));
            Assert.AreEqual("Open snippets file", ResourceValue(resources, "TextExpander_SnippetFile_Open.Header"));
            Assert.AreEqual(
                "Open the active snippet file in your default text editor.",
                ResourceValue(resources, "TextExpander_SnippetFile_Open.Description"));
            Assert.AreEqual("Open file", ResourceValue(resources, "TextExpander_SnippetFile_OpenButton.Content"));
            Assert.AreEqual(
                "Can't open the snippets file",
                ResourceValue(resources, "TextExpander_OpenSnippetsErrorDialog_Title"));
            StringAssert.Contains(
                ResourceValue(resources, "TextExpander_SnippetFile_OpenMissing"),
                "can't be found or accessed");
            StringAssert.Contains(
                ResourceValue(resources, "TextExpander_SnippetFile_OpenFailed"),
                "default app");
            Assert.IsFalse(
                resources.Descendants("data")
                    .Any(element => (string)element.Attribute("name") == "TextExpander_SnippetFile.Description"),
                "Do not leave the old static description dead behind the dynamic summary binding.");

            string code = LoadPageCode();
            StringAssert.Contains(code, "ViewModel.OpenSnippetsFile()");
            StringAssert.Contains(code, "TextExpander_OpenSnippetsErrorDialog_Title");
        }

        [TestMethod]
        public void StaleFolderChangeMessageRequiresTheFolderToBeChosenAgain()
        {
            XDocument resources = LoadResources();
            Assert.AreEqual(
                "The active snippet file or selected folder changed, disappeared, or is no longer available. Choose the folder again before moving your snippets.",
                ResourceValue(resources, "TextExpander_SnippetFolder_ChangeStale"));
        }

        [TestMethod]
        public void FolderRollbackFailureReportsTheRecoveryLocation()
        {
            XDocument resources = LoadResources();
            Assert.AreEqual(
                "Recovery file paths are available at {0}.",
                ResourceValue(resources, "TextExpander_SnippetFolder_RecoveryAvailable"));

            string code = LoadPageCode();
            StringAssert.Contains(code, "TextExpander_SnippetFolder_RecoveryAvailable");
            StringAssert.Contains(code, "if (!string.IsNullOrEmpty(backup))");
        }

        [TestMethod]
        public void UncertainFolderSettingMessagesGiveRecoveryInstructions()
        {
            XDocument resources = LoadResources();
            StringAssert.Contains(
                ResourceValue(resources, "TextExpander_SnippetFolder_SettingsCommittedAfterError"),
                "The new folder remains active.");
            StringAssert.Contains(
                ResourceValue(resources, "TextExpander_SnippetFolder_SettingsStateUncertain"),
                "No snippet files were removed.");

            string viewModel = LoadViewModelCode();
            StringAssert.Contains(viewModel, "TextExpander_SnippetFolder_SettingsCommittedAfterError");
            StringAssert.Contains(viewModel, "TextExpander_SnippetFolder_SettingsStateUncertain");
            StringAssert.Contains(viewModel, "error.RecoveryPaths");
        }

        [TestMethod]
        public void SnippetRevisionChangesInvalidateTheEditorAndCloseItsPromptWindow()
        {
            string code = LoadPageCode();
            const string revisionBranch = "e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision)";
            int start = code.IndexOf(revisionBranch, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "The page must listen for snippet-source revision changes.");
            int end = code.IndexOf("};", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start);
            string branch = code[start..end];

            StringAssert.Contains(branch, "_editingRevision != ViewModel.SnippetRevision");
            StringAssert.Contains(branch, "CancelPromptEdit();");
            StringAssert.Contains(branch, "Validate();");
            StringAssert.Contains(code, "if (_isSnippetDialogOpen && _editingRevision != ViewModel.SnippetRevision)");
        }

        private static XElement LoadRowTemplate()
        {
            XElement list = LoadPage().Descendants()
                .Single(element => element.Name.LocalName == "ItemsRepeater" && ((string)element.Attribute("ItemsSource") ?? string.Empty).Contains("FilteredSnippets", StringComparison.Ordinal));
            return list.Descendants().Single(element => element.Name.LocalName == "DataTemplate").Elements().Single();
        }

        private static XDocument LoadPage()
        {
            string page = FindRepositoryFile(Path.Combine("src", "settings-ui", "Settings.UI", "SettingsXAML", "Views", "TextExpanderPage.xaml"));
            if (page == null)
            {
                Assert.Inconclusive("The page's XAML isn't next to the tests here.");
            }

            return XDocument.Load(page);
        }

        private static XDocument LoadResources()
        {
            string resources = FindRepositoryFile(Path.Combine("src", "settings-ui", "Settings.UI", "Strings", "en-us", "Resources.resw"));
            if (resources == null)
            {
                Assert.Inconclusive("The English resources aren't next to the tests here.");
            }

            return XDocument.Load(resources);
        }

        private static string LoadPageCode()
        {
            string page = FindRepositoryFile(Path.Combine("src", "settings-ui", "Settings.UI", "SettingsXAML", "Views", "TextExpanderPage.xaml.cs"));
            if (page == null)
            {
                Assert.Inconclusive("The page's code-behind isn't next to the tests here.");
            }

            return File.ReadAllText(page);
        }

        private static string LoadViewModelCode()
        {
            string viewModel = FindRepositoryFile(Path.Combine("src", "settings-ui", "Settings.UI", "ViewModels", "TextExpanderViewModel.cs"));
            if (viewModel == null)
            {
                Assert.Inconclusive("The Text Expander view model isn't next to the tests here.");
            }

            return File.ReadAllText(viewModel);
        }

        private static string ResourceValue(XDocument resources, string name)
            => resources.Descendants("data")
                .Single(element => (string)element.Attribute("name") == name)
                .Element("value")
                .Value;

        private static string FindRepositoryFile(string relativePath)
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                string path = Path.Combine(folder.FullName, relativePath);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
    }
}
