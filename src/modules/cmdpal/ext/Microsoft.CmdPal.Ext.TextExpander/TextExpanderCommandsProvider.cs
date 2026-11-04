// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander;

/// <summary>
/// Text Expander's snippets in Command Palette: search them, and paste one into the app Command Palette was opened
/// over. Snippets are read from Text Expander's own file, the way Text Expander reads it.
/// </summary>
public sealed partial class TextExpanderCommandsProvider : CommandProvider
{
    internal const string ProviderId = "com.microsoft.cmdpal.builtin.textexpander";

    private readonly ICommandItem[] _topLevelCommands;
    private readonly IFallbackCommandItem[] _fallbackCommands;

    public TextExpanderCommandsProvider()
        : this(new SnippetActions(new SnippetSource(), new SnippetInserter()))
    {
    }

    internal TextExpanderCommandsProvider(SnippetActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        Id = ProviderId;
        DisplayName = Resources.provider_display_name;
        Icon = Icons.TextExpander;

        _topLevelCommands =
        [
            new CommandItem(new SnippetsPage(actions))
            {
                Title = Resources.snippets_title,
                Subtitle = Resources.snippets_subtitle,
                Icon = Icons.TextExpander,
                MoreCommands = [new CommandContextItem(new OpenTextExpanderSettingsCommand())],
            },
        ];
        _fallbackCommands = [new SnippetFallbackItem(actions)];
    }

    public override ICommandItem[] TopLevelCommands() => _topLevelCommands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;
}
