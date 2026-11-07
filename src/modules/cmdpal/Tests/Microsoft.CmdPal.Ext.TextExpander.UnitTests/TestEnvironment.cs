// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

/// <summary>
/// Everything the extension needs, made for one test: its own PowerToys folder, group policies, a clock for the
/// main search's reuse of what was read, and a clipboard and desktop in memory.
/// </summary>
internal sealed class TestEnvironment : IDisposable
{
    public TestEnvironment()
    {
        Source = new SnippetSource(Folder.Paths, Policies, () => Milliseconds);
        Inserter = new SnippetInserter(Clipboard, Desktop, Clock);
        Actions = new SnippetActions(Source, Inserter);
    }

    public TestFolder Folder { get; } = new();

    public FakePolicyStore Policies { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeDesktop Desktop { get; } = new();

    public FakeClock Clock { get; } = new();

    public long Milliseconds { get; set; } = 1_000_000;

    public SnippetSource Source { get; }

    public SnippetInserter Inserter { get; }

    public SnippetActions Actions { get; }

    /// <summary>Turns Text Expander on and saves these snippets in its default file.</summary>
    public void UseSnippets(params (string Trigger, string Replacement)[] snippets)
    {
        Folder.SetEnabled(true);
        Folder.WriteSnippets(snippets);
    }

    public Snippet SnippetFor(string trigger)
    {
        foreach (var snippet in Source.GetState().Library.Snippets)
        {
            if (snippet.Trigger == trigger)
            {
                return snippet;
            }
        }

        throw new KeyNotFoundException(trigger);
    }

    public void Dispose() => Folder.Dispose();
}
