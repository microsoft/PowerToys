// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>Everything this extension needs from Text Expander at one moment.</summary>
internal sealed record SnippetSourceState(TextExpanderAvailability Availability, TextExpanderSettings Settings, SnippetLibrarySnapshot Library)
{
    public bool IsOn => Availability == TextExpanderAvailability.On;
}
