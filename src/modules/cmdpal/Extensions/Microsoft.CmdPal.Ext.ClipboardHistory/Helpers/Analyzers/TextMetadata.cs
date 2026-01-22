// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal sealed record TextMetadata
{
    public int CharacterCount { get; init; }

    public int WordCount { get; init; }

    public int SentenceCount { get; init; }

    public int LineCount { get; init; }

    public int ParagraphCount { get; init; }

    public LineEndingType LineEnding { get; init; }

    public override string ToString()
    {
        return $"Characters: {CharacterCount}, Words: {WordCount}, Sentences: {SentenceCount}, Lines: {LineCount}, Paragraphs: {ParagraphCount}, Line Ending: {LineEnding}";
    }
}
