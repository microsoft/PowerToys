// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Models;

public enum PasteFormats
{
    [PasteFormatMetadata(IsCoreAction = true, ResourceId = "PasteAsPlainText", IconGlyph = "\uE8E9", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Text)]
    PlainText,

    [PasteFormatMetadata(IsCoreAction = true, ResourceId = "PasteAsMarkdown", IconGlyph = "\ue8a5", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Text)]
    Markdown,

    [PasteFormatMetadata(IsCoreAction = true, ResourceId = "PasteAsJson", IconGlyph = "\uE943", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Text)]
    Json,

    [PasteFormatMetadata(IsCoreAction = false, ResourceId = "ImageToText", IconGlyph = "\uE91B", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Image | ClipboardFormat.ImageFile, IPCKey = AdvancedPasteAdditionalActions.PropertyNames.ImageToText)]
    ImageToText,

    [PasteFormatMetadata(IsCoreAction = false, ResourceId = "PasteAsTxtFile", IconGlyph = "\uE8D2", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Text | ClipboardFormat.Html, IPCKey = AdvancedPastePasteAsFileAction.PropertyNames.PasteAsTxtFile)]
    PasteAsTxtFile,

    [PasteFormatMetadata(IsCoreAction = false, ResourceId = "PasteAsPngFile", IconGlyph = "\uE8B9", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Image | ClipboardFormat.ImageFile, IPCKey = AdvancedPastePasteAsFileAction.PropertyNames.PasteAsPngFile)]
    PasteAsPngFile,

    [PasteFormatMetadata(IsCoreAction = false, ResourceId = "PasteAsHtmlFile", IconGlyph = "\uF6FA", RequiresAIService = false, SupportedClipboardFormats = ClipboardFormat.Html, IPCKey = AdvancedPastePasteAsFileAction.PropertyNames.PasteAsHtmlFile)]
    PasteAsHtmlFile,

    [PasteFormatMetadata(IsCoreAction = false, IconGlyph = "\uE945", RequiresAIService = true, SupportedClipboardFormats = ClipboardFormat.Text)]
    Custom,
}
