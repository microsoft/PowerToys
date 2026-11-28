// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>The box for one of a snippet's questions, in Command Palette's search bar.</summary>
internal sealed partial class AnswerRun : StringParameterRun
{
    public AnswerRun(bool required)
    {
        Required = required;
        PlaceholderText = required ? Resources.answers_required_placeholder : Resources.answers_optional_placeholder;
    }

    /// <summary>
    /// Gets a value indicating whether the snippet has to wait for this answer. As in Text Expander, a required answer
    /// can't be blank, and an optional one (<c>{{prompt?:...}}</c>) can be left empty.
    /// </summary>
    public override bool NeedsValue => Required && string.IsNullOrWhiteSpace(Text);
}
