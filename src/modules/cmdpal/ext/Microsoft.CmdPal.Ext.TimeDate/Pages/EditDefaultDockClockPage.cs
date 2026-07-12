// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

#pragma warning disable SA1402 // Page and form are private implementation details of this page.

internal sealed partial class EditDefaultDockClockPage : ContentPage
{
    private readonly EditDefaultDockClockForm _form;

    internal EditDefaultDockClockPage(IDockClockSettings dockClockSettings)
    {
        Icon = Icons.EditIcon;
        Title = Resources.timedate_custom_clock_customize_dock;
        Name = Title;
        _form = new EditDefaultDockClockForm(dockClockSettings);
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class EditDefaultDockClockForm : FormContent
{
    private readonly IDockClockSettings _settings;

    internal EditDefaultDockClockForm(IDockClockSettings settings)
    {
        _settings = settings;
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    { "type": "Input.ChoiceSet", "id": "titleFormat", "label": {{Encode(Resources.timedate_custom_clock_title_format)}}, "value": {{Encode(GetTitleFormat(settings.DockClockTitleFormat))}}, "style": "compact", "choices": {{BuildChoices(settings, GetTitleFormat(settings.DockClockTitleFormat), includeNoText: false)}} },
    { "type": "Input.ChoiceSet", "id": "subtitleFormat", "label": {{Encode(Resources.timedate_custom_clock_subtitle_format)}}, "value": {{Encode(settings.DockClockSubtitleFormat)}}, "style": "compact", "choices": {{BuildChoices(settings, settings.DockClockSubtitleFormat)}} },
    { "type": "TextBlock", "text": {{Encode(Resources.timedate_custom_clock_relative_hint)}}, "wrap": true, "isSubtle": true, "size": "Small" }
  ],
  "actions": [ { "type": "Action.Submit", "title": {{Encode(Resources.timedate_custom_clock_save)}}, "data": { "titleFormat": "titleFormat", "subtitleFormat": "subtitleFormat" } } ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var input = JsonNode.Parse(payload);
        if (input is not null)
        {
            _settings.SetDockClockFormats(
                input["titleFormat"]?.ToString() ?? "t",
                input["subtitleFormat"]?.ToString() ?? "d");
        }

        return CommandResult.GoBack();
    }

    private static string BuildChoices(ISettingsInterface settings, string selectedValue, bool includeNoText = true)
    {
        var choices = CustomClockFormatOptions.Get(settings, includeNoText).ToList();
        if (choices.All(choice => choice.Value != selectedValue))
        {
            choices.Add((selectedValue, selectedValue));
        }

        return "[" + string.Join(",", choices.Select(choice =>
            $$"""{ "title": {{Encode(choice.Title)}}, "value": {{Encode(choice.Value)}} }""")) + "]";
    }

    private static string Encode(string value) => JsonSerializer.Serialize(value, CustomClockJsonContext.Default.String);

    private static string GetTitleFormat(string format) => string.IsNullOrEmpty(format) ? "t" : format;
}

#pragma warning restore SA1402 // File may only contain a single type
