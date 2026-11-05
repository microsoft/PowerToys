// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetAnswersPageTests
{
    private const string Greeting = "Hi {{prompt:Name}},\nthanks for {{prompt?:Topic}}.\n{{prompt:Name}}";

    [TestMethod]
    public void Parameters_EachQuestion_IsItsWordsThenABox()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", Greeting));
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);

        var runs = page.Parameters;
        string[] labels = ["Name", "Topic", "Name"];
        bool[] required = [true, false, true];

        Assert.AreEqual(6, runs.Length);
        CollectionAssert.AreEqual(labels, runs.OfType<LabelRun>().Select(label => label.Text).ToArray());
        CollectionAssert.AreEqual(required, runs.OfType<AnswerRun>().Select(answer => answer.Required).ToArray());
        Assert.AreEqual("!greet", page.Title);
    }

    [TestMethod]
    public void NeedsValue_RequiredAnswer_CannotBeBlankButAnOptionalOneCan()
    {
        var required = new AnswerRun(required: true);
        var optional = new AnswerRun(required: false);

        Assert.IsTrue(required.NeedsValue);
        required.Text = "   ";
        Assert.IsTrue(required.NeedsValue);
        required.Text = "Your name";
        Assert.IsFalse(required.NeedsValue);
        Assert.IsFalse(optional.NeedsValue);
    }

    [TestMethod]
    public void Answers_Typed_ShowInThePreviewInOrder()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", Greeting));
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answers = page.Parameters.OfType<AnswerRun>().ToArray();

        Assert.AreEqual("Hi [Name],\nthanks for [Topic].\n[Name]", page.Command.Subtitle);

        answers[0].Text = "Your name";
        answers[2].Text = "Your colleague";

        Assert.AreEqual("Hi Your name,\nthanks for [Topic].\nYour colleague", page.Command.Subtitle);
        CollectionAssert.AreEqual(new[] { "Your name", string.Empty, "Your colleague" }, page.Answers().ToArray());
    }

    [TestMethod]
    public void Paste_Answered_InsertsTheSnippetAndClearsTheAnswers()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", Greeting));
        environment.Clipboard.Text = "Something the user copied";
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answers = page.Parameters.OfType<AnswerRun>().ToArray();
        answers[0].Text = "Your name";
        answers[1].Text = "the notes";
        answers[2].Text = "Your colleague";

        var result = ((IInvokableCommand)page.Command.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual("Hi Your name,\nthanks for the notes.\nYour colleague", environment.Clipboard.Writes.Single().Text);
        Assert.AreEqual("Something the user copied", environment.Clipboard.Text);
        Assert.IsTrue(answers.All(answer => answer.Text.Length == 0), "Answers aren't kept after the paste.");
    }

    [TestMethod]
    public void MoreCommands_Answered_OffersCopy()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";

        var copy = (CopySnippetCommand)((CommandContextItem)page.Command.MoreCommands.Single()).Command!;
        _ = copy.Invoke();

        Assert.AreEqual(("Hi Your name", false), environment.Clipboard.Writes.Single());

        // The page stays with its snippet for up to a minute, so answers kept after a copy came back with it.
        Assert.AreEqual(0, answer.Text.Length, "Answers aren't kept after the copy, as after a paste.");
    }

    [TestMethod]
    public void Copy_ClipboardWontTakeIt_KeepsTheAnswers()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        environment.Clipboard.NextWrite = ClipboardWrite.Refused;
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";

        var copy = (CopySnippetCommand)((CommandContextItem)page.Command.MoreCommands.Single()).Command!;
        _ = copy.Invoke();

        Assert.AreEqual("Your name", answer.Text, "The user can try again.");
    }

    [TestMethod]
    public void Paste_SnippetTakingPartOfTheCopy_GoesOnToPickOnceTheQuestionsAreAnswered()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", "Hi {{prompt:Name}}, reply to " + AskingSpots.Email));
        environment.Clipboard.Text = AskingSpots.Copied;
        var page = new SnippetAnswersPage(environment.SnippetFor("!reply"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";

        var picks = (SnippetPicksPage)page.Command.Command!;
        Assert.AreEqual(0, environment.Clipboard.Reads, "The copy is read once the questions are answered, as in Text Expander.");
        var item = picks.GetItems()[1];
        var result = ((IInvokableCommand)item.Command!).Invoke(null);

        Assert.AreEqual(0, page.Command.MoreCommands.Length, "Copy comes with the picks, on the pick page.");
        Assert.AreEqual("Hi Your name, reply to team@example.com", item.Subtitle);
        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual("Hi Your name, reply to team@example.com", environment.Clipboard.Writes.Single().Text);
        Assert.AreEqual(string.Empty, answer.Text, "Answers aren't kept after the paste.");
    }
}
