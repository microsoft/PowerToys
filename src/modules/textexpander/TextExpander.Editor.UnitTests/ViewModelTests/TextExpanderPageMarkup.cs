// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Storage;
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
        private const string EditorAppMarkup = @"src\modules\textexpander\TextExpander.Editor\TextExpanderEditorXAML\App.xaml";
        private const string ReplacementEditor = @"src\modules\textexpander\TextExpander.Editor\Helpers\TextExpanderReplacementEditor.cs";
        private static readonly string[] MenuHandlers = { "EditSnippet_Click", "DuplicateSnippet_Click", "DeleteSnippet_Click" };
        private static readonly string[] MenuOrder = { "TextExpander_EditSnippet", "TextExpander_DuplicateSnippet", null, "TextExpander_DeleteSnippet" };
        private static readonly string[] ErrorBarHandlers = { "ReloadSnippets_Click", "UseSnippetsFile_Click" };
        private static readonly string[] PromptBackdrops = { "{ThemeResource SmokeFillColorDefault}", "{ThemeResource SolidBackgroundFillColorBaseAlt}", "{ThemeResource SystemColorWindowColor}" };
        private static readonly string[] SnippetFolderButtons = { "Button_TextExpander_OpenSnippetFile", "Button_TextExpander_ReloadSnippets", "Button_TextExpander_MoveSnippets", "Button_TextExpander_UseSnippetsFile" };

        // Loaded by name or set through x:Uid, so nothing but this test notices one going missing.
        private static readonly string[] EditorKeys =
        {
            "Attribution_TextExpander.Content",
            "TextExpander_SnippetFile_SyncHeading",
            "TextExpander_SnippetFile_SyncStepMove",
            "TextExpander_SnippetFile_SyncStepUse",
            "TextExpander_SnippetFile_SyncHeadingOneDrive",
            "TextExpander_SnippetFile_SyncStepOneDrive",
            "TextExpander_SnippetFile_OpenButton.Content",
            "TextExpander_SnippetFolder_ReloadButton.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip",
            "TextExpander_SnippetFile_FileType",
            "TextExpander_SnippetFile_ChooseDifferent",
            "TextExpander_SnippetFile_NotSnippets",
            "TextExpander_SnippetFile_Conflict_Title",
            "TextExpander_SnippetFile_Conflict_Message",
            "TextExpander_SnippetFile_Conflict_Use",
            "TextExpander_SnippetFile_Conflict_Replace",
            "TextExpander_EditorTitle",
            "TextExpander_SnippetDialog_NewTitle",
            "TextExpander_SnippetDialog_EditTitle",
            "TextExpander_SnippetTotal",
            "TextExpander_SnippetTotal_One",
            "TextExpander_Row_Inserts",
            "TextExpander_Row_AsksThenInserts",
            "TextExpander_Row_NeverExpandsSpoken",
            "TextExpander_OpenSnippetsErrorDialog_Title",
            "TextExpander_SnippetFile_OpenMissing",
            "TextExpander_SnippetFile_OpenFailed",
            "TextExpander_SnippetError_NotJson",
            "TextExpander_SnippetError_NoList",
            "TextExpander_SnippetFile_NewerVersion",
            "TextExpander_SnippetFile_CantSaveText",
            "TextExpander_StarterSignature_Text",
            "TextExpander_SnippetFolder_SettingsCommittedAfterError",
            "TextExpander_SnippetFolder_SettingsStateUncertain",
            "TextExpander_Variable_Prompt",
            "TextExpander_Variable_Prompt_Help",
            "TextExpander_PromptFlyout.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "TextExpander_PromptFlyout_Question.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "TextExpander_PromptFlyout_Preview.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
            "TextExpander_PromptFlyout_QuestionRequired",
            "TextExpander_PromptFlyout_InvalidQuestion",
            "TextExpander_PromptFlyout_Add",
            "TextExpander_PromptFlyout_Update",
            "TextExpander_Dialog_ReplacementBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText",
        };

        private static readonly string[] SettingsKeys = { "TextExpander.SecondaryLinksHeader", "TextExpander_RequireWordBoundary.Header", "TextExpander_RequireWordBoundary.Description" };

        [TestMethod]
        public void EverythingClickableInARowCarriesItsSnippetInItsTag()
        {
            XElement row = RowTemplate();

            // A row's menu isn't in the list's tree, so its items find the row's snippet in Tag, which x:Bind keeps
            // current as the list reuses its rows. The row itself comes with the list's ItemClick.
            var clickable = row.DescendantsAndSelf().Where(element => element.Attribute("Click") != null).ToList();
            CollectionAssert.AreEquivalent(MenuHandlers, clickable.Select(element => (string)element.Attribute("Click")).ToArray());
            foreach (XElement element in clickable)
            {
                Assert.AreEqual("{x:Bind}", (string)element.Attribute("Tag"), $"{element.Name.LocalName} ({(string)element.Attribute("Click")})");
            }

            // {Binding} reads DataContext, which a menu doesn't get from its row.
            Assert.IsFalse(row.DescendantsAndSelf().Attributes().Any(attribute => attribute.Value.Contains("{Binding", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void ARowIsOneStopThatAClickOrEnterEditsAndEditComesFirstInMoreOptions()
        {
            // Keyboard Manager's list: the arrow keys move row by row, and a click or Enter on a row edits it. No pencil,
            // so Edit is offered once on the row and once in its menu (E05).
            XElement list = SnippetList();
            Assert.AreEqual("True", (string)list.Attribute("IsItemClickEnabled"));
            Assert.AreEqual("SnippetList_ItemClick", (string)list.Attribute("ItemClick"));
            Assert.AreEqual("None", (string)list.Attribute("SelectionMode"));
            Assert.IsFalse(RowTemplate().DescendantsAndSelf().Any(element => element.Attribute("ActionIcon") != null));

            XElement menu = RowTemplate().Descendants().Single(element => element.Name.LocalName == "MenuFlyout");
            CollectionAssert.AreEqual(MenuOrder, menu.Elements().Select(item => (string)item.Attribute(Uid)).ToArray(), "Edit, Duplicate, a separator, then Delete.");
            Assert.AreEqual("Button_TextExpander_MoreOptions", (string)WithUid(RowTemplate(), "TextExpander_MoreOptions").Attribute("AutomationProperties.AutomationId"));
        }

        [TestMethod]
        public void TheTitleBarHoldsTheSearchWhichCtrlFReaches()
        {
            XElement page = Xml(EditorPage).Root;
            XElement search = WithName(WithName(page, "AppTitleBar"), "SnippetSearchBox");
            Assert.AreEqual("TextExpander_SnippetSearch", (string)search.Attribute("AutomationProperties.AutomationId"));
            XElement shortcut = search.Descendants().Single(element => element.Name.LocalName == "KeyboardAccelerator");
            Assert.AreEqual("F", (string)shortcut.Attribute("Key"));
            Assert.AreEqual("Control", (string)shortcut.Attribute("Modifiers"));
            Assert.AreEqual("SearchShortcut_Invoked", (string)shortcut.Attribute("Invoked"));

            // One way to clear a search besides the box's own: Clear search where nothing matches (E03, E04).
            Assert.AreEqual(1, page.Descendants().Count(element => (string)element.Attribute(Uid) == "TextExpander_ClearSearch"));
            Assert.AreEqual("{x:Bind ViewModel.HasNoMatchingSnippets, Mode=OneWay}", (string)WithUid(page, "TextExpander_ClearSearch").Parent.Attribute("Visibility"));

            // New snippet's icon and text are its content, so its name comes from the text.
            XElement add = page.Descendants().Single(element => (string)element.Attribute("AutomationProperties.AutomationId") == "Button_TextExpander_AddSnippet");
            Assert.AreEqual("{x:Bind AddSnippetLabel}", (string)add.Attribute("AutomationProperties.LabeledBy"));
            Assert.AreEqual("TextExpander_AddSnippetButton", (string)WithName(add, "AddSnippetLabel").Attribute(Uid));

            // The count isn't shown over the empty state (E35).
            Assert.AreEqual("{x:Bind ViewModel.ShowsSnippetCount, Mode=OneWay}", (string)WithName(page, "SnippetsHeading").Parent.Attribute("Visibility"));

            // The caption buttons follow the theme (E01), and the dialog is named for what it does (E09).
            string app = Text(EditorApp);
            StringAssert.Contains(app, "_window.ExtendsContentIntoTitleBar = true;");
            StringAssert.Contains(app, "_window.SetTitleBar(page.AppTitleBar);");
            StringAssert.Contains(app, "TitleBarTheme.UseDefaultAppMode");
            StringAssert.Contains(Text(EditorPage + ".cs"), "id.HasValue ? \"TextExpander_SnippetDialog_EditTitle\" : \"TextExpander_SnippetDialog_NewTitle\"");
        }

        [TestMethod]
        public void AttributionCreditsTheAuthorAndLinksToTheCurrentPublicWebsite()
        {
            XElement link = WithUid(Xml(EditorPage), "Attribution_TextExpander");
            Assert.AreEqual("HyperlinkButton", link.Name.LocalName);
            Assert.AreEqual("https://howdygary.github.io/text-expander/", (string)link.Attribute("NavigateUri"));

            // With Good to know, behind its button, which also has the input method note.
            XElement flyout = link.Ancestors().First(element => element.Name.LocalName == "Flyout");
            Assert.AreEqual("TextExpander_GoodToKnowButton", (string)flyout.Parent.Parent.Attribute(Uid));
            Assert.AreEqual("GoodToKnowFlyout", (string)flyout.Attribute(Name), "A flyout's name is its AutomationId.");
            WithUid(flyout, "TextExpander_PrivacyNotice_InputMethod");

            XElement credit = Xml(EditorResources).Root.Elements("data").Single(data => (string)data.Attribute("name") == "Attribution_TextExpander.Content");
            Assert.AreEqual("Created by Gary Hsu", (string)credit.Element("value"));
        }

        [TestMethod]
        public void SnippetsWarnAgainstSavingSensitiveInfoWhereTheyAreWrittenAndInGoodToKnow()
        {
            // W1: a caption right under the Replacement box, which Narrator reads with the box's own help.
            XDocument page = Xml(EditorPage);
            XElement caption = WithName(page, "SensitiveInfoWarning");
            Assert.AreEqual("TextExpander_Dialog_SensitiveInfo", (string)caption.Attribute(Uid));
            Assert.AreEqual("ReplacementBox", (string)caption.Parent.ElementsBeforeSelf().Last().Attribute(Name));
            StringAssert.Contains(Text(EditorPage + ".cs"), "AutomationProperties.SetHelpText(ReplacementBox, $\"{AutomationProperties.GetHelpText(ReplacementBox)} {SensitiveInfoWarning.Text}\".Trim());");

            // W5b: the first bullet of Good to know, in its long form.
            XElement bullet = WithUid(page, "TextExpander_PrivacyNotice_SensitiveInfo");
            Assert.IsNull(bullet.Attribute("Grid.Row"));
            Dictionary<string, string> text = Xml(EditorResources).Root.Elements("data").ToDictionary(data => (string)data.Attribute("name"), data => (string)data.Element("value"));
            StringAssert.Contains(text["TextExpander_PrivacyNotice_SensitiveInfo.Text"], "Social Security numbers");
            StringAssert.Contains(text["TextExpander_Dialog_SensitiveInfo.Text"], "aren't encrypted");
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
            Assert.AreEqual("BottomEdgeAlignedLeft", (string)flyout.Attribute("Placement"), "The page places it at a point against the token; this is only the fallback.");
            Assert.AreEqual("True", (string)flyout.Attribute("ShouldConstrainToRootBounds"));
            Assert.AreEqual("PromptFlyoutContent_PreviewKeyDown", (string)WithName(flyout, "PromptFlyoutContent").Attribute("PreviewKeyDown"));
            Assert.AreEqual("Level2", (string)WithUid(flyout, "TextExpander_PromptFlyout_Title").Attribute("AutomationProperties.HeadingLevel"));

            XElement error = WithName(flyout, "PromptQuestionError");
            Assert.AreEqual("Assertive", (string)error.Attribute("AutomationProperties.LiveSetting"));
            Assert.AreEqual("Collapsed", (string)error.Attribute("Visibility"));
        }

        [TestMethod]
        public void PromptWindowAddsOrCancelsAndPreviewsTheWindowOutOfReach()
        {
            XElement flyout = WithName(Xml(EditorPage), "PromptFlyout");
            XElement required = WithName(flyout, "PromptRequired");
            Assert.AreEqual("ToggleSwitch", required.Name.LocalName);
            Assert.AreEqual("True", (string)required.Attribute("IsOn"), "A new question is required by default.");
            Assert.AreEqual("PromptRequired_Toggled", (string)required.Attribute("Toggled"));

            XElement add = WithName(flyout, "PromptAddButton");
            Assert.AreEqual("PromptAdd_Click", (string)add.Attribute("Click"));
            Assert.AreEqual("{StaticResource AccentButtonStyle}", (string)add.Attribute("Style"));
            Assert.AreEqual("PromptCancel_Click", (string)WithUid(flyout, "TextExpander_PromptFlyout_Cancel").Attribute("Click"));

            // Opaque, as a teaching tip is: acrylic let the Save button behind it show through.
            Assert.IsTrue(flyout.Descendants().Any(setter => (string)setter.Attribute("Property") == "Background" && (string)setter.Attribute("Value") == "{ThemeResource TeachingTipBackgroundBrush}"));

            // Only to look at: it has a name for screen readers, and nothing in it can be clicked, typed in or tabbed to.
            XElement preview = WithName(flyout, "PromptPreview");
            Assert.AreEqual("TextExpander_PromptFlyout_Preview", (string)preview.Attribute(Uid));
            Assert.AreEqual("False", (string)preview.Attribute("IsHitTestVisible"));
            Assert.AreEqual("{ThemeResource PromptPreviewBackdropBrush}", (string)preview.Elements().Single().Attribute("Background"));

            // The mockup's mid-grey in dark; in light a pale grey, since smoke over a light flyout looked like a disabled overlay.
            string[] backdrops = Xml(EditorAppMarkup).Descendants()
                .Where(element => (string)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "PromptPreviewBackdropBrush")
                .Select(brush => (string)brush.Attribute("Color"))
                .ToArray();
            CollectionAssert.AreEqual(PromptBackdrops, backdrops, "Default (dark), Light and HighContrast.");
            Assert.IsFalse(preview.Descendants().Any(element => element.Name.LocalName is "TextBox" or "CheckBox" or "ToggleSwitch"));
            foreach (XElement button in preview.Descendants().Where(element => element.Name.LocalName == "Button"))
            {
                Assert.AreEqual("False", (string)button.Attribute("IsTabStop"));
                Assert.AreEqual("Raw", (string)button.Attribute("AutomationProperties.AccessibilityView"));
            }

            // Typing follows the question in the token and the preview, but no draft is an undo step, and an error
            // waits until adding is refused.
            string code = Text(EditorPage + ".cs");
            StringAssert.Contains(Member(code, "private void UpdatePromptDraft("), "_replacementEditor.UpdateTransientEdit(");
            Assert.IsFalse(Member(Text(ReplacementEditor), "public void UpdateTransientEdit(").Contains("_history", StringComparison.Ordinal));
            StringAssert.Contains(Member(code, "private void PromptQuestionBox_TextChanged("), "if (PromptQuestionError.Visibility == Visibility.Visible)");
        }

        [TestMethod]
        public void PromptWindowSaysUpdateForAnExistingQuestionAndAsksTheExampleUntilOneIsTyped()
        {
            // Reopening a question used to offer Add; and an empty question's token said a hardcoded "Question"
            // while the box and the preview showed their translated example.
            string begin = Member(Text(EditorPage + ".cs"), "private void BeginPromptEdit(");
            StringAssert.Contains(begin, "session.IsNew ? \"TextExpander_PromptFlyout_Add\" : \"TextExpander_PromptFlyout_Update\"");

            string code = Text(EditorPage + ".cs");
            StringAssert.Contains(code, "TextExpanderPromptEditSession.BeginNew(_replacementEditor.Text, start, end, PromptQuestionBox.PlaceholderText)");
            StringAssert.Contains(Member(code, "private void UpdatePromptDraft("), "PromptQuestionBox.PlaceholderText");
            Assert.AreEqual("{x:Bind PromptQuestionBox.PlaceholderText}", (string)WithName(Xml(EditorPage), "PromptPreviewPlaceholder").Attribute("Text"));
        }

        [TestMethod]
        public void PromptWindowPartsHaveAutomationIds()
        {
            // F040: UI tests and screen reader scripts find these by AutomationId, which Add and Cancel lacked.
            XElement flyout = WithName(Xml(EditorPage), "PromptFlyout");
            var ids = new Dictionary<string, string>
            {
                ["PromptFlyoutScroller"] = "TextExpander_PromptFlyout",
                ["PromptQuestionBox"] = "TextExpander_PromptQuestion",
                ["PromptRequired"] = "Toggle_TextExpander_PromptRequired",
                ["PromptAddButton"] = "Button_TextExpander_PromptAdd",
            };
            foreach ((string name, string id) in ids)
            {
                Assert.AreEqual(id, (string)WithName(flyout, name).Attribute("AutomationProperties.AutomationId"), name);
            }

            Assert.AreEqual("Button_TextExpander_PromptCancel", (string)WithUid(flyout, "TextExpander_PromptFlyout_Cancel").Attribute("AutomationProperties.AutomationId"));
        }

        [TestMethod]
        public void PromptWindowIsPlacedAgainstTheTokenWithinTheWindow()
        {
            // It covered the token's line and the dialog's buttons by half; the token's bounds were measured from
            // the text inside the box's border and padding.
            string show = Member(Text(EditorPage + ".cs"), "private void ShowPromptFlyout(");
            StringAssert.Contains(show, "TextExpanderPromptPlacement.Place(");
            StringAssert.Contains(show, "PromptFlyoutScroller.MaxHeight =");

            // V3-08, V3-09: it covers Save and Cancel, and Insert variable, completely or not at all.
            StringAssert.Contains(show, "PromptFlyoutScroller.MinHeight =");
            StringAssert.Contains(show, "save, cancel, BoundsOf(InsertVariableButton)");
            StringAssert.Contains(show, "FlyoutPlacementMode.TopEdgeAlignedLeft : FlyoutPlacementMode.BottomEdgeAlignedLeft");
            StringAssert.Contains(Member(Text(ReplacementEditor), "public Rect? BoundsForRange("), "view.Padding.Top - view.VerticalOffset");
        }

        [TestMethod]
        public void BoundarySettingIsBoundToTheGlobalOption()
        {
            XElement toggle = WithUid(Xml(SettingsPage), "TextExpander_RequireWordBoundary").Descendants().Single(element => element.Name.LocalName == "ToggleSwitch");
            Assert.AreEqual("{x:Bind ViewModel.RequireWordBoundary, Mode=TwoWay}", (string)toggle.Attribute("IsOn"));
            Assert.AreEqual("Toggle_TextExpander_RequireWordBoundary", (string)toggle.Attribute("AutomationProperties.AutomationId"));
        }

        [TestMethod]
        public void TheSnippetFolderFlyoutSaysWhereSnippetsAreSavedAndClosesBeforeItsActions()
        {
            XElement button = WithName(Xml(EditorPage), "SnippetFolderButton");
            Assert.AreEqual("{x:Bind ViewModel.CanEditSnippets}", (string)button.Attribute("IsEnabled"));
            Assert.AreEqual("{x:Bind ViewModel.SnippetFolderLabel, Mode=OneWay}", (string)button.Attribute("AutomationProperties.Name"));

            XElement flyout = WithName(button, "SnippetFolderFlyout");
            Assert.IsTrue(flyout.Descendants().Any(element => (string)element.Attribute("Text") == "{x:Bind ViewModel.SnippetFileSummary, Mode=OneWay}"));

            // FL4: within WinUI's 456 px for a flyout with its padding; the advice on other PCs has its own region, between
            // the file's own actions and the two that change the file.
            Assert.AreEqual("422", (string)flyout.Elements().Single().Attribute("Width"));
            XElement advice = WithName(flyout, "SnippetsSyncAdvice");
            Assert.IsTrue(advice.Descendants().Any(element => (string)element.Attribute("Text") == "{x:Bind ViewModel.SyncHeading, Mode=OneWay}"));
            WithName(advice, "SyncStepList");
            Assert.IsTrue(advice.ElementsBeforeSelf().Any(element => element.Descendants().Any(child => (string)child.Attribute("Click") == "ReloadSnippets_Click")));
            Assert.IsTrue(advice.ElementsAfterSelf().Any(element => element.Descendants().Any(child => (string)child.Attribute("Click") == "UseSnippetsFile_Click")));

            // The path is one middle-trimmed line; the tooltip and screen readers get all of it.
            XElement path = WithName(flyout, "SnippetFolderPath");
            Assert.AreEqual("{x:Bind ViewModel.SnippetsFileDisplayPath, Mode=OneWay}", (string)path.Attribute("Text"));
            Assert.AreEqual("{x:Bind ViewModel.SnippetsFilePath, Mode=OneWay}", (string)path.Attribute("ToolTipService.ToolTip"));
            Assert.AreEqual("{x:Bind ViewModel.SnippetsFilePath, Mode=OneWay}", (string)path.Attribute("AutomationProperties.Name"));
            Assert.AreEqual("{x:Bind ViewModel.CanAddSnippets, Mode=OneWay}", (string)flyout.Descendants().Single(element => (string)element.Attribute("Click") == "MoveSnippets_Click").Attribute("IsEnabled"), "Only a loaded file can be moved.");
            CollectionAssert.AreEqual(
                SnippetFolderButtons,
                flyout.Descendants().Where(element => element.Name.LocalName == "Button").Select(element => (string)element.Attribute("AutomationProperties.AutomationId")).ToArray());
            Assert.AreEqual("OpenSnippetsFile_Click", (string)WithUid(flyout, "TextExpander_SnippetFile_OpenButton").Attribute("Click"));

            // The steps are a numbered list for screen readers too ("1 of 2"), each with its button's name bold.
            string code = Text(EditorPage + ".cs");
            string steps = Member(code, "private void ShowSyncAdvice(");
            StringAssert.Contains(steps, "AutomationProperties.SetAutomationControlType(text, AutomationControlType.ListItem);");
            StringAssert.Contains(steps, "AutomationProperties.SetPositionInSet(text, i + 1);");
            StringAssert.Contains(steps, "AutomationProperties.SetSizeOfSet(text, steps.Count);");
            StringAssert.Contains(steps, "FontWeight = Microsoft.UI.Text.FontWeights.SemiBold");

            // Move snippets to… saves through Windows' Save dialog, asking about a file that has snippets with Cancel the
            // default; Use a snippets file… opens one and offers another when it isn't a snippets file.
            string move = Member(code, "private async void MoveSnippets_Click(");
            StringAssert.Contains(move, "new FileSavePicker(");
            StringAssert.Contains(move, "ViewModel.SnippetsPickerFolder");
            StringAssert.Contains(move, "ViewModel.FileHasSnippets(path)");
            StringAssert.Contains(move, "ViewModel.MoveSnippetsTo(path, choice, out string status)");
            StringAssert.Contains(move, "if (status != null)", "Moving onto the database in use changes nothing, so it keeps the status bar.");
            StringAssert.Contains(Member(code, "private async Task<TextExpanderFileConflictChoice?> ConfirmFileHasSnippetsAsync("), "DefaultButton = ContentDialogButton.Close");
            string use = Member(code, "private async void UseSnippetsFile_Click(");
            StringAssert.Contains(use, "new FileOpenPicker(");
            StringAssert.Contains(use, "ViewModel.UseSnippetsFile(path, out string status)");
            StringAssert.Contains(use, "TextExpander_SnippetFile_ChooseDifferent");
            StringAssert.Contains(use, "TextExpander_SnippetFile_NotSnippets");
            StringAssert.Contains(use, "await ShowStorageErrorAsync(error);", "Only a file that isn't a snippets file offers another; other errors show the error dialog.");

            // The picker, a text editor or a reload takes over from the flyout.
            foreach (string handler in new[] { "private async void MoveSnippets_Click(", "private async void UseSnippetsFile_Click(", "private async void OpenSnippetsFile_Click(", "private void ReloadSnippets_Click(" })
            {
                StringAssert.Contains(Member(code, handler), "SnippetFolderFlyout.Hide();", handler);
            }
        }

        [TestMethod]
        public void EveryTextThePagesShowHasAnEnglishResource()
        {
            Dictionary<string, string> editor = AssertResources(EditorPage, EditorResources, EditorKeys);
            AssertResources(SettingsPage, SettingsResources, SettingsKeys);

            // The code formats these with the paths to recover from, so they must keep their slots.
            foreach (string key in new[] { "TextExpander_SnippetFolder_SettingsCommittedAfterError", "TextExpander_SnippetFolder_SettingsStateUncertain" })
            {
                StringAssert.Contains(editor[key], "{0}", key);
                StringAssert.Contains(editor[key], "{1}", key);
            }

            StringAssert.Contains(editor["TextExpander_SnippetError_NotJson"], "{0}", "The line with the mistake.");

            // The example the empty list offers is the starter file's own signature.
            Assert.AreEqual(
                SnippetFile.Read(Encoding.UTF8.GetBytes(TextExpanderStarter.Content)).Snippets.Single(snippet => snippet.Trigger == "!mysig").Replacement,
                editor["TextExpander_StarterSignature_Text"].ReplaceLineEndings("\n"));
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
            XElement errorBar = WithName(page, "SnippetErrorBar");
            Assert.AreEqual("{x:Bind ViewModel.SnippetErrorTitle, Mode=OneWay}", (string)errorBar.Attribute("Title"));
            Assert.AreEqual("{x:Bind ViewModel.SnippetError, Mode=OneWay}", (string)errorBar.Attribute("Message"));

            // Retry, and for a snippets file that's missing or can't be reached, another file without waiting.
            XElement[] actions = errorBar.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();
            CollectionAssert.AreEqual(ErrorBarHandlers, actions.Select(button => (string)button.Attribute("Click")).ToArray());
            Assert.AreEqual("{x:Bind ViewModel.IsSnippetsFileUnavailable, Mode=OneWay}", (string)actions[1].Attribute("Visibility"));
            Assert.IsTrue(errorBar.Descendants().Any(element => (string)element.Attribute("Text") == "{x:Bind ViewModel.SnippetsFileDisplayPath, Mode=OneWay}" && (string)element.Attribute("Visibility") == "{x:Bind ViewModel.IsSnippetsFileMissing, Mode=OneWay}"), "The bar names the file it can't find.");

            // An InfoBar announces itself as it opens only if it already has an automation peer, and never a new
            // message while it's open, so the first problem, a different one and a failed Retry weren't announced.
            string code = Text(EditorPage + ".cs");
            StringAssert.Contains(code, "FrameworkElementAutomationPeer.CreatePeerForElement(SnippetProblemBar);");
            StringAssert.Contains(code, "FrameworkElementAutomationPeer.CreatePeerForElement(SnippetErrorBar);");
            StringAssert.Contains(Member(code, "public string SnippetProblem"), "Announce(SnippetProblemBar, value,");
            StringAssert.Contains(Member(code, "private void ReloadSnippets_Click("), "Announce(SnippetErrorBar, ViewModel.SnippetError,");
        }

        [TestMethod]
        public void EnterOnThePromptWindowsCancelButtonCancels()
        {
            // The flyout took Enter from every control in it, so Enter on the button that closes it without
            // saving saved the question instead.
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
