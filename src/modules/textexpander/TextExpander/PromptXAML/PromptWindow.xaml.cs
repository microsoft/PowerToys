// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The window a {{prompt:Label}} snippet opens: the question over a box, then OK and Cancel. It
/// follows Windows' theme, high contrast included, and is sharp at every scale. Shown by
/// <see cref="PromptHost"/> on the prompt thread, never on the keyboard hook's.
/// </summary>
public sealed partial class PromptWindow : Window
{
    private const double WidthDip = 420;

    // What a one-line question lays out at, title bar included, so the window opens at its size.
    // Refit once it's loaded, for a question that wraps or larger text.
    private const double EstimatedHeightDip = 209.6;

    private readonly PromptWindowChrome _chrome;
    private readonly Action<string?> _done;
    private readonly PromptRequest _request;
    private bool _finished;

    /// <param name="label">The snippet's question.</param>
    /// <param name="target">The window being typed into. The prompt opens on the screen with its caret.</param>
    /// <param name="done">Called once, on this thread, with the answer, or with null when cancelled.</param>
    internal PromptWindow(PromptRequest request, IntPtr target, Action<string?> done)
    {
        _request = request;
        _done = done;
        InitializeComponent();
        _chrome = new PromptWindowChrome(this, target, TitleBar, TitleBarIcon, TitleText);

        Answer.Header = PromptText.Label(request.Label);
        AnswerError.Text = PromptText.RequiredError;
        OkButton.Content = PromptText.OkText;
        CancelButton.Content = PromptText.CancelText;
        OkButton.IsEnabled = !request.Required;

        // The close button and Alt+F4 cancel, as Esc does.
        _chrome.OnClose(closing: () => Finish(accepted: false, close: false), closed: () => Complete(null));

        _chrome.Place(WidthDip, EstimatedHeightDip);
    }

    /// <summary>Shows the window and brings it in front of the app the user was typing in.</summary>
    internal void Open() => _chrome.Open();

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        _chrome.FitToContent(Root);
        Answer.Focus(FocusState.Programmatic);
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            // OK is the default. A focused button handles Enter itself, so this is everywhere else.
            case VirtualKey.Enter:
                e.Handled = true;
                TryFinish();
                break;

            case VirtualKey.Escape:
                e.Handled = true;
                Finish(accepted: false, close: true);
                break;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => TryFinish();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(accepted: false, close: true);

    private void Answer_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool valid = PromptText.HasRequiredAnswer(_request, Answer.Text);
        OkButton.IsEnabled = valid;
        if (valid)
        {
            AnswerError.Visibility = Visibility.Collapsed;
        }
    }

    private void TryFinish()
    {
        if (!PromptText.HasRequiredAnswer(_request, Answer.Text))
        {
            AnswerError.Visibility = Visibility.Visible;
            Answer.Focus(FocusState.Programmatic);
            return;
        }

        Finish(accepted: true, close: true);
    }

    private void Finish(bool accepted, bool close)
    {
        if (_finished)
        {
            return;
        }

        string typed = PromptText.Take(() => Answer.Text, () => Answer.Text = string.Empty);
        Complete(PromptText.Result(accepted, typed));
        if (close)
        {
            _chrome.Close();
        }
    }

    private void Complete(string? answer)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _done(answer);
    }
}
