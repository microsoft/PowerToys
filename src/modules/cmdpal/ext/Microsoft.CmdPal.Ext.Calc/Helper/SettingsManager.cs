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

    private static readonly List<ChoiceSetCardSetting.Entry> _trigUnitChoices = new()
    {
        new ChoiceSetCardSetting.Entry(Properties.Resources.calculator_settings_trig_unit_radians, "0"),
        new ChoiceSetCardSetting.Entry(Properties.Resources.calculator_settings_trig_unit_degrees, "1"),
        new ChoiceSetCardSetting.Entry(Properties.Resources.calculator_settings_trig_unit_gradians, "2"),
    };

    private readonly ChoiceSetCardSetting _trigUnit = new(
        Namespaced(nameof(TrigUnit)),
        Properties.Resources.calculator_settings_trig_unit_mode,
        Properties.Resources.calculator_settings_trig_unit_mode_description,
        _trigUnitChoices);

    private readonly ToggleCardSetting _inputUseEnNumberFormat = new(
        Namespaced(nameof(InputUseEnglishFormat)),
        Properties.Resources.calculator_settings_in_en_format,
        Properties.Resources.calculator_settings_in_en_format_description,
        false);

    private readonly ToggleCardSetting _outputUseEnNumberFormat = new(
        Namespaced(nameof(OutputUseEnglishFormat)),
        Properties.Resources.calculator_settings_out_en_format,
        Properties.Resources.calculator_settings_out_en_format_description,
        false);

    private readonly ToggleCardSetting _closeOnEnter = new(
        Namespaced(nameof(CloseOnEnter)),
        Properties.Resources.calculator_settings_close_on_enter,
        Properties.Resources.calculator_settings_close_on_enter_description,
        true);

    private readonly ToggleCardSetting _copyResultToSearchBarIfQueryEndsWithEqualSign = new(
        Namespaced(nameof(CopyResultToSearchBarIfQueryEndsWithEqualSign)),
        Properties.Resources.calculator_settings_copy_result_to_search_bar,
        Properties.Resources.calculator_settings_copy_result_to_search_bar_description,
        false);

    private readonly ToggleCardSetting _autoFixQuery = new(
        Namespaced(nameof(AutoFixQuery)),
        Properties.Resources.calculator_settings_auto_fix_query,
        Properties.Resources.calculator_settings_auto_fix_query_description,
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

    public bool CopyResultToSearchBarIfQueryEndsWithEqualSign => _copyResultToSearchBarIfQueryEndsWithEqualSign.Value;

    public bool AutoFixQuery => _autoFixQuery.Value;

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
        Settings.Add(_copyResultToSearchBarIfQueryEndsWithEqualSign);
        Settings.Add(_autoFixQuery);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
