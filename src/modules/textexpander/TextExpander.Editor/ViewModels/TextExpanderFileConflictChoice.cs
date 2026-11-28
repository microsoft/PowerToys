// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    /// <summary>What Move snippets to… does with a file that already has snippets.</summary>
    public enum TextExpanderFileConflictChoice
    {
        /// <summary>Use the file's own snippets and write nothing, as Use a snippets file… does.</summary>
        UseFile,

        /// <summary>Back up the file, then write this PC's snippets over it.</summary>
        ReplaceFile,
    }
}
