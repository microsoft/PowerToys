// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

public class SettingsManager : JsonSettingsManager, IDockClockSettings
{
    // Line break character used in WinUI3 TextBox and TextBlock.
    private const char TEXTBOXNEWLINE = '\r';

    private const string CUSTOMFORMATPLACEHOLDER = "MyFormat=dd-MMM-yyyy\rMySecondFormat=dddd (Da\\y nu\\mber: DOW)\rMyUtcFormat=UTC:hh:mm:ss";

    private static readonly string _namespace = "timeDate";

    private string _dockClockTitleFormat = "t";
    private string _dockClockSubtitleFormat = "d";
    private string _dockClockCopyFormat = string.Empty;

    internal event EventHandler? DockClockFormatsChanged;

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _firstWeekOfYearChoices = new()
    {
        new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_Setting_UseSystemSetting, "-1"),
        new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_FirstDay, "0"),
        new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_FirstFullWeek, "1"),
        new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_FirstFourDayWeek, "2"),
    };

    private static readonly List<ChoiceSetSetting.Choice> _firstDayOfWeekChoices = GetFirstDayOfWeekChoices();

    private static readonly List<ChoiceSetSetting.Choice> _dockClockClickActionChoices = new()
    {
        new ChoiceSetSetting.Choice(Resources.timedate_dock_clock_click_action_default, "default"),
        new ChoiceSetSetting.Choice(Resources.timedate_dock_clock_click_action_notification_center, "notificationCenter"),
        new ChoiceSetSetting.Choice(Resources.timedate_dock_clock_click_action_all_clocks, "allClocks"),
    };

    private static List<ChoiceSetSetting.Choice> GetFirstDayOfWeekChoices()
    {
        // List (Sorted for first day is Sunday)
        var list = new List<ChoiceSetSetting.Choice>
            {
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_Setting_UseSystemSetting, "-1"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Sunday, "0"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Monday, "1"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Tuesday, "2"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Wednesday, "3"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Thursday, "4"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Friday, "5"),
                new ChoiceSetSetting.Choice(Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek_Saturday, "6"),
            };

        // Order Rules
        var orderRuleSaturday = new string[] { "-1", "6", "0", "1", "2", "3", "4", "5" };
        var orderRuleMonday = new string[] { "-1", "1", "2", "3", "4", "5", "6", "0" };

        switch (DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek)
        {
            case DayOfWeek.Saturday:
                return list.OrderBy(x => Array.IndexOf(orderRuleSaturday, x.Value)).ToList();
            case DayOfWeek.Monday:
                return list.OrderBy(x => Array.IndexOf(orderRuleMonday, x.Value)).ToList();
            default:
                // DayOfWeek.Sunday
                return list;
        }
    }

    private readonly ChoiceSetSetting _firstWeekOfYear = new(
        Namespaced(nameof(FirstWeekOfYear)),
        Resources.Microsoft_plugin_timedate_SettingFirstWeekRule,
        Resources.Microsoft_plugin_timedate_SettingFirstWeekRule_Description,
        _firstWeekOfYearChoices);

    private readonly ChoiceSetSetting _firstDayOfWeek = new(
        Namespaced(nameof(FirstDayOfWeek)),
        Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek,
        Resources.Microsoft_plugin_timedate_SettingFirstDayOfWeek,
        _firstDayOfWeekChoices);

    private readonly ToggleSetting _timeWithSeconds = new(
        Namespaced(nameof(TimeWithSecond)),
        Resources.Microsoft_plugin_timedate_SettingTimeWithSeconds,
        Resources.Microsoft_plugin_timedate_SettingTimeWithSeconds_Description,
        false); // TODO -- double check default value

    private readonly ToggleSetting _dateWithWeekday = new(
        Namespaced(nameof(DateWithWeekday)),
        Resources.Microsoft_plugin_timedate_SettingDateWithWeekday,
        Resources.Microsoft_plugin_timedate_SettingDateWithWeekday_Description,
        false); // TODO -- double check default value

    private readonly ChoiceSetSetting _dockClockClickAction = new(
        Namespaced(nameof(DockClockClickAction)),
        Resources.timedate_dock_clock_click_action,
        Resources.timedate_dock_clock_click_action_description,
        _dockClockClickActionChoices);

    private readonly TextSetting _customFormats = new(
        Namespaced(nameof(CustomFormats)),
        Resources.Microsoft_plugin_timedate_Setting_CustomFormats,
        Resources.Microsoft_plugin_timedate_Setting_CustomFormats + TEXTBOXNEWLINE + string.Format(CultureInfo.CurrentCulture, Resources.Microsoft_plugin_timedate_Setting_CustomFormatsDescription.ToString(), "DOW", "DIM", "WOM", "WOY", "EAB", "WFT", "UXT", "UMS", "OAD", "EXC", "EXF", "UTC:"),
        string.Empty);

    public int FirstWeekOfYear
    {
        get
        {
            if (_firstWeekOfYear.Value is null || string.IsNullOrEmpty(_firstWeekOfYear.Value))
            {
                return -1;
            }

            var success = int.TryParse(_firstWeekOfYear.Value, out var result);

            if (!success)
            {
                return -1;
            }

            return result;
        }
    }

    public int FirstDayOfWeek
    {
        get
        {
            if (_firstDayOfWeek.Value is null || string.IsNullOrEmpty(_firstDayOfWeek.Value))
            {
                return -1;
            }

            var success = int.TryParse(_firstDayOfWeek.Value, out var result);

            if (!success)
            {
                return -1;
            }

            return result;
        }
    }

    public bool TimeWithSecond => _timeWithSeconds.Value;

    public string DockClockTitleFormat => _dockClockTitleFormat;

    public string DockClockSubtitleFormat => _dockClockSubtitleFormat;

    public string DockClockCopyFormat => _dockClockCopyFormat;

    public string DockClockClickAction => _dockClockClickAction.Value ?? "default";

    public bool DateWithWeekday => _dateWithWeekday.Value;

    public List<string> CustomFormats => (_customFormats.Value ?? string.Empty).Split(TEXTBOXNEWLINE).ToList();

    public void SetDockClockFormats(string titleFormat, string subtitleFormat, string copyFormat)
    {
        ValidateDockClockFormat(titleFormat, nameof(titleFormat));
        ValidateDockClockFormat(subtitleFormat, nameof(subtitleFormat));
        ValidateDockClockFormat(copyFormat, nameof(copyFormat));

        _dockClockTitleFormat = titleFormat;
        _dockClockSubtitleFormat = subtitleFormat;
        _dockClockCopyFormat = copyFormat;
        SaveSettings();
        DockClockFormatsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void LoadAdditionalSettings(JsonObject settings)
    {
        _dockClockTitleFormat = LoadDockClockFormat(settings, nameof(DockClockTitleFormat), "t");
        _dockClockSubtitleFormat = LoadDockClockFormat(settings, nameof(DockClockSubtitleFormat), "d");
        _dockClockCopyFormat = LoadDockClockFormat(settings, nameof(DockClockCopyFormat), string.Empty);
    }

    protected override void SaveAdditionalSettings(JsonObject settings)
    {
        settings[Namespaced(nameof(DockClockTitleFormat))] = _dockClockTitleFormat;
        settings[Namespaced(nameof(DockClockSubtitleFormat))] = _dockClockSubtitleFormat;
        settings[Namespaced(nameof(DockClockCopyFormat))] = _dockClockCopyFormat;
    }

    private static string LoadDockClockFormat(JsonObject settings, string propertyName, string defaultFormat)
    {
        try
        {
            var format = settings[Namespaced(propertyName)]?.GetValue<string>() ?? defaultFormat;
            _ = CustomClockDisplay.CompileFormat(format);
            return format;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return defaultFormat;
        }
    }

    private static void ValidateDockClockFormat(string format, string parameterName)
    {
        try
        {
            _ = CustomClockDisplay.CompileFormat(format);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The dock clock contains an invalid format.", parameterName, ex);
        }
    }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    public SettingsManager()
        : this(SettingsJsonPath())
    {
    }

    internal SettingsManager(string filePath)
    {
        FilePath = filePath;

        Settings.Add(_timeWithSeconds);
        Settings.Add(_dateWithWeekday);
        Settings.Add(_dockClockClickAction);
        Settings.Add(_firstWeekOfYear);
        Settings.Add(_firstDayOfWeek);

        _customFormats.Multiline = true;
        _customFormats.Placeholder = CUSTOMFORMATPLACEHOLDER;
        Settings.Add(_customFormats);

        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
