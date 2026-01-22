// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class SettingsForm : FormContent
{
    private readonly Settings _settings;

    internal SettingsForm(Settings settings)
    {
        _settings = settings;
        TemplateJson = _settings.ToFormJson();
    }

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        var formInput = JsonNode.Parse(inputs)?.AsObject();
        if (formInput is null)
        {
            return CommandResult.KeepOpen();
        }

        // Re-render the current value of the settings to a card. The
        // SettingsContentPage will raise an ItemsChanged in its own
        // SettingsChange handler, so we need to be prepared to return the
        // current settings value.
        TemplateJson = _settings.ToFormJson();

        _settings.Update(inputs);
        _settings.RaiseSettingsChanged();

        return CommandResult.GoHome();
    }
}
