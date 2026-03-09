// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

internal sealed partial class HostMatchedRemoteDesktopItem : FormattedFallbackCommandItem, IHostMatchedFallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.remotedesktop.host.fallback";
    private const string _matchPattern = @"(?:(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)(?:\.(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?))*|(?:\d{1,3}\.){3}\d{1,3}|\[[0-9A-Fa-f:.%]+\]|[0-9A-Fa-f:]+)(?::\d{1,5})?";
    private static readonly CompositeFormat RemoteDesktopOpenHostFormat = CompositeFormat.Parse(Resources.remotedesktop_open_host);
    private readonly OpenRemoteDesktopCommand _command = new(string.Empty);

    public HostMatchedRemoteDesktopItem()
        : base(
            new OpenRemoteDesktopCommand(string.Empty),
            Resources.remotedesktop_title,
            _id,
            titleTemplate: Resources.remotedesktop_open_host.Replace("{0}", "{query}", StringComparison.Ordinal),
            subtitleTemplate: Resources.remotedesktop_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.RDPIcon;
        Command = _command;
    }

    public HostMatchKind MatchKind => HostMatchKind.Regex;

    public string MatchValue => _matchPattern;

    public override void UpdateQuery(string query)
    {
        if (!OpenRemoteDesktopCommand.TryGetValidatedHost(query, out var validatedHost) || string.IsNullOrWhiteSpace(validatedHost))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = _command;
            return;
        }

        Title = string.Format(CultureInfo.CurrentCulture, RemoteDesktopOpenHostFormat, validatedHost);
        Subtitle = Resources.remotedesktop_title;
        Command = new OpenRemoteDesktopCommand(validatedHost);
    }
}
