// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>
    /// The snippets file has text that can't be written back as JSON, such as half of an emoji in a property this version
    /// keeps without reading. The file still reads and its snippets expand; it's fixed in a text editor.
    /// </summary>
    public sealed class SnippetFileWriteException : InvalidOperationException
    {
        public SnippetFileWriteException(Exception innerException)
            : base("The snippets file has text that can't be written back as JSON. " + innerException.Message, innerException)
        {
        }
    }
}
