// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.PowerToys.TextExpander.Editor.UnitTests.RepositoryFiles;

namespace ViewModelTests
{
    /// <summary>
    /// What the pages' code relies on in their markup: wiring, bindings, accessibility and the
    /// resources they load. Wording, spacing and styling are left to the markup itself.
    /// </summary>
    [TestClass]
    public class TextExpanderPageMarkup
    {
        private const string EditorApp = @"src\modules\textexpander\TextExpander.Editor\TextExpanderEditorXAML\App.xaml.cs";
        private static readonly string[] RowHandlers = { "EditSnippet_Click", "EditSnippet_Click", "DuplicateSnippet_Click", "DeleteSnippet_Click" };
        private static readonly string[] MenuOrder = { "TextExpander_EditSnippet", "TextExpander_DuplicateSnippet", "TextExpander_DeleteSnippet" };
        private static readonly string[] SnippetFolderItemOrder = { "TextExpander_SnippetFolder_Current", "TextExpander_SnippetFile_Open", "TextExpander_SnippetFolder_Reload" };

        // Loaded by name or set through x:Uid, so nothing but this test notices one going missing.
        private static readonly string[] EditorKeys =
        {
            "Attribution_TextExpander.Content",
            "TextExpander_SnippetFile_SyncGuidance.Text",
            "TextExpander_SnippetFile_Open.Header",
            "TextExpander_SnippetFile_Open.Description",
            "TextExpander_SnippetFile_OpenButton.Content",
            "TextExpander_OpenSnippetsErrorDialog_Title",
            "TextExpander_SnippetFile_OpenMissing",
            "TextExpander_SnippetFile_OpenFailed",
            "TextExpander_SnippetFolder_ChangeStale",
            "TextExpander_SnippetFolder_RecoveryAvailable",
            "TextExpander_SnippetFolder_SettingsCommittedAfterError",
            "TextExpander_SnippetFolder_SettingsStateUncertain",
            "TextExpander_Variable_Prompt",
            "TextExpander_Variable_Prompt_Help",
            "TextExpander_PromptFlyout.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "TextExpander_Dialog_ReplacementBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText",
        };

        private static readonly string[] SettingsKeys = { "TextExpander.SecondaryLinksHeader", "TextExpander_RequireWordBoundary.Header", "TextExpander_RequireWordBoundary.Description" };

        [TestMethod]
        public void EverythingClickableInARowCarriesItsSnippetInItsTag()
        {
            XElement row = RowTemplate();

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
            XElement row = RowTemplate();
            Assert.AreEqual("SettingsCard", row.Name.LocalName);
            Assert.AreEqual("EditSnippet_Click", (string)row.Attribute("Click"));
            StringAssert.Contains((string)row.Attribute("ActionIcon"), "\uE70F", "A pencil instead of the chevron, so it's clear that selecting a row edits it.");

            XElement menu = row.Descendants().Single(element => element.Name.LocalName == "MenuFlyout");
            CollectionAssert.AreEqual(MenuOrder, menu.Elements().Select(item => (string)item.Attribute(Uid)).ToArray());
        }

        [TestMethod]
        public void AttributionCreditsTheAuthorAndLinksToTheCurrentPublicWebsite()
        {
            XElement link = WithUid(Xml(EditorPage), "Attribution_TextExpander");
            Assert.AreEqual("HyperlinkButton", link.Name.LocalName);
            Assert.AreEqual("https://howdygary.github.io/text-expander/", (string)link.Attribute("NavigateUri"));

            XElement credit = Xml(EditorResources).Root.Elements("data").Single(data => (string)data.Attribute("name") == "Attribution_TextExpander.Content");
            Assert.AreEqual("Created by Gary Hsu", (string)credit.Element("value"));
        }

        [TestMethod]
        public void EmptyStatusDoesNotReserveSpaceAboveSnippetRows()
        {
            Assert.AreEqual("Collapsed", (string)WithName(Xml(EditorPage), "SnippetStatus").Attribute("Visibility"));
        }

        [TestMethod]
        public void PromptWindowUsesOneAttachedFlyoutWithItsLifecycleAndAssertiveError()
        {
            XElement flyout = WithName(Xml(EditorPage), "PromptFlyout");
            Assert.AreEqual("Flyout", flyout.Name.LocalName);
            Assert.AreEqual("FlyoutBase.AttachedFlyout", flyout.Parent.Name.LocalName);
            Assert.AreEqual("PromptFlyout_Opened", (string)flyout.Attribute("Opened"));
            Assert.AreEqual("PromptFlyout_Closing", (string)flyout.Attribute("Closing"));
            Assert.AreEqual("PromptFlyout_Closed", (string)flyout.Attribute("Closed"));
            Assert.AreEqual("BottomEdgeAlignedLeft", (string)flyout.Attribute("Placement"));
            Assert.AreEqual("True", (string)flyout.Attribute("ShouldConstrainToRootBounds"));
            Assert.AreEqual("PromptFlyoutContent_PreviewKeyDown", (string)WithName(flyout, "PromptFlyoutContent").Attribute("PreviewKeyDown"));
            Assert.AreEqual("Level2", (string)WithUid(flyout, "TextExpander_PromptFlyout_Title").Attribute("AutomationProperties.HeadingLevel"));

            XElement error = WithName(flyout, "PromptQuestionError");
            Assert.AreEqual("Assertive", (string)error.Attribute("AutomationProperties.LiveSetting"));
            Assert.AreEqual("Collapsed", (string)error.Attribute("Visibility"));
        }

        [TestMethod]
        public void BoundarySettingIsBoundToTheGlobalOption()
        {
            XElement toggle = WithUid(Xml(SettingsPage), "TextExpander_RequireWordBoundary").Descendants().Single(element => element.Name.LocalName == "ToggleSwitch");
            Assert.AreEqual("{x:Bind ViewModel.RequireWordBoundary, Mode=TwoWay}", (string)toggle.Attribute("IsOn"));
            Assert.AreEqual("Toggle_TextExpander_RequireWordBoundary", (string)toggle.Attribute("AutomationProperties.AutomationId"));
        }

        [TestMethod]
        public void SnippetFolderExpanderShowsSyncGuidanceAndOpenFileRow()
        {
            XElement expander = WithUid(Xml(EditorPage), "TextExpander_SnippetFile");
            Assert.AreEqual(
                "{x:Bind ViewModel.SnippetFileSummary, Mode=OneWay}",
                (string)expander.Attribute("Description"),
                "The collapsed expander keeps its dynamic local/custom-folder summary.");
            Assert.AreEqual("{x:Bind ViewModel.CanEditSnippets, Mode=OneWay}", (string)expander.Attribute("IsEnabled"));

            XElement itemsHeader = expander.Elements().Single(element => element.Name.LocalName == "SettingsExpander.ItemsHeader");
            XElement guidance = itemsHeader.Elements().Single(element => element.Name.LocalName == "TextBlock");
            Assert.AreEqual("TextExpander_SnippetFile_SyncGuidance", (string)guidance.Attribute(Uid));

            XElement items = expander.Elements().Single(element => element.Name.LocalName == "SettingsExpander.Items");
            CollectionAssert.AreEqual(SnippetFolderItemOrder, items.Elements().Select(element => (string)element.Attribute(Uid)).ToArray());

            XElement openButton = items.Elements().ElementAt(1).Descendants().Single(element => element.Name.LocalName == "Button");
            Assert.AreEqual("TextExpander_SnippetFile_OpenButton", (string)openButton.Attribute(Uid));
            Assert.AreEqual("OpenSnippetsFile_Click", (string)openButton.Attribute("Click"));
            Assert.AreEqual("Button_TextExpander_OpenSnippetFile", (string)openButton.Attribute("AutomationProperties.AutomationId"));
        }

        [TestMethod]
        public void EveryTextThePagesShowHasAnEnglishResource()
        {
            Dictionary<string, string> editor = AssertResources(EditorPage, EditorResources, EditorKeys);
            AssertResources(SettingsPage, SettingsResources, SettingsKeys);

            // The code formats these with the paths to recover from, so they must keep their slots.
            StringAssert.Contains(editor["TextExpander_SnippetFolder_RecoveryAvailable"], "{0}");
            foreach (string key in new[] { "TextExpander_SnippetFolder_SettingsCommittedAfterError", "TextExpander_SnippetFolder_SettingsStateUncertain" })
            {
                StringAssert.Contains(editor[key], "{0}", key);
                StringAssert.Contains(editor[key], "{1}", key);
            }
        }

        [TestMethod]
        public void SnippetRevisionChangesInvalidateTheEditorAndCloseItsPromptWindow()
        {
            string code = Text(EditorPage + ".cs");
            const string revisionBranch = "else if (e.PropertyName == nameof(TextExpanderViewModel.SnippetRevision)";
            int start = code.IndexOf(revisionBranch, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "The page must listen for snippet-source revision changes.");
            int end = code.IndexOf("};", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start);
            string branch = code[start..end];

            StringAssert.Contains(branch, "_editingRevision != ViewModel.SnippetRevision");
            StringAssert.Contains(branch, "CancelPromptEdit();");
            StringAssert.Contains(branch, "Validate();");

            // Only an existing snippet's ID can name another entry after a reload. A new snippet (New snippet,
            // Duplicate or a starter) is checked against the reloaded file, so a sync, or a save's own conflict
            // reload, must not turn its Save off for good.
            StringAssert.Contains(code, "if (_isSnippetDialogOpen && _editingId.HasValue && _editingRevision != ViewModel.SnippetRevision)");
        }

        [TestMethod]
        public void AReloadClosesAnOpenRowMenu()
        {
            // An open More options menu would act on a row from before the reload, or on another row once the
            // list reuses its element.
            XElement menu = RowTemplate().Descendants().Single(element => element.Name.LocalName == "MenuFlyout");
            Assert.AreEqual("RowMenu_Opening", (string)menu.Attribute("Opening"));

            string code = Text(EditorPage + ".cs");
            StringAssert.Contains(code, "_rowMenu = (MenuFlyout)sender;");
            StringAssert.Contains(code, "_rowMenu?.Hide();");
        }

        [TestMethod]
        public void ProblemsAndFileErrorsAreAnnouncedAndDescribeTheTrigger()
        {
            XDocument page = Xml(EditorPage);
            Assert.AreEqual("{x:Bind SnippetProblem, Mode=OneWay}", (string)WithName(page, "TriggerBox").Attribute("AutomationProperties.FullDescription"));
            Assert.AreEqual("{x:Bind SnippetProblem, Mode=OneWay}", (string)WithName(page, "SnippetProblemBar").Attribute("Message"));
            Assert.AreEqual("{x:Bind ViewModel.SnippetError, Mode=OneWay}", (string)WithName(page, "SnippetErrorBar").Attribute("Message"));

            // An InfoBar announces itself as it opens only if it already has an automation peer, and never a new
            // message while it's open, so the first problem, a different one and a failed Retry weren't announced.
            string code = Text(EditorPage + ".cs");
            StringAssert.Contains(code, "FrameworkElementAutomationPeer.CreatePeerForElement(SnippetProblemBar);");
            StringAssert.Contains(code, "FrameworkElementAutomationPeer.CreatePeerForElement(SnippetErrorBar);");
            StringAssert.Contains(Member(code, "public string SnippetProblem"), "Announce(SnippetProblemBar, value,");
            StringAssert.Contains(Member(code, "private void ReloadSnippets_Click("), "Announce(SnippetErrorBar, ViewModel.SnippetError,");
        }

        [TestMethod]
        public void EnterOnThePromptWindowsCloseButtonClosesItWithoutSaving()
        {
            // The flyout took Enter from every control in it, so Enter on "Close Prompt window without saving
            // changes" saved the question instead.
            string handler = Member(Text(EditorPage + ".cs"), "private void PromptFlyoutContent_PreviewKeyDown(");
            StringAssert.Contains(handler, "VirtualKey.Enter && e.OriginalSource is not Button");
        }

        [TestMethod]
        public void ClosingTheWindowWithAnEditedSnippetAsksToDiscardItFirst()
        {
            // The snippet dialog covers only the window's content, so the title bar's close button and Alt+F4
            // closed the window and lost the draft without asking.
            string app = Text(EditorApp);
            StringAssert.Contains(app, "AppWindow.Closing +=");
            StringAssert.Contains(app, "page.AskToDiscardBeforeClosing()");

            string code = Text(EditorPage + ".cs");
            StringAssert.Contains(code, "args.Cancel = AskToDiscardChanges();");
            StringAssert.Contains(Member(code, "internal bool AskToDiscardBeforeClosing("), "_isSnippetDialogOpen && AskToDiscardChanges()");
            StringAssert.Contains(Member(code, "private void KeepEditing_Click("), "_closeWindowOnDiscard = false;");
            StringAssert.Contains(Member(code, "private void DiscardChanges_Click("), "App.EditorWindow?.Close();");
        }

        private static XElement RowTemplate()
            => SnippetList().Descendants().Single(element => element.Name.LocalName == "DataTemplate").Elements().Single();

        /// <summary>Gets a member of the page's code, from <paramref name="signature"/> to its closing brace.</summary>
        private static string Member(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, signature);
            int end = code.IndexOf("\n        }", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, signature);
            return code[start..end];
        }

        /// <summary>Checks that every x:Uid on the page and each of the keys has English text, and returns that text by name.</summary>
        private static Dictionary<string, string> AssertResources(string page, string resources, string[] keys)
        {
            Dictionary<string, string> values = Xml(resources).Descendants("data")
                .ToDictionary(element => (string)element.Attribute("name"), element => element.Element("value").Value);
            foreach (string uid in Xml(page).Descendants().Attributes(Uid).Select(attribute => attribute.Value))
            {
                Assert.IsTrue(values.Keys.Any(name => name.StartsWith(uid + ".", StringComparison.Ordinal)), $"x:Uid {uid} has no resources.");
            }

            foreach (string key in keys)
            {
                Assert.IsTrue(values.ContainsKey(key), key);
            }

            return values;
        }
    }
}
