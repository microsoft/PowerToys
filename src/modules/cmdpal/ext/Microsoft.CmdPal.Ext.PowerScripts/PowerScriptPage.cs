// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed partial class PowerScriptPage : ContentPage, IDisposable
{
    private const int PreviewLength = 16384;
    private readonly object _gate = new();
    private readonly PowerScriptRunSession _session;
    private readonly Lazy<PowerScriptParameterForm> _form;
    private readonly bool _ownsSession;
    private PowerScriptInfo _script;
    private IContent[] _content = [];

    internal PowerScriptPage(IPowerScriptsService service, PowerScriptInfo script, PowerScriptRunSession? session = null)
    {
        _ownsSession = session is null;
        _session = session ?? new PowerScriptRunSession(service, script);
        _script = script;
        _form = new Lazy<PowerScriptParameterForm>(() => new PowerScriptParameterForm(_script, _session));
        Id = "com.microsoft.cmdpal.builtin.powerscripts.script." + Uri.EscapeDataString(script.Id);
        Name = Resources.Open;
        Title = script.Name;
        Icon = new IconInfo(script.IconPath ?? "\uE756");
        _session.Changed += SessionChanged;
    }

    internal PowerScriptParameterForm ParameterForm => _form.Value;

    public override IContent[] GetContent()
    {
        _ = _form.Value;
        if (_content.Length == 0)
        {
            UpdateContent();
        }

        lock (_gate)
        {
            return _content;
        }
    }

    internal void UpdateScript(PowerScriptInfo script)
    {
        _script = script;
        if (_session.IsRunning)
        {
            return;
        }

        _session.UpdateScript(script);
        if (_form.IsValueCreated)
        {
            _form.Value.UpdateScript(script);
        }

        Title = script.Name;
        Icon = new IconInfo(script.IconPath ?? "\uE756");
        if (_form.IsValueCreated)
        {
            UpdateContent();
        }
    }

    public void Dispose()
    {
        _session.Changed -= SessionChanged;
        if (_ownsSession)
        {
            _session.Dispose();
        }
    }

    private void SessionChanged(object? sender, EventArgs args)
    {
        if (!_session.IsRunning)
        {
            // A refresh may have discovered new metadata while this run was active.
            _session.UpdateScript(_script);
            if (_form.IsValueCreated)
            {
                _form.Value.UpdateScript(_script);
            }

            Title = _script.Name;
            Icon = new IconInfo(_script.IconPath ?? "\uE756");
        }

        UpdateContent();
    }

    private void UpdateContent()
    {
        if (!_form.IsValueCreated)
        {
            return;
        }

        // Form submission can synchronously publish a session update while holding
        // the form lock. Always refresh it before taking the page's content lock.
        _form.Value.Refresh();
        var result = _session.Result;
        var content = new List<IContent>();
        var commands = new List<IContextItem>();
        if (result?.Text is { } output)
        {
            content.Add(CreateTextContent(Resources.Output, output, allowEmpty: true));
            commands.Add(new CommandContextItem(new CopyTextCommand(output) { Name = Resources.CopyOutput })
            {
                Title = Resources.CopyOutput,
            });
        }

        // stderr is diagnostic text. Only the host's exit code determines success.
        if (result is not null && !string.IsNullOrEmpty(result.StdErr))
        {
            content.Add(CreateTextContent(Resources.Diagnostics, result.StdErr));
            commands.Add(new CommandContextItem(new CopyTextCommand(result.StdErr) { Name = Resources.CopyDiagnostics })
            {
                Title = Resources.CopyDiagnostics,
            });
        }

        content.Add(_form.Value);

        lock (_gate)
        {
            _content = [.. content];
        }

        Commands = [.. commands];
        IsLoading = _session.IsRunning;
        RaiseItemsChanged();
    }

    private static PlainTextContent CreateTextContent(string heading, string text, bool allowEmpty = false)
    {
        var preview = text;
        if (text.Length > PreviewLength)
        {
            var length = char.IsHighSurrogate(text[PreviewLength - 1]) ? PreviewLength - 1 : PreviewLength;
            preview = text[..length] + Environment.NewLine + Environment.NewLine + Resources.OutputTruncated;
        }
        else if (allowEmpty && text.Length == 0)
        {
            preview = Resources.EmptyTextOutput;
        }

        return new PlainTextContent(heading + Environment.NewLine + Environment.NewLine + preview)
        {
            FontFamily = FontFamily.Monospace,
            WrapWords = true,
        };
    }
}
