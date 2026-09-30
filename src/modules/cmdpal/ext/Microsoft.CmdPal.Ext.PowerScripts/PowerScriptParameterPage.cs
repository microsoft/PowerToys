// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>A Command Palette-owned parameter page for one PowerScript.</summary>
internal sealed partial class PowerScriptParameterPage : ParametersPage
{
    private readonly PowerScriptInfo _script;
    private readonly Dictionary<string, Func<string?>> _valueReaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly IParameterRun[] _parameters;
    private readonly ListItem _commandItem;

    public PowerScriptParameterPage(PowerScriptInfo script, IconInfo icon)
    {
        _script = script;
        Name = script.Name;
        Title = script.Name;
        Icon = icon;

        var parameters = new List<IParameterRun>();
        foreach (var parameter in script.Parameters)
        {
            var label = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            parameters.Add(new LabelRun(label + (parameter.IsRequired ? " *" : string.Empty)));
            parameters.Add(CreateParameterRun(parameter));
            if (!string.IsNullOrWhiteSpace(parameter.Description))
            {
                parameters.Add(new LabelRun(parameter.Description));
            }
        }

        _parameters = parameters.ToArray();
        _commandItem = new ListItem(new RunParameterizedPowerScriptCommand(this, icon))
        {
            Title = "Run",
            Subtitle = script.Description,
            Icon = icon,
        };
    }

    public override IParameterRun[] Parameters => _parameters;

    public override IListItem Command => _commandItem;

    private IParameterRun CreateParameterRun(PowerScriptParameterInfo parameter)
    {
        var placeholder = string.IsNullOrWhiteSpace(parameter.Description)
            ? parameter.Name
            : parameter.Description;

        if (string.Equals(parameter.Type, "file", StringComparison.OrdinalIgnoreCase))
        {
            var file = new OptionalFilePickerParameterRun
            {
                PlaceholderText = placeholder,
                Required = parameter.IsRequired,
            };
            _valueReaders[parameter.Name] = () => file.File?.Path;
            return file;
        }

        if (string.Equals(parameter.Type, "bool", StringComparison.OrdinalIgnoreCase))
        {
            return CreateSelectionParameter(
                parameter,
                ["false", "true"],
                value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "On" : "Off");
        }

        if (string.Equals(parameter.Type, "choice", StringComparison.OrdinalIgnoreCase))
        {
            return CreateSelectionParameter(parameter, parameter.Options, value => value);
        }

        var text = new OptionalStringParameterRun
        {
            PlaceholderText = placeholder,
            Required = parameter.IsRequired,
            Text = parameter.Default ?? string.Empty,
        };
        _valueReaders[parameter.Name] = () => text.Text;
        return text;
    }

    private IParameterRun CreateSelectionParameter(
        PowerScriptParameterInfo parameter,
        IEnumerable<string> values,
        Func<string, string> displayText)
    {
        var selectionPage = new StaticParameterList<string>(
            values,
            (value, item) =>
            {
                item.Title = displayText(value);
                return item;
            })
        {
            Name = parameter.Name,
            Title = parameter.Name,
        };

        var selection = new OptionalCommandParameterRun
        {
            Command = selectionPage,
            PlaceholderText = parameter.Name,
            Required = parameter.IsRequired,
        };

        selectionPage.ValueSelected += (_, value) =>
        {
            selection.Value = value;
            selection.DisplayText = displayText(value);
        };

        if (!string.IsNullOrEmpty(parameter.Default))
        {
            selection.Value = parameter.Default;
            selection.DisplayText = displayText(parameter.Default);
        }
        else if (string.Equals(parameter.Type, "bool", StringComparison.OrdinalIgnoreCase))
        {
            selection.Value = "false";
            selection.DisplayText = displayText("false");
        }

        _valueReaders[parameter.Name] = () => selection.Value?.ToString();
        return selection;
    }

    private ICommandResult Run()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in _script.Parameters)
        {
            var value = _valueReaders[parameter.Name]();
            if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                ShowError($"{parameter.Name} is required.");
                return CommandResult.KeepOpen();
            }

            if (string.Equals(parameter.Type, "int", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(value))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    ShowError($"{parameter.Name} must be an integer.");
                    return CommandResult.KeepOpen();
                }

                if (parameter.Min is { } min && number < min)
                {
                    ShowError($"{parameter.Name} must be at least {min}.");
                    return CommandResult.KeepOpen();
                }

                if (parameter.Max is { } max && number > max)
                {
                    ShowError($"{parameter.Name} must be at most {max}.");
                    return CommandResult.KeepOpen();
                }
            }

            if (!string.IsNullOrEmpty(value) || parameter.Default is not null)
            {
                values[parameter.Name] = value;
            }
        }

        Task.Run(() => PowerScriptHostClient.Run(_script.Id, values));
        return CommandResult.Dismiss();
    }

    private static void ShowError(string message)
    {
        new ToastStatusMessage(new StatusMessage
        {
            Message = message,
            State = MessageState.Error,
        }).Show();
    }

    private sealed partial class RunParameterizedPowerScriptCommand : InvokableCommand
    {
        private readonly PowerScriptParameterPage _page;

        public RunParameterizedPowerScriptCommand(PowerScriptParameterPage page, IconInfo icon)
        {
            _page = page;
            Name = "Run";
            Icon = icon;
        }

        public override ICommandResult Invoke() => _page.Run();
    }

    private sealed partial class OptionalStringParameterRun : StringParameterRun
    {
        public override bool NeedsValue => Required && string.IsNullOrEmpty(Text);
    }

    private sealed partial class OptionalCommandParameterRun : CommandParameterRun
    {
        public override bool NeedsValue => Required && Value is null;
    }

    private sealed partial class OptionalFilePickerParameterRun : FilePickerParameterRun
    {
        public override bool NeedsValue => Required && File is null;
    }
}
