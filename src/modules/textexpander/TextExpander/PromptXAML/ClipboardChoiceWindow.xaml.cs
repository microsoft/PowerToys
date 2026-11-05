// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The window a clipboard variable opens to pick one of several matches, as {{clipboard:link:pick}} does: the question
/// over the list, then Insert and Cancel. A click, or Enter, on a match inserts it; Esc, Cancel or the close button
/// cancels the whole snippet, as a question does. It follows Windows' theme, high contrast included. Shown by
/// <see cref="PromptHost"/> on the prompt thread, never on the keyboard hook's.
/// </summary>
public sealed partial class ClipboardChoiceWindow : Window
{
    private const double WidthDip = 420;

    // A heading and a few rows, title bar included; refit once it's loaded.
    private const double EstimatedHeightDip = 300;

    private readonly PromptWindowChrome _chrome;
    private readonly Action<string?> _done;
    private bool _finished;

    /// <param name="choice">What to pick from.</param>
    /// <param name="target">The window being typed into. The window opens on its screen.</param>
    /// <param name="done">Called once, on this thread, with the pick, or with null when cancelled.</param>
    internal ClipboardChoiceWindow(ClipboardChoice choice, IntPtr target, Action<string?> done)
    {
        _done = done;
        InitializeComponent();
        _chrome = new PromptWindowChrome(this, target, TitleBar, TitleBarIcon, TitleText);

        Heading.Text = ClipboardChoiceText.Heading(choice);
        AutomationProperties.SetName(Options, Heading.Text);
        InsertButton.Content = ClipboardChoiceText.InsertText;
        CancelButton.Content = PromptText.CancelText;
        Options.ItemsSource = choice.Options.Select((option, index) => new ClipboardChoiceOption(option, index + 1)).ToList();

        // A row going back to be reused shows no match, and may have none.
        Options.ContainerContentChanging += (_, args) =>
        {
            if (!args.InRecycleQueue && args.Item is ClipboardChoiceOption option)
            {
                AutomationProperties.SetName(args.ItemContainer, option.Spoken);
            }
        };
        Options.SelectedIndex = choice.Options.Count > 0 ? 0 : -1;

        // The close button and Alt+F4 cancel, as Esc does.
        AppWindow.Closing += (_, _) => Finish(null, close: false);
        Closed += (_, _) => Complete(null);

        _chrome.Place(WidthDip, EstimatedHeightDip);
    }

    private string? Selected => (Options.SelectedItem as ClipboardChoiceOption)?.Text;

    /// <summary>Shows the window and brings it in front of the app the user was typing in.</summary>
    internal void Open() => _chrome.Open();

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        _chrome.FitToContent(Root);

        // On the first match, so the arrow keys move from it and Enter inserts it.
        if (Options.ContainerFromIndex(Math.Max(Options.SelectedIndex, 0)) is ListViewItem first)
        {
            first.Focus(FocusState.Programmatic);
        }
        else
        {
            Options.Focus(FocusState.Programmatic);
        }
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            // Insert is the default. A focused button or match handles Enter itself, so this is everywhere else.
            case VirtualKey.Enter:
                e.Handled = true;
                Finish(Selected, close: true);
                break;

            case VirtualKey.Escape:
                e.Handled = true;
                Finish(null, close: true);
                break;
        }
    }

    private void Options_ItemClick(object sender, ItemClickEventArgs e) => Finish((e.ClickedItem as ClipboardChoiceOption)?.Text, close: true);

    private void Insert_Click(object sender, RoutedEventArgs e) => Finish(Selected, close: true);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(null, close: true);

    private void Finish(string? chosen, bool close)
    {
        if (_finished)
        {
            return;
        }

        Complete(chosen);
        if (close)
        {
            Close();
        }
    }

    private void Complete(string? chosen)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _done(chosen);
    }
}
