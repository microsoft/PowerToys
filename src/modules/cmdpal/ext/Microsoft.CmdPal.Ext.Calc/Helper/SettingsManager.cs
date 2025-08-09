// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "calculator";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _trigUnitChoices = new()
    {
        new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_trig_unit_radians, "0"),
        new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_trig_unit_degrees, "1"),
        new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_trig_unit_gradians, "2"),
    };

    private readonly ChoiceSetSetting _trigUnit = new(
        Namespaced(nameof(TrigUnit)),
        Properties.Resources.calculator_settings_trig_unit_mode,
        Properties.Resources.calculator_settings_trig_unit_mode_description,
        _trigUnitChoices);

    private readonly ToggleSetting _inputUseEnNumberFormat = new(
        Namespaced(nameof(InputUseEnglishFormat)),
        Properties.Resources.calculator_settings_in_en_format,
        Properties.Resources.calculator_settings_in_en_format_description,
        false);

    private readonly ToggleSetting _outputUseEnNumberFormat = new(
        Namespaced(nameof(OutputUseEnglishFormat)),
        Properties.Resources.calculator_settings_out_en_format,
        Properties.Resources.calculator_settings_out_en_format_description,
        false);

    private readonly ToggleSetting _closeOnEnter = new(
        Namespaced(nameof(CloseOnEnter)),
        Properties.Resources.calculator_settings_close_on_enter,
        Properties.Resources.calculator_settings_close_on_enter_description,
        true);

    public CalculateEngine.TrigMode TrigUnit
    {
        get
        {
            if (_trigUnit.Value == null || string.IsNullOrEmpty(_trigUnit.Value))
            {
                return CalculateEngine.TrigMode.Radians;
            }

            var success = int.TryParse(_trigUnit.Value, out var result);

            if (!success)
            {
                return CalculateEngine.TrigMode.Radians;
            }

            switch (result)
            {
                case 0:
                    return CalculateEngine.TrigMode.Radians;
                case 1:
                    return CalculateEngine.TrigMode.Degrees;
                case 2:
                    return CalculateEngine.TrigMode.Gradians;
                default:
                    return CalculateEngine.TrigMode.Radians;
            }
        }
    }

    public bool InputUseEnglishFormat => _inputUseEnNumberFormat.Value;

    public bool OutputUseEnglishFormat => _outputUseEnNumberFormat.Value;

    public bool CloseOnEnter => _closeOnEnter.Value;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_trigUnit);
        Settings.Add(_inputUseEnNumberFormat);
        Settings.Add(_outputUseEnNumberFormat);
        Settings.Add(_closeOnEnter);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
