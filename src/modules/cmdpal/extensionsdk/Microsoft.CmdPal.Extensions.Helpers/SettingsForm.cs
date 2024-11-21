// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class SettingsForm : Form
{
    private readonly Settings _settings;

    internal SettingsForm(Settings settings)
    {
        _settings = settings;
        Template = _settings.ToFormJson();
    }

    public override ICommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput == null)
        {
            return CommandResult.KeepOpen();
        }

        _settings.Update(payload);
        _settings.RaiseSettingsChanged();

        return CommandResult.GoHome();
    }
}
