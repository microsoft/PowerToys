// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleSettingsPage : ContentPage
{
    private readonly Settings _settings = new();

    private readonly List<ChoiceSetCardSetting.Entry> _choices =
    [
        new("The first choice in the list is the default choice", "0"),
        new("Choices have titles and values", "1"),
        new("Title", "Value"),
        new("The options are endless", "3"),
        new("So many choices", "4")
    ];

    private readonly List<ChoiceSetSetting.Choice> _choicesOriginal =
    [
        new("The first choice in the list is the default choice", "0"),
        new("Choices have titles and values", "1"),
        new("Title", "Value"),
        new("The options are endless", "3"),
        new("So many choices", "4")
    ];

    public override IContent[] GetContent()
    {
        var s = _settings.ToContent();
        return s;
    }

    public SampleSettingsPage()
    {
        Name = "Sample Settings";
        Icon = new IconInfo(string.Empty);

        // Settings cards (optional)
        _settings.Add(new ToggleCardSetting("onOff", true)
        {
            Label = "This is a toggle",
            Description = "It produces a simple switch",
        });

        _settings.Add(new CheckBoxCardSettings("onOffCheckbox", true)
        {
            Label = "This is a checkbox",
            Description = "It produces a simple checkbox",
        });

        _settings.Add(new TextCardSetting("someText", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
        });

        _settings.Add(new TextCardSetting("areaText", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
            Multiline = true,
        });

        _settings.Add(new ChoiceSetCardSetting("choiceSetExample", _choices)
        {
            Label = "It also has a label",
            Description = "Describe your choice set setting here",
        });

        // Settings cards (required)
        _settings.Add(new ToggleCardSetting("onOffWithError", true)
        {
            Label = "This is a toggle",
            Description = "It produces a simple checkbox",
            ErrorMessage = "Error message for the checkbox",
            IsRequired = true,
        });

        _settings.Add(new CheckBoxCardSettings("onOffCheckboxWithError", true)
        {
            Label = "This is a checkbox",
            Description = "It produces a simple checkbox",
            ErrorMessage = "Error message for the checkbox",
            IsRequired = true,
        });

        _settings.Add(new ChoiceSetCardSetting("choiceSetExampleWithError", _choices)
        {
            Label = "It also has a label",
            Description = "Describe your choice set setting here",
            ErrorMessage = "Error message for the choice set",
            IsRequired = true,
        });

        _settings.Add(new TextCardSetting("someTextWithError", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
            ErrorMessage = "Error message for the text box",
            IsRequired = true,
        });

        _settings.Add(new TextCardSetting("areaTextWithError", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
            IsRequired = true,
            ErrorMessage = "Error message for the text area",
            Multiline = true,
        });

        // Original settings (optional)
        _settings.Add(new ToggleSetting("onOff_original", true)
        {
            Label = "This is a toggle",
            Description = "It produces a simple switch",
        });

        _settings.Add(new TextSetting("someText_original", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
        });

        _settings.Add(new TextSetting("areaText_original", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
            Multiline = true,
        });

        _settings.Add(new ChoiceSetSetting("choiceSetExample_original", _choicesOriginal)
        {
            Label = "It also has a label",
            Description = "Describe your choice set setting here",
        });

        // Original settings (required)
        _settings.Add(new ToggleSetting("onOffWithError_original", true)
        {
            Label = "This is a toggle",
            Description = "It produces a simple checkbox",
            ErrorMessage = "Error message for the checkbox",
            IsRequired = true,
        });

        _settings.Add(new ChoiceSetSetting("choiceSetExampleWithError_original", _choicesOriginal)
        {
            Label = "It also has a label",
            Description = "Describe your choice set setting here",
            ErrorMessage = "Error message for the choice set",
            IsRequired = true,
        });

        _settings.Add(new TextSetting("someTextWithError_original", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
            ErrorMessage = "Error message for the text box",
            IsRequired = true,
        });

        _settings.Add(new TextSetting("areaTextWithError_original", "initial value")
        {
            Label = "This is a text box",
            Description = "For some string of text",
            IsRequired = true,
            ErrorMessage = "Error message for the text area",
            Multiline = true,
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
