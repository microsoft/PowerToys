// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The snippet editor, PowerToys.TextExpander.Editor.exe, driven through its AutomationIds. Lookups are scoped to the
/// editor's process, so the snippet dialog and the menus and flyouts it opens are all found.
/// </summary>
internal sealed class SnippetEditor
{
    public const string ProcessName = "PowerToys.TextExpander.Editor";
    public const string Title = "PowerToys Text Expander - Manage snippets";

    private const string TriggerId = "TextExpander_Trigger";
    private const string ReplacementId = "TextExpander_Replacement";
    private readonly Action<string> step;

    private SnippetEditor(long window, int processId, Action<string> step)
    {
        Handle = new IntPtr(window);
        ProcessId = processId;
        this.step = step;
        Session = Session.FromProcess(processId.ToString(CultureInfo.InvariantCulture), PowerToysModule.Runner, timeoutMS: 15_000);
    }

    public Session Session { get; }

    public IntPtr Handle { get; }

    public int ProcessId { get; }

    public bool IsDialogOpen => Session.Has<TextBox>(By.AccessibilityId(TriggerId), timeoutMS: 0);

    public string TriggerText => Session.Find<TextBox>(By.AccessibilityId(TriggerId), 5_000).Value;

    /// <summary>Gets the replacement box's text with <c>\n</c> line breaks, as the editor saves it.</summary>
    public string ReplacementText
    {
        get
        {
            // RichEdit reports paragraphs with \r and always ends the document with one.
            var text = Session.Find<Element>(By.AccessibilityId(ReplacementId), 5_000).GetValue().ReplaceLineEndings("\n");
            return text.EndsWith('\n') ? text[..^1] : text;
        }
    }

    public Button SaveButton => Session.WaitForId<Button>("PrimaryButton", "Save", 10_000);

    /// <summary>Waits for the editor's window, maximizes it as UITestBase does for Settings, and waits for its list.</summary>
    public static SnippetEditor WaitForWindow(Action<string> step, int timeoutMS = 60_000)
    {
        step("Waiting for the snippet editor window");
        var window = WindowsFinder.WaitForWindowByApp(
            ProcessName,
            candidate => candidate.Title.Contains(Title, StringComparison.Ordinal),
            timeoutMS);
        Assert.IsNotNull(window, $"The snippet editor window ('{Title}') did not open within {timeoutMS} ms.");

        WindowHelper.MaximizeWindow(new IntPtr(window.WindowHandle));
        var editor = new SnippetEditor(window.WindowHandle, window.ProcessId, step);
        Assert.IsTrue(
            editor.Session.WaitForElement(By.AccessibilityId("Button_TextExpander_AddSnippet"), 30_000),
            "The snippet editor did not show its New snippet button.");
        return editor;
    }

    public static int[] ProcessIds() => TextExpanderTestBase.ProcessIds(ProcessName);

    public static string CountText(int count) => count == 1 ? "1 snippet" : $"{count} snippets";

    /// <summary>Waits for the list's count, "5 snippets", which shows once the database is loaded.</summary>
    public void WaitForCount(int count, int timeoutMS = 20_000)
    {
        var text = CountText(count);
        Assert.IsTrue(Session.WaitForExactName(text, timeoutMS), $"The snippet editor did not show '{text}'. Rows: {DescribeRows()}.");
    }

    /// <summary>Waits for the count a search shows, "Showing 1 of 5 snippets".</summary>
    public void WaitForFilteredCount(int shown, int total, int timeoutMS = 10_000)
    {
        var text = total == 1 ? $"Showing {shown} of 1 snippet" : $"Showing {shown} of {total} snippets";
        Assert.IsTrue(Session.WaitForExactName(text, timeoutMS), $"The snippet editor did not show '{text}'. Rows: {DescribeRows()}.");
    }

    /// <summary>The list rows, each named "trigger: what it inserts" for screen readers.</summary>
    public IReadOnlyList<Element> Rows() =>
        Session.FindAll<Element>(By.Name(": "), 0)
            .Where(element => element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase))
            .ToList();

    public Element? Row(string trigger) =>
        Session.FindAll<Element>(By.Name(trigger + ": "), 0).FirstOrDefault(element =>
            element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) &&
            element.Name.StartsWith(trigger + ": ", StringComparison.Ordinal));

    public bool WaitForRow(string trigger, bool present, int timeoutMS = 10_000) =>
        Session.WaitFor(() => (Row(trigger) is not null) == present, timeoutMS, pollIntervalMS: 250);

    public string DescribeRows()
    {
        try
        {
            return string.Join(" | ", Rows().Select(row => row.Name));
        }
        catch (Exception ex)
        {
            return $"<rows unavailable: {ex.Message}>";
        }
    }

    /// <summary>Opens the New snippet dialog.</summary>
    public void OpenNewSnippet()
    {
        step("Selecting New snippet");
        Session.Find<Button>(By.AccessibilityId("Button_TextExpander_AddSnippet"), 10_000).Invoke(msPostAction: 0);
        WaitForDialog("New snippet");
    }

    /// <summary>Opens a row for editing by clicking it, as a person does.</summary>
    /// <remarks>
    /// The click goes to the editor's main window, brought in front by its handle: a process-wide session brings the
    /// process's first window forward, which needn't be the main one.
    /// </remarks>
    public void OpenRow(string trigger)
    {
        if (WindowControl.GetForegroundWindowHandle() != Handle)
        {
            WindowControl.TryBringToForeground(Handle);
        }

        Assert.IsTrue(
            WindowControl.WaitForForeground(Handle, timeoutMS: 10_000, requiredConsecutiveMatches: 2),
            $"The snippet editor did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}. Editor windows: {DescribeWindows()}.");

        var row = WaitForRowElement(trigger);
        var (x, y) = (row.X + (row.Width / 2), row.Y + (row.Height / 2));
        Assert.IsTrue(
            WindowControl.IsPointOwnedByWindow(Handle, x, y),
            $"The '{trigger}' row at ({x}, {y}) is covered. Foreground: {WindowControl.GetForegroundWindowInfo()}. Editor windows: {DescribeWindows()}.");

        step($"Clicking the '{trigger}' row at ({x}, {y})");
        MouseHelper.LeftClickAt(x, y);
        WaitForDialog(
            "Edit snippet",
            () => $"Clicked ({x}, {y}) in the row's bounds ({row.X}, {row.Y}, {row.Width}x{row.Height}). " +
                $"Foreground: {WindowControl.GetForegroundWindowInfo()}. Editor windows: {DescribeWindows()}.");
    }

    /// <summary>Runs one of a row's More options commands: Edit, Duplicate or Delete.</summary>
    public void RunRowCommand(string trigger, string menuItemId)
    {
        var row = WaitForRowElement(trigger);

        // Every row has a More options button; the row's own is the one inside its bounds.
        var button = Session.FindAll<Button>(By.AccessibilityId("Button_TextExpander_MoreOptions"), 5_000)
            .FirstOrDefault(candidate =>
            {
                var centerY = candidate.Y + (candidate.Height / 2);
                return centerY >= row.Y && centerY <= row.Y + row.Height;
            });
        Assert.IsNotNull(button, $"The '{trigger}' row has no More options button. Rows: {DescribeRows()}.");

        step($"Opening More options for '{trigger}'");
        button.Invoke(msPostAction: 0);
        step($"Selecting {menuItemId}");
        Session.Find<Element>(By.AccessibilityId(menuItemId), 10_000).Invoke(msPostAction: 0);
    }

    public void SetTrigger(string trigger)
    {
        step($"Setting the trigger to '{trigger}'");
        Session.Find<TextBox>(By.AccessibilityId(TriggerId), 5_000).SetText(trigger);
    }

    /// <summary>Types into the Replacement box, replacing what it holds, with real key presses.</summary>
    public void TypeReplacement(string text)
    {
        step($"Typing '{TextExpanderFiles.Visible(text)}' into Replacement");
        FocusReplacement();
        TextInput.SelectAll();
        Thread.Sleep(200);
        TextInput.Type(text);
    }

    /// <summary>Clicks into the Replacement box and waits until the editor owns keyboard input.</summary>
    public void FocusReplacement()
    {
        var box = Session.Find<Element>(By.AccessibilityId(ReplacementId), 10_000);
        box.Click(msPostAction: 200);
        Assert.IsTrue(
            WindowControl.WaitForForeground(Handle, timeoutMS: 10_000, requiredConsecutiveMatches: 2),
            $"The snippet editor did not get keyboard input. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
    }

    public bool WaitForReplacementText(string expected, int timeoutMS = 10_000) =>
        Session.WaitFor(() => ReplacementText == expected, timeoutMS, pollIntervalMS: 250);

    /// <summary>Selects Save and waits for the dialog to close.</summary>
    public void Save()
    {
        step("Selecting Save");
        var save = SaveButton;
        Assert.IsTrue(save.IsEnabled, $"Save is off. Problem: {DescribeDialog()}.");
        save.Invoke(msPostAction: 0);
        Assert.IsTrue(
            Session.WaitFor(() => !IsDialogOpen, 10_000, pollIntervalMS: 250),
            $"The snippet dialog did not close after Save. {DescribeDialog()}");
    }

    /// <summary>Selects the dialog's Cancel button.</summary>
    public void Cancel()
    {
        step("Selecting Cancel");
        Session.WaitForId<Button>("CloseButton", "Cancel", 10_000).Invoke(msPostAction: 0);
    }

    public void Search(string text)
    {
        step($"Searching for '{text}'");
        Session.Find<TextBox>(By.AccessibilityId("TextExpander_SnippetSearch"), 10_000).SetText(text);
    }

    /// <summary>Opens the database menu, Saved on this PC.</summary>
    public void OpenDatabaseMenu()
    {
        step("Opening the snippets database menu");
        Session.Find<Button>(By.AccessibilityId("Button_TextExpander_SnippetFolder"), 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(
            Session.WaitForElement(By.AccessibilityId("Button_TextExpander_ReloadSnippets"), 10_000),
            "The snippets database menu did not open.");
    }

    public bool WaitForText(string text, int timeoutMS = 10_000) => Session.WaitForExactName(text, timeoutMS);

    /// <summary>Describes the open dialog's trigger, replacement and Save state for a failure message.</summary>
    public string DescribeDialog()
    {
        try
        {
            var save = Session.FindById<Button>("PrimaryButton", "Save");
            return $"trigger '{TriggerText}', replacement '{TextExpanderFiles.Visible(ReplacementText)}', Save enabled {save?.IsEnabled.ToString() ?? "<not found>"}";
        }
        catch (Exception ex)
        {
            return $"<dialog unavailable: {ex.Message}>";
        }
    }

    private Element WaitForRowElement(string trigger)
    {
        Element? row = null;
        Session.WaitFor(() => (row = Row(trigger)) is not null, 10_000, pollIntervalMS: 250);
        Assert.IsNotNull(row, $"The snippet editor has no '{trigger}' row. Rows: {DescribeRows()}.");
        return row;
    }

    /// <summary>Lists the editor process's top-level windows, for a failure message.</summary>
    private string DescribeWindows()
    {
        try
        {
            return string.Join(" | ", WindowsFinder.ListByApp(ProcessId.ToString(CultureInfo.InvariantCulture)).Select(window => window.ToString()));
        }
        catch (Exception ex)
        {
            return $"<windows unavailable: {ex.Message}>";
        }
    }

    private void WaitForDialog(string title, Func<string>? describeFailure = null)
    {
        Assert.IsTrue(
            Session.WaitFor(() => IsDialogOpen, 15_000, pollIntervalMS: 250),
            $"The '{title}' dialog did not open. {describeFailure?.Invoke()}");
        Assert.IsTrue(Session.WaitForExactName(title, 5_000), $"The snippet dialog is not titled '{title}'.");
    }
}
