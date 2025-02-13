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
        if (formInput == null)
        {
            return CommandResult.KeepOpen();
        }

        _settings.Update(inputs);
        _settings.RaiseSettingsChanged();

        return CommandResult.GoHome();
    }
}
