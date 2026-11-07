// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>The snippets file isn't JSON, or has no "snippets" list. It's reported, and never written over.</summary>
    public sealed class SnippetFileFormatException : IOException
    {
        public SnippetFileFormatException(long? lineNumber, Exception? innerException)
            : base(innerException is null ? "The snippets file has no \"snippets\" list." : "The snippets file isn't valid JSON. " + innerException.Message, innerException)
        {
            LineNumber = lineNumber;
        }

        /// <summary>Gets the line, counting from 1, where the file stops being JSON, or null for a file without a "snippets" list.</summary>
        public long? LineNumber { get; }
    }
}
