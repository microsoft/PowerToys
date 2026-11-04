// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

/// <summary>Clipboard variables for one part of what's on the clipboard, which ask which match when there are several.</summary>
internal static class AskingSpots
{
    public const string Email = "{{clipboard:email}}";

    public const string Link = "{{clipboard:link}}";

    public const string Phone = "{{clipboard:phone}}";

    /// <summary>What the user copied: two email addresses, a link and two phone numbers.</summary>
    public const string Copied = "Write to you@example.com or team@example.com, see https://example.com/docs, call 555-0123 or 555-0199.";
}
