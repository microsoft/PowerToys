// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.CmdPal.Ext.ClipboardHistory.Models;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal sealed class TextMetadataProvider : IClipboardMetadataProvider
{
    public string SectionTitle => Resources.metadata_text_section_title;

    public bool CanHandle(ClipboardItem item) => item.IsText;

    public IEnumerable<DetailsElement> GetDetails(ClipboardItem item)
    {
        var result = new List<DetailsElement>();
        if (!CanHandle(item) || string.IsNullOrEmpty(item.Content))
        {
            return result;
        }

        var r = new TextMetadataAnalyzer().Analyze(item.Content);

        result.Add(new DetailsElement
        {
            Key = Resources.metadata_text_characters_key,
            Data = new DetailsLink(r.CharacterCount.ToString(CultureInfo.CurrentCulture)),
        });
        result.Add(new DetailsElement
        {
            Key = Resources.metadata_text_words_key,
            Data = new DetailsLink(r.WordCount.ToString(CultureInfo.CurrentCulture)),
        });
        result.Add(new DetailsElement
        {
            Key = Resources.metadata_text_sentences_key,
            Data = new DetailsLink(r.SentenceCount.ToString(CultureInfo.CurrentCulture)),
        });
        result.Add(new DetailsElement
        {
            Key = Resources.metadata_text_lines_key,
            Data = new DetailsLink(r.LineCount.ToString(CultureInfo.CurrentCulture)),
        });
        result.Add(new DetailsElement
        {
            Key = Resources.metadata_text_paragraphs_key,
            Data = new DetailsLink(r.ParagraphCount.ToString(CultureInfo.CurrentCulture)),
        });
        result.Add(new DetailsElement
        {
            Key = Resources.metadata_text_line_ending_key,
            Data = new DetailsLink(r.LineEnding.ToString()),
        });

        return result;
    }

    public IEnumerable<ProviderAction> GetActions(ClipboardItem item) => [];
}
