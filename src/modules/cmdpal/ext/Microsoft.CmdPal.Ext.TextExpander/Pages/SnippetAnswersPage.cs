// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>
/// Asks a snippet's questions in Command Palette's search bar, as Bookmarks asks for a bookmark's placeholders: each
/// question is followed by a box, Tab moves between them, and Enter pastes once every required answer is in. The
/// page shows the snippet as it will come out. Esc goes back without pasting, as Cancel does in Text Expander.
/// </summary>
internal sealed partial class SnippetAnswersPage : ParametersPage
{
    /// <summary>The longest preview shown below the boxes.</summary>
    internal const int PreviewLength = 1000;

    private readonly Snippet _snippet;
    private readonly AnswerRun[] _answers;
    private readonly IParameterRun[] _parameters;
    private readonly ListItem _command;

    public SnippetAnswersPage(Snippet snippet, SnippetActions actions)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(actions);

        _snippet = snippet;
        Name = Resources.paste_command_name;
        Title = snippet.Trigger;
        Icon = Icons.TextExpander;

        // One box for each time a question appears, as Text Expander asks it each time.
        var runs = new List<IParameterRun>(snippet.Questions.Count * 2);
        _answers = new AnswerRun[snippet.Questions.Count];
        for (var i = 0; i < _answers.Length; i++)
        {
            var question = snippet.Questions[i];
            var answer = new AnswerRun(question.Required);
            answer.PropChanged += OnAnswerChanged;
            _answers[i] = answer;
            runs.Add(new LabelRun(SnippetText.QuestionLabel(question)));
            runs.Add(answer);
        }

        _parameters = [.. runs];

        // A snippet that takes part of the copy goes on to its pick page, which offers Copy once the matches are picked.
        ICommand submit = snippet.UsesClipboardParts
            ? new SnippetPicksPage(snippet, actions, Answers, ClearAnswers)
            : new InsertSnippetCommand(snippet, actions.Source, actions.Inserter, Answers, ClearAnswers);
        _command = new ListItem(submit)
        {
            Title = Resources.paste_command_name,
            Icon = Icons.Paste,
            MoreCommands = snippet.UsesClipboardParts ? [] : [new CommandContextItem(actions.CopyCommand(snippet, Answers))],
        };
        UpdatePreview();
    }

    public override IParameterRun[] Parameters => _parameters;

    public override IListItem Command => _command;

    /// <summary>Gets the answers in the order the snippet asks its questions.</summary>
    internal IReadOnlyList<string> Answers()
    {
        var answers = new string[_answers.Length];
        for (var i = 0; i < answers.Length; i++)
        {
            answers[i] = _answers[i].Text;
        }

        return answers;
    }

    /// <summary>Empties the boxes once the snippet is in, so the answers aren't kept, and the next paste starts afresh.</summary>
    internal void ClearAnswers()
    {
        foreach (var answer in _answers)
        {
            answer.ClearValue();
        }
    }

    private void OnAnswerChanged(object sender, IPropChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StringParameterRun.Text))
        {
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        var preview = SnippetText.Preview(_snippet.Replacement, Answers());
        _command.Subtitle = preview.Length > PreviewLength ? string.Concat(preview.AsSpan(0, PreviewLength), "\u2026") : preview;
    }
}
