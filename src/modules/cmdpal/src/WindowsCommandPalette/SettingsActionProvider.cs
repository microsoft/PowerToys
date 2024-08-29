// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.CmdPal.Common.Contracts;
using Microsoft.CmdPal.Common.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Run.Settings;

internal sealed class SettingsPage : FormPage
{
    private readonly SettingsForm _settings = new();
    public override IForm[] Forms() => [_settings];
    public SettingsPage()
    {
        this.Icon = new("\uE713");
        this.Name = "Settings";
    }
}

internal sealed class SettingsForm: Form
{
    public SettingsForm()
    {
    }
        public override string TemplateJson()
    {
        var json = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
          "type": "TextBlock",
          "text": "🚧 This page is a work in progress 🚧",
          "weight": "bolder",
          "size": "extraLarge",
          "spacing": "none",
          "wrap": true,
          "style": "heading"
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "hotkey",
            "label": "Global hotkey",
            "value": "${hotkey}",
            "isRequired": false
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Save",
            "data": {
                "name": "name",
                "url": "url"
            }
        }
    ]
}
""";
        return json;
    }

    public override string DataJson()
    {
        var t = GetSettingsDataJson();
        t.ConfigureAwait(false);
        return t.Result;
    }
    private static async Task<string> GetSettingsDataJson()
    {

        var hotkey = "win+ctrl+.";
        try
        {
            hotkey = await Application.Current.GetService<ILocalSettingsService>().ReadSettingAsync<string>("GlobalHotkey") ;
        }
        catch (Exception ex)
        {
            _ = ex.Message;
        }
        return $$"""
{
    "hotkey": "{{hotkey}}"
}
""";
    }
    public override string StateJson() => throw new NotImplementedException();
    public override ActionResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput == null) {
            return ActionResult.GoHome();
        }
        Application.Current.GetService<ILocalSettingsService>().SaveSettingAsync("GlobalHotkey", formInput["hotkey"]?.ToString() ?? "");

        return ActionResult.GoHome();
    }
}

public class SettingsActionProvider : ICommandProvider
{
    public string DisplayName => $"Settings";

    private readonly SettingsPage settingsPage = new();

    public SettingsActionProvider()
    {
    }

    public IconDataType Icon => new("");

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize


    public IListItem[] TopLevelCommands()
    {
        return [ new ListItem(settingsPage) { Subtitle = "Runv2 settings"} ];
    }
}

