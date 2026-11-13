// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

internal enum SnippetTokenKind
{
    Text,
    Date,
    Time,
    Prompt,
    Clipboard,
    Cursor,
    LineBreak,
    Tab,
}
