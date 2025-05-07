// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.WindowsTerminal;

public class TerminalProfile
{
    public TerminalPackage Terminal { get; }

    public string Name { get; }

    public Guid? Identifier { get; }

    public bool Hidden { get; }

    public string Icon { get; }

    public TerminalProfile(TerminalPackage terminal, string name, Guid? identifier, bool hidden, string icon)
    {
        Terminal = terminal;
        Name = name;
        Identifier = identifier;
        Hidden = hidden;
        Icon = icon;
    }
}
