// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Models;

public enum PasteFormats
{
    [PasteFormatMetadata(
        IsCoreAction = true,
        ResourceId = "PasteAsPlainText",
        IconGlyph = "\uE8E9",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Text,
        KernelFunctionDescription = "Takes clipboard text and returns it as it is.")]
    PlainText,

    [PasteFormatMetadata(
        IsCoreAction = true,
        ResourceId = "PasteAsMarkdown",
        IconGlyph = "\ue8a5",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Text,
        KernelFunctionDescription = "Takes clipboard text and formats it as markdown text.")]
    Markdown,

    [PasteFormatMetadata(
        IsCoreAction = true,
        ResourceId = "PasteAsJson",
        IconGlyph = "\uE943",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Text,
        KernelFunctionDescription = "Takes clipboard text and formats it as JSON text.")]
    Json,

    [PasteFormatMetadata(
        IsCoreAction = false,
        ResourceId = "ImageToText",
        IconGlyph = "\uE91B",
        RequiresAIService = false,
        CanPreview = true,
        SupportedClipboardFormats = ClipboardFormat.Image,
        IPCKey = AdvancedPasteAdditionalActions.PropertyNames.ImageToText,
        KernelFunctionDescription = "Takes an image in the clipboard and extracts all text from it using OCR.")]
    ImageToText,

    [PasteFormatMetadata(
        IsCoreAction = false,
        ResourceId = "PasteAsTxtFile",
        IconGlyph = "\uE8D2",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Text | ClipboardFormat.Html,
        IPCKey = AdvancedPastePasteAsFileAction.PropertyNames.PasteAsTxtFile,
        KernelFunctionDescription = "Takes text or HTML data in the clipboard and transforms it to a TXT file.")]
    PasteAsTxtFile,

    [PasteFormatMetadata(
        IsCoreAction = false,
        ResourceId = "PasteAsPngFile",
        IconGlyph = "\uE8B9",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Image,
        IPCKey = AdvancedPastePasteAsFileAction.PropertyNames.PasteAsPngFile,
        KernelFunctionDescription = "Takes an image in the clipboard and transforms it to a PNG file.")]
    PasteAsPngFile,

    [PasteFormatMetadata(
        IsCoreAction = false,
        ResourceId = "PasteAsHtmlFile",
        IconGlyph = "\uF6FA",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Html,
        IPCKey = AdvancedPastePasteAsFileAction.PropertyNames.PasteAsHtmlFile,
        KernelFunctionDescription = "Takes HTML data in the clipboard and transforms it to an HTML file.")]
    PasteAsHtmlFile,

    [PasteFormatMetadata(
        IsCoreAction = false,
        ResourceId = "TranscodeToMp3",
        IconGlyph = "\uE8D6",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Audio | ClipboardFormat.Video,
        IPCKey = AdvancedPasteTranscodeAction.PropertyNames.TranscodeToMp3,
        KernelFunctionDescription = "Takes an audio or video file in the clipboard and transcodes it to MP3.")]
    TranscodeToMp3,

    [PasteFormatMetadata(
        IsCoreAction = false,
        ResourceId = "TranscodeToMp4",
        IconGlyph = "\uE714",
        RequiresAIService = false,
        CanPreview = false,
        SupportedClipboardFormats = ClipboardFormat.Video,
        IPCKey = AdvancedPasteTranscodeAction.PropertyNames.TranscodeToMp4,
        KernelFunctionDescription = "Takes a video file in the clipboard and transcodes it to MP4 (H.264/AAC).")]
    TranscodeToMp4,

    [PasteFormatMetadata(
        IsCoreAction = false,
        IconGlyph = "\uE945",
        RequiresAIService = true,
        CanPreview = true,
        SupportedClipboardFormats = ClipboardFormat.Text | ClipboardFormat.Html | ClipboardFormat.Audio | ClipboardFormat.Video | ClipboardFormat.Image,
        RequiresPrompt = true)]
    KernelQuery,

    [PasteFormatMetadata(
        IsCoreAction = false,
        IconGlyph = "\uE945",
        RequiresAIService = true,
        CanPreview = true,
        SupportedClipboardFormats = ClipboardFormat.Text,
        KernelFunctionDescription = "Takes input instructions and transforms clipboard text (not TXT files) with these input instructions, putting the result back on the clipboard. This uses AI to accomplish the task.",
        RequiresPrompt = true)]
    CustomTextTransformation,
}
