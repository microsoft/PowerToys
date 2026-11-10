// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed partial class PowerScriptParameterForm : FormContent
{
    private readonly object _gate = new();
    private readonly PowerScriptRunSession _session;
    private PowerScriptInfo _script;
    private IReadOnlyDictionary<string, string> _values;
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();
    private bool _parametersChanged;

    internal PowerScriptParameterForm(PowerScriptInfo script, PowerScriptRunSession session)
    {
        _script = script;
        _session = session;
        _values = PowerScriptParameterValues.CreateInitialValues(script);
        Refresh();
    }

    internal void UpdateScript(PowerScriptInfo script)
    {
        lock (_gate)
        {
            if (_session.IsRunning)
            {
                return;
            }

            if (!PowerScriptsService.HasSameParameterContract(_script, script))
            {
                _values = PowerScriptParameterValues.CreateInitialValues(script);
                _errors = new Dictionary<string, string>();
                _parametersChanged = true;
            }

            _script = script;
            Refresh();
        }
    }

    internal void Refresh()
    {
        lock (_gate)
        {
            var running = _session.IsRunning;
            var fields = new JsonArray();
            var body = new JsonArray();
            var data = new JsonObject
            {
                ["description"] = _script.Description,
                ["status"] = GetStatus(),
                ["fields"] = fields,
                ["trustPrompt"] = Resources.TrustPrompt,
                ["stopNotice"] = Resources.StopNotice,
                ["intro"] = _script.Parameters.Count > 0 ? Resources.ParametersIntro : string.Empty,
                ["optionalHint"] = Resources.OptionalParametersHint,
                ["formError"] = _errors.TryGetValue("_form", out var formError) ? formError : Resources.FormErrors,
                ["parametersChanged"] = Resources.ParametersChanged,
                ["resultHint"] = Resources.TextResultHint,
            };

            if (_session.State != PowerScriptRunState.Idle)
            {
                var status = Text("${status}");
                status["weight"] = "Bolder";
                status["size"] = "Medium";
                status["style"] = "heading";
                body.Add((JsonNode?)status);
            }

            if (_session.Result?.Text is not null)
            {
                body.Add((JsonNode?)Text("${resultHint}", subtle: true));
            }

            if (!string.IsNullOrWhiteSpace(_script.Description))
            {
                body.Add((JsonNode?)Text("${description}"));
            }

            if (!_script.Trusted)
            {
                body.Add((JsonNode?)Text("${trustPrompt}", subtle: true));
            }

            if (_session.StopWasRequested || _session.State == PowerScriptRunState.TimedOut)
            {
                body.Add((JsonNode?)Text("${stopNotice}", subtle: true));
            }

            if (_errors.Count > 0)
            {
                var errorBlock = Text("${formError}");
                errorBlock["color"] = "Attention";
                body.Add((JsonNode?)errorBlock);
            }

            if (_parametersChanged)
            {
                body.Add((JsonNode?)Text("${parametersChanged}"));
            }

            if (!running && _script.Parameters.Count > 0)
            {
                body.Add((JsonNode?)Text("${intro}", subtle: true));
                if (_script.Parameters.Any(parameter => !parameter.IsRequired))
                {
                    body.Add((JsonNode?)Text("${optionalHint}", subtle: true));
                }
            }

            // Keep field data available while changing templates. The host can
            // observe DataJson before it observes the new TemplateJson.
            var parameterBody = running ? new JsonArray() : body;
            for (var index = 0; index < _script.Parameters.Count; index++)
            {
                AddParameter(parameterBody, fields, _script.Parameters[index], index);
            }

            var action = new JsonObject
            {
                ["type"] = "Action.Submit",
                ["title"] = "${actionTitle}",
                ["associatedInputs"] = running ? "none" : "auto",
                ["isEnabled"] = _session.State != PowerScriptRunState.StopRequested,
                ["data"] = new JsonObject { ["action"] = running ? "stop" : "run" },
            };
            data["actionTitle"] = running
                ? Resources.Stop
                : _session.State is PowerScriptRunState.Succeeded or PowerScriptRunState.Failed or PowerScriptRunState.Stopped or PowerScriptRunState.TimedOut
                    ? Resources.RunAgain
                    : Resources.Run;

            var card = new JsonObject
            {
                ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                ["type"] = "AdaptiveCard",
                ["version"] = "1.5",
                ["body"] = body,
                ["actions"] = new JsonArray(action),
            };

            // Metadata is data-bound so script names, help, and errors cannot introduce
            // Adaptive Card template expressions or additional controls.
            DataJson = data.ToJsonString();
            TemplateJson = card.ToJsonString();
        }
    }

    public override ICommandResult SubmitForm(string inputs) => SubmitForm(inputs, string.Empty);

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        lock (_gate)
        {
            try
            {
                var action = "run";
                if (!string.IsNullOrWhiteSpace(data))
                {
                    using var actionDocument = JsonDocument.Parse(data);
                    if (actionDocument.RootElement.ValueKind != JsonValueKind.Object ||
                        !actionDocument.RootElement.TryGetProperty("action", out var actionValue) ||
                        actionValue.ValueKind != JsonValueKind.String)
                    {
                        ShowFormError();
                        return CommandResult.KeepOpen();
                    }

                    action = actionValue.GetString() ?? string.Empty;
                }

                if (action == "stop")
                {
                    _session.RequestStop();
                    return CommandResult.KeepOpen();
                }

                if (_session.IsRunning)
                {
                    return CommandResult.KeepOpen();
                }

                if (action != "run")
                {
                    ShowFormError();
                    return CommandResult.KeepOpen();
                }

                using var inputDocument = JsonDocument.Parse(inputs);
                if (inputDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    ShowFormError();
                    return CommandResult.KeepOpen();
                }

                var submitted = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in inputDocument.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                    {
                        ShowFormError();
                        return CommandResult.KeepOpen();
                    }

                    submitted[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.GetRawText();
                }

                _values = submitted;
                if (PowerScriptParameterValues.TryRead(_script, submitted, out var parameters, out var errors))
                {
                    _errors = new Dictionary<string, string>();
                    _parametersChanged = false;
                    _session.TryRun(_script, parameters);
                }
                else
                {
                    _errors = errors;
                }

                Refresh();
            }
            catch (JsonException)
            {
                ShowFormError();
            }
        }

        return CommandResult.KeepOpen();
    }

    private void AddParameter(JsonArray body, JsonArray fields, PowerScriptParameter parameter, int index)
    {
        var suffix = index.ToString(CultureInfo.InvariantCulture);
        var id = "parameter_" + suffix;
        var value = _values.TryGetValue(id, out var rawValue) ? rawValue : string.Empty;
        var label = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
        var field = new JsonObject
        {
            ["label"] = parameter.IsRequired ? PowerScriptFormatting.Format(Resources.RequiredLabel, label) : label,
            ["value"] = value,
            ["numberValue"] = 0,
            ["description"] = parameter.Description ?? string.Empty,
            ["error"] = _errors.TryGetValue(id, out var error) ? error : string.Empty,
        };
        fields.Add((JsonNode?)field);

        if (!parameter.IsRequired)
        {
            field["includeLabel"] = PowerScriptFormatting.Format(Resources.IncludeParameter, label);
            field["include"] = _values.TryGetValue("include_" + suffix, out var included) ? included : "false";
            body.Add((JsonNode?)new JsonObject
            {
                ["type"] = "Input.Toggle",
                ["id"] = "include_" + suffix,
                ["label"] = Binding(index, "includeLabel"),
                ["title"] = string.Empty,
                ["value"] = Binding(index, "include"),
                ["valueOn"] = "true",
                ["valueOff"] = "false",
            });
        }

        var input = new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = id,
            ["label"] = Binding(index, "label"),
            ["value"] = Binding(index, "value"),
        };
        switch (parameter.Type.ToLowerInvariant())
        {
            case "int":
                // Input.Number submits only numbers. Retain any invalid submitted text
                // using Input.Text so validation failures never silently erase a value.
                if (string.IsNullOrEmpty(value) || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    input["type"] = "Input.Number";
                    if (string.IsNullOrEmpty(value))
                    {
                        input.Remove("value");
                    }
                    else
                    {
                        field["numberValue"] = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        input["value"] = Binding(index, "numberValue");
                    }
                }

                field["hint"] = parameter.Min is { } minimum && parameter.Max is { } maximum
                    ? PowerScriptFormatting.Format(Resources.IntegerRange, minimum, maximum)
                    : parameter.Min is { } min
                        ? PowerScriptFormatting.Format(Resources.IntegerMin, min)
                        : parameter.Max is { } max
                            ? PowerScriptFormatting.Format(Resources.IntegerMax, max)
                            : string.Empty;
                break;
            case "bool":
                input["type"] = "Input.ChoiceSet";
                input["style"] = "compact";
                field["trueLabel"] = Resources.TrueValue;
                field["falseLabel"] = Resources.FalseValue;
                input["choices"] = new JsonArray(
                    new JsonObject { ["title"] = Binding(index, "trueLabel"), ["value"] = "true" },
                    new JsonObject { ["title"] = Binding(index, "falseLabel"), ["value"] = "false" });
                break;
            case "choice":
                input["type"] = "Input.ChoiceSet";
                input["style"] = "compact";
                var options = new JsonArray();
                var choices = new JsonArray();
                for (var optionIndex = 0; optionIndex < parameter.Options.Count; optionIndex++)
                {
                    options.Add((JsonNode?)JsonValue.Create(parameter.Options[optionIndex]));
                    var binding = Binding(index, $"options[{optionIndex.ToString(CultureInfo.InvariantCulture)}]");
                    choices.Add((JsonNode?)new JsonObject { ["title"] = binding, ["value"] = binding });
                }

                field["options"] = options;
                input["choices"] = choices;
                break;
            case "file":
                field["hint"] = Resources.FilePathHint;
                break;
        }

        body.Add((JsonNode?)input);
        if (!string.IsNullOrEmpty(parameter.Description))
        {
            body.Add((JsonNode?)Text(Binding(index, "description"), subtle: true));
        }

        if (field["hint"] is JsonValue hint && !string.IsNullOrEmpty(hint.GetValue<string>()))
        {
            body.Add((JsonNode?)Text(Binding(index, "hint"), subtle: true));
        }

        if (_errors.ContainsKey(id))
        {
            var errorBlock = Text(Binding(index, "error"));
            errorBlock["color"] = "Attention";
            body.Add((JsonNode?)errorBlock);
        }
    }

    private string GetStatus() => _session.State switch
    {
        PowerScriptRunState.Running => Resources.Running,
        PowerScriptRunState.StopRequested => Resources.StopRequested,
        PowerScriptRunState.Succeeded => _session.Result?.Text is null ? Resources.CompletedNoOutput : Resources.Completed,
        PowerScriptRunState.Stopped => Resources.Stopped,
        PowerScriptRunState.TimedOut => Resources.TimedOut,
        PowerScriptRunState.Failed when _session.Result is { } result => PowerScriptFormatting.Format(Resources.RunFailedWithExitCode, result.ExitCode),
        PowerScriptRunState.Failed => string.IsNullOrEmpty(_session.Error) ? Resources.RunFailed : Resources.RunFailed + " " + _session.Error,
        _ => string.Empty,
    };

    private void ShowFormError()
    {
        _errors = new Dictionary<string, string> { ["_form"] = Resources.InvalidForm };
        Refresh();
    }

    private static string Binding(int index, string property) => $"${{fields[{index.ToString(CultureInfo.InvariantCulture)}].{property}}}";

    private static JsonObject Text(string value, bool subtle = false) => new()
    {
        ["type"] = "TextBlock",
        ["text"] = value,
        ["wrap"] = true,
        ["isSubtle"] = subtle,
    };
}
