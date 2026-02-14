// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "calculator";
    private const int HistoryCapacity = 100;

    public event EventHandler HistoryChanged
    {
        add => _history.Changed += value;
        remove => _history.Changed -= value;
    }

    public event EventHandler SettingsChanged;

    private readonly HistoryStore _history;

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

    private readonly ToggleSetting _replaceQueryOnEnter = new(
        Namespaced(nameof(ReplaceQueryOnEnter)),
        Properties.Resources.calculator_settings_replace_query_on_enter,
        Properties.Resources.calculator_settings_replace_query_on_enter_description,
        true);

    private readonly ToggleSetting _copyResultToSearchBarIfQueryEndsWithEqualSign = new(
        Namespaced(nameof(CopyResultToSearchBarIfQueryEndsWithEqualSign)),
        Properties.Resources.calculator_settings_copy_result_to_search_bar,
        Properties.Resources.calculator_settings_copy_result_to_search_bar_description,
        false);

    private readonly ToggleSetting _autoFixQuery = new(
        Namespaced(nameof(AutoFixQuery)),
        Properties.Resources.calculator_settings_auto_fix_query,
        Properties.Resources.calculator_settings_auto_fix_query_description,
        true);

    private readonly ToggleSetting _saveFallbackResultsToHistory = new(
        Namespaced(nameof(SaveFallbackResultsToHistory)),
        Properties.Resources.calculator_settings_fallback_history,
        Properties.Resources.calculator_settings_fallback_history_description,
        false);

    private readonly ToggleSetting _confirmDelete = new(
        Namespaced(nameof(DeleteHistoryRequiresConfirmation)),
        Properties.Resources.calculator_settings_confirm_delete_title,
        Properties.Resources.calculator_settings_confirm_delete_description,
        true);

    private readonly ChoiceSetSetting _primaryAction = new(
        Namespaced(nameof(PrimaryAction)),
        Properties.Resources.calculator_settings_primary_action_title,
        Properties.Resources.calculator_settings_primary_action_description,
        [
            new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_primary_action_default, PrimaryAction.Default.ToString("G")),
            new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_primary_action_copy, PrimaryAction.Copy.ToString("G")),
            new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_primary_action_paste, PrimaryAction.Paste.ToString("G")),
        ]);

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

    public bool ReplaceQueryOnEnter => _replaceQueryOnEnter.Value;

    public bool CopyResultToSearchBarIfQueryEndsWithEqualSign => _copyResultToSearchBarIfQueryEndsWithEqualSign.Value;

    public bool AutoFixQuery => _autoFixQuery.Value;

    public bool SaveFallbackResultsToHistory => _saveFallbackResultsToHistory.Value;

    public bool DeleteHistoryRequiresConfirmation => _confirmDelete.Value;

    public PrimaryAction PrimaryAction => Enum.TryParse<PrimaryAction>(_primaryAction.Value, out var action) ? action : PrimaryAction.Default;

    public IReadOnlyList<HistoryItem> HistoryItems => _history.HistoryItems;

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
        Settings.Add(_saveFallbackResultsToHistory);
        Settings.Add(_confirmDelete);
        Settings.Add(_primaryAction);

        // Load settings from file upon initialization
        LoadSettings();

        _history = new HistoryStore(HistoryStateJsonPath(), HistoryCapacity);

        Settings.SettingsChanged += (s, a) =>
        {
            this.SaveSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private static string HistoryStateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "calculator_history.json");
    }

    public void AddHistoryItem(HistoryItem historyItem)
    {
        try
        {
            _history.Add(historyItem);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to add item to the calculator history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    public void RemoveHistoryItem(Guid historyItemId)
    {
        try
        {
            _history.Remove(historyItemId);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to remove item from the calculator history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    public void ClearHistory()
    {
        try
        {
            _history.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to clear calculator history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }
}
