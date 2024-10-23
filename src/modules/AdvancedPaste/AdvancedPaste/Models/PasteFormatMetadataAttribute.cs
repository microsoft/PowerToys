// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Models;

[AttributeUsage(AttributeTargets.Field)]
public sealed class PasteFormatMetadataAttribute : Attribute
{
    public bool IsCoreAction { get; init; }

    public string ResourceId { get; init; }

    public string IconGlyph { get; init; }

    public bool RequiresAIService { get; init; }

    public bool CanPreview { get; init; }

    public ClipboardFormat SupportedClipboardFormats { get; init; }

    public string IPCKey { get; init; }

    /// <summary>
    /// Gets a description of the action that should be exposed to Semantic Kernel, or <see langword="null"/> if it should not be exposed.
    /// </summary>
    public string KernelFunctionDescription { get; init; }

    public bool RequiresPrompt { get; init; }
}
