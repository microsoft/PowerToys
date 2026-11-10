// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>Runs a parameterless script only when the user explicitly invokes its list item.</summary>
internal sealed partial class RunPowerScriptCommand : InvokableCommand
{
    private static readonly IReadOnlyDictionary<string, JsonElement> NoParameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly PowerScriptRunSession _session;
    private readonly PowerScriptPage _page;
    private readonly Action<IPage> _navigate;
    private PowerScriptInfo _script;

    internal RunPowerScriptCommand(PowerScriptInfo script, PowerScriptRunSession session, PowerScriptPage page, Action<IPage> navigate)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(navigate);
        _script = script;
        _session = session;
        _page = page;
        _navigate = navigate;
        Id = page.Id + ".run";
        Name = Resources.Run;
        Icon = page.Icon;
    }

    internal void UpdateScript(PowerScriptInfo script)
    {
        lock (_gate)
        {
            _script = script;
        }

        Icon = new IconInfo(script.IconPath ?? "\uE756");
    }

    public override ICommandResult Invoke()
    {
        PowerScriptInfo script;
        lock (_gate)
        {
            script = _script;
        }

        // Request the result page before starting work so it owns the run and its result.
        // Browsing, filtering, and rendering this page never call TryRun.
        _navigate(_page);
        if (script.Parameters.Count == 0)
        {
            // Metadata refresh can turn a cached parameterless action into a parameter form.
            // The session also rejects a second invocation while its current run is active.
            _session.TryRun(script, NoParameters);
        }

        return CommandResult.KeepOpen();
    }
}
