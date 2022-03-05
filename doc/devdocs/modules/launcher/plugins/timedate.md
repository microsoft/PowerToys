# 'Time and Date' plugin
The 'Time and Date' plugin shows the date and time in different formats. For the date and time formats the plugin uses the culture setting in Windows, if the format is not commonly defined. The user can search for the system date/time or a custom date/time. The value of each result can be copied to clipboard.

![Image of Window Walker plugin](/doc/images/launcher/plugins/timedate.png)

![Image of Window Walker plugin](/doc/images/launcher/plugins/timedate2.png)


**Query examples:**
- Format: `time`
- Date/time: `10:30 AM`
- Format and date/time: `Week number::10/10/2022`


## Formats

The following formats are currently available:

| Format | Example (Based on default settings) | As result | As input |
|--------------|-----------|------------|------------|
| Time | 5:10 PM | x | x |
| Date | 3/5/2022 | x | x |
| Now | 3/5/2022 5:10 PM | x | x |
| Time UTC | 4:10 PM | x | x |
| Now UTC | 3/5/2022 4:10 PM | x | x |
| Unix Timestamp | 1646496622 | x | x |
| Day (Week day) | Saturday | x | |
| Day of the week | 6 | x | |
| Day of the month | 5 | x | |
| Day of the year | 64 | x | |
| Week of the month | 1 | x | |
| Week of the year (Calendar week, Week number) | 10 | x | |
| Month | March | x | |
| Month of the year | 3 | x | |
| Year | 2022 | x | |
| Windows file time (Current date and time as Int64 number) | 637820976123938199 | x | x |
| Universal time format: YYYY-MM-DD hh:mm:ss| 2022-03-05 16:20:12Z | x | x |
| ISO 8601 | 2022-03-05T17:23:04 | x | x |
| ISO 8601 UTC | 2022-03-05T16:23:04 | x | x |
| ISO 8601 with time zone | 2022-03-05T17:23:04+01:00 | x | x |
| ISO 8601 UTC with time zone | 2022-03-05T16:23:04Z | x | x |
| RFC1123 | Sat, 05 Mar 2022 16:23:04 GMT | x | x |

### Remarks

- The following formats requires a prefix in the query:
   - Unix Timestamp: `u`
   - Windows file time: `ft`
- On invalid number inputs we shaw a warning that tells the user which prefixes are allowed/required.

## Adding new formats
- To add a new formats you have to add them in the method `GetAvailableResults()` of the [`ResultHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/ResultHelper.cs) class.
	- Please add the new formats in the second range. The first one is reserved for the following three main formats (Time, Date, Now).
- After adding the new formats you have to update the Unit Tests!

## Optional plugin settings
- The optional plugin settings are implemented via the [`ISettingProvider`](/src/modules/launcher/Wox.Plugin/ISettingProvider.cs) interface from `Wox.Plugin` project.
- All available settings for the plugin are defined in the [`TimeDateSettings`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/TimeDateSettings.cs) class of the plugin. The settings can be accessed everywhere in the plugin code via the static class instance `TimeDateSettings.Instance`.
- We have the following settings that the user can configure to change the behavior of the plugin:

	| Key | Default value | Name/Description |
	|--------------|-----------|------------|
	| `OnlyDateTimeNowGlobal` | `true` | Show only 'Time', 'Date', and 'Now' result on global queries |
	| `TimeWithSeconds` | `false` | Show time with seconds (Applies to 'Time' and 'Now' result) |
	| `DateWithWeekday` | `false` | Show date with weekday and name of month (Applies to 'Date' and 'Now' result) |
	| `HideNumberMessageOnGlobalQuery` | `false` | Hide 'Invalid number input' error message on global queries |
	

## Technical details

### [`AvailableResult.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/AvailableResult.cs)
- Each instance of the [`AvailableResult`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/AvailableResult.cs) class represents a time/date result/format that the user can search for.
- The results/formats are defined in the method `GetAvailableResults()` of the [`ResultHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/ResultHelper.cs) class.

### [`ResultHelper.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/ResultHelper.cs)
- The [`ResultHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/ResultHelper.cs) class contains a method to create the list of all available formats/results.
- And it contains the method that copies the result values to the clipboard.

### [`TimeAndDateHelper.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/TimeAndDateHelper.cs)
- The [`TimeAndDateHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/TimeAndDateHelper.cs) class contains methods to format/convert date and time formats/strings.

### [`TimeDateSettings.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/TimeDateSettings.cs)
- The [`TimeDateSettings`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/TimeDateSettings.cs) class provides access to all optional plugin settings.
- The class has a static property called `Instance` that holds an instance of the class itself. This allows us to access the settings from everywhere in the plugin code without having additional parameters in our methods.

### [`SearchController.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/SearchController.cs)
- The [`SearchController`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate/Components/SearchController.cs) encapsulates the functions needed to search and find matches.

### Score
The plugin uses `FuzzyMatching` to get the matching formats, if the user searches for a specific format. The score is set based on the `FuzzySearch` result.


## [Unit Tests](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests)
We have a [Unit Test project](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests) that executes various test to ensure that the plugin works as expected.

### [`DateTimeResultTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/DateTimeResultTests.cs)
- The [`DateTimeResultTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/DateTimeResultTests.cs) class contains tests to validate that the time and date values are correctly formatted/converted.
- That we can execute the tests at any time on any machine, we use a specified date/time value and set the thread culture always to `en-us` while executing the tests.

### [`ImageTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/ImageTests.cs)
- The [`ImageTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/ImageTests.cs) class contains tests to validate that each result shows the expected and correct image.

### [`PluginSettingsTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/PluginSettingsTests.cs)
- The [`PluginSettingsTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/PluginSettingsTests.cs) class contains tests to validate that all settings exist and that they have the correct default values.

### [`QueryTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/QueryTests.cs)
- The [`QueryTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/QueryTests.cs) class contains tests to validate that the user gets the correct results when searching.

### [`StringParserTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/StringParserTests.cs)
- The [`StringParserTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests/StringParserTests.cs) class contains tests to validate that the typed string gets converted correctly into a `DateTime` object.