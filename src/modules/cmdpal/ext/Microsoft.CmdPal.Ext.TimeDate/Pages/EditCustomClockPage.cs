// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

#pragma warning disable SA1402 // Form and command are private implementation details of this page.

internal sealed partial class EditCustomClockPage : ContentPage
{
    private readonly EditCustomClockForm _form;

    internal EditCustomClockPage(CustomClockManager clockManager, ISettingsInterface settings, CustomClock? clock, bool customizeDock = false)
    {
        Icon = clock is null ? Icons.AddIcon : Icons.EditIcon;
        Title = customizeDock
            ? Resources.timedate_custom_clock_customize_dock
            : clock is null
            ? Resources.timedate_custom_clock_add
            : Resources.timedate_custom_clock_edit;
        Name = Title;
        _form = new EditCustomClockForm(clockManager, settings, clock);
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class EditCustomClockForm : FormContent
{
    private readonly CustomClockManager _clockManager;
    private readonly ISettingsInterface _settings;
    private readonly CustomClock? _existingClock;

    internal EditCustomClockForm(CustomClockManager clockManager, ISettingsInterface settings, CustomClock? clock)
    {
        _clockManager = clockManager;
        _settings = settings;
        _existingClock = clock;
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    { "type": "Input.ChoiceSet", "id": "timeZoneId", "label": {{Encode(Resources.timedate_custom_clock_timezone)}}, "value": {{Encode(clock?.TimeZoneId ?? CustomClock.CurrentTimeZoneId)}}, "style": "compact", "choices": {{BuildTimeZoneChoices(clock?.TimeZoneId)}} },
    { "type": "Input.ChoiceSet", "id": "titleFormat", "label": {{Encode(Resources.timedate_custom_clock_title_format)}}, "value": {{Encode(GetTitleFormat(clock?.TitleFormat))}}, "style": "compact", "choices": {{BuildFormatChoices(GetDisplayFormats(includeNoText: false), GetTitleFormat(clock?.TitleFormat))}} },
    { "type": "Input.ChoiceSet", "id": "subtitleFormat", "label": {{Encode(Resources.timedate_custom_clock_subtitle_format)}}, "value": {{Encode(clock?.SubtitleFormat ?? "REL")}}, "style": "compact", "choices": {{BuildFormatChoices(GetDisplayFormats(), clock?.SubtitleFormat)}} },
    { "type": "Input.ChoiceSet", "id": "copyFormat", "label": {{Encode(Resources.timedate_custom_clock_copy_format)}}, "value": {{Encode(clock?.CopyFormat ?? string.Empty)}}, "style": "compact", "choices": {{BuildFormatChoices(GetDisplayFormats(), clock?.CopyFormat)}} },
    { "type": "TextBlock", "text": {{Encode(Resources.timedate_custom_clock_relative_hint)}}, "wrap": true, "isSubtle": true, "size": "Small" },
    { "type": "Input.Text", "id": "title", "label": {{Encode(Resources.timedate_custom_clock_name)}}, "value": {{Encode(clock?.Title ?? string.Empty)}}, "isRequired": false, "placeholder": {{Encode(Resources.timedate_custom_clock_name_placeholder)}} }
  ],
  "actions": [ { "type": "Action.Submit", "title": {{Encode(Resources.timedate_custom_clock_save)}}, "data": { "title": "title", "timeZoneId": "timeZoneId", "titleFormat": "titleFormat", "subtitleFormat": "subtitleFormat", "copyFormat": "copyFormat" } } ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            if (JsonNode.Parse(payload) is not JsonObject input)
            {
                return CommandResult.KeepOpen();
            }

            _clockManager.Save(new CustomClock
            {
                Id = _existingClock?.Id ?? Guid.NewGuid(),
                Title = input["title"]?.ToString() ?? string.Empty,
                TimeZoneId = input["timeZoneId"]?.ToString() ?? string.Empty,
                TitleFormat = input["titleFormat"]?.ToString() ?? "t",
                SubtitleFormat = input["subtitleFormat"]?.ToString() ?? "REL",
                CopyFormat = input["copyFormat"]?.ToString() ?? string.Empty,
            });

            return CommandResult.GoHome();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            ExtensionHost.LogMessage($"Custom clock was not saved: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }

    private static string Encode(string value) => JsonSerializer.Serialize(value, CustomClockJsonContext.Default.String);

    private IEnumerable<(string Title, string Value)> GetDisplayFormats(bool includeNoText = true) => CustomClockFormatOptions.Get(_settings, includeNoText);

    private static string GetTitleFormat(string? format) => string.IsNullOrEmpty(format) ? "t" : format;

    private static string BuildTimeZoneChoices(string? selectedTimeZoneId)
    {
        var timeZones = new[] { (Resources.timedate_custom_clock_timezone_current, CustomClock.CurrentTimeZoneId) }
            .Concat(TimeZoneInfo.GetSystemTimeZones()
            .Select(timeZone => ($"{timeZone.DisplayName} ({timeZone.Id})", timeZone.Id)));
        return BuildChoices(timeZones, selectedTimeZoneId);
    }

    private static string BuildFormatChoices(IEnumerable<(string Title, string Value)> choices, string? selectedValue) => BuildChoices(choices, selectedValue);

    private static string BuildChoices(IEnumerable<(string Title, string Value)> choices, string? selectedValue)
    {
        var allChoices = choices.ToList();
        if (!string.IsNullOrEmpty(selectedValue) && allChoices.All(choice => choice.Value != selectedValue))
        {
            allChoices.Add((selectedValue, selectedValue));
        }

        return "[" + string.Join(",", allChoices.Select(choice =>
            $$"""{ "title": {{Encode(choice.Title)}}, "value": {{Encode(choice.Value)}} }""")) + "]";
    }
}

internal sealed partial class DeleteCustomClockCommand : InvokableCommand
{
    private readonly CustomClockManager _clockManager;
    private readonly Guid _clockId;

    internal DeleteCustomClockCommand(CustomClockManager clockManager, Guid clockId)
    {
        _clockManager = clockManager;
        _clockId = clockId;
    }

    public override CommandResult Invoke()
    {
        _clockManager.Remove(_clockId);
        return CommandResult.GoBack();
    }
}

#pragma warning restore SA1402 // File may only contain a single type
