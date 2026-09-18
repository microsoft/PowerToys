// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleWidgetContent : WidgetContent
{
    public const string WidgetId = "com.microsoft.cmdpal.samples.counter";

    private readonly FormContent _content;

    private int _count;

    public SampleWidgetContent(string instanceId)
    {
        Id = WidgetId;
        Title = "Sample counter";
        Description = "A sample interactive widget provided by a Command Palette extension.";
        Icon = new IconInfo("\uE9D9");
        SupportedSizes = [WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large];
        AllowMultiple = true;
        _content = new SampleWidgetForm(this)
        {
            TemplateJson = """
            {
                "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                "type": "AdaptiveCard",
                "version": "1.5",
                "body": [
                    {
                        "type": "TextBlock",
                        "text": "Sample counter",
                        "size": "Large",
                        "weight": "Bolder"
                    },
                    {
                        "type": "TextBlock",
                        "text": "Count: ${count}",
                        "wrap": true
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Execute",
                        "title": "Increment",
                        "verb": "increment"
                    }
                ]
            }
            """,
        };
        Content = _content;
        UpdateData();
    }

    private ICommandResult SubmitForm(string inputs, string data)
    {
        using var context = JsonDocument.Parse(data);
        if (context.RootElement.TryGetProperty("verb", out var verb) && verb.GetString() == "increment")
        {
            _count++;
            UpdateData();
        }

        return CommandResult.KeepOpen();
    }

    private void UpdateData()
    {
        _content.DataJson = $$"""
            {
                "count": {{_count.ToString(CultureInfo.InvariantCulture)}}
            }
            """;
    }

    private sealed partial class SampleWidgetForm(SampleWidgetContent owner) : FormContent
    {
        public override ICommandResult SubmitForm(string inputs, string data) => owner.SubmitForm(inputs, data);
    }
}
