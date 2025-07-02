// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleSettingsPage : ContentPage
{
    private readonly Settings _settings = new();

    private readonly List<ChoiceSetSetting.Choice> _choices = new()
    {
        new ChoiceSetSetting.Choice("The first choice in the list is the default choice", "0"),
        new ChoiceSetSetting.Choice("Choices have titles and values", "1"),
        new ChoiceSetSetting.Choice("Title", "Value"),
        new ChoiceSetSetting.Choice("The options are endless", "3"),
        new ChoiceSetSetting.Choice("So many choices", "4"),
    };

    public override IContent[] GetContent()
    {
        var s = _settings.ToContent();
        return s;
    }

    public SampleSettingsPage()
    {
        Name = "Sample Settings";
        Icon = new IconInfo(string.Empty);
        _settings.Add(new ToggleSetting("onOff", true)
        {
            Label = "This is a toggle",
            Description = "It produces a simple checkbox",
        });
        _settings.Add(new TextSetting("someText", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
        });
        _settings.Add(new ChoiceSetSetting("choiceSetExample", _choices)
        {
            Label = "It also has a label",
            Description = "Describe your choice set setting here",
        });

        _settings.SettingsChanged += SettingsChanged;
    }

    private void SettingsChanged(object sender, Settings args)
    {
        /* Do something with the new settings here */
        var onOff = _settings.GetSetting<bool>("onOff");
        ExtensionHost.LogMessage(new LogMessage() { Message = $"SampleSettingsPage: Changed the value of onOff to {onOff}" });
    }
}
