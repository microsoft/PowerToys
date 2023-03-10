# Calculator Plugin
The Calculator plugin as the name suggests is used to perform calculations on the user entered query.

![Image of Calculator plugin](/doc/images/launcher/plugins/calculator.png)

## Optional plugin settings

* We have the following settings that the user can configure to change the behavior of the plugin:

	| Key | Default value | Name | Description |
	|--------------|-----------|------------|------------|
	| `InputUseEnglishFormat` | `false` | Use English (United States) number format for input | Ignores your system setting and expects numbers in the format '1,000.50' |
	| `OutputUseEnglishFormat` | `false` | Use English (United States) number format for output | Ignores your system setting and returns numbers in the format '1000.50' |

* The optional plugin settings are implemented via the [`ISettingProvider`](/src/modules/launcher/Wox.Plugin/ISettingProvider.cs) interface from `Wox.Plugin` project. All available settings for the plugin are defined in the [`Main`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/Main.cs) class of the plugin.

## Technical details

### [`BracketHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/BracketHelper.cs)
- This helper validates the bracket usage in the input string.

### [`CalculateHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateHelper.cs)
- The [`CalculateHelper.cs`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateHelper.cs) class checks to see if the user entered query is a valid input to the calculator and only if the input is valid does it perform the operation.
  - It does so by matching the user query to a valid regex.
- This class also handles some human multiplication expression like `2(1+2)` and `(2+3)(3+4)` in order to be computed by `Mages` lib.
  - It does so by matching some regex and inserting `'*'` where appropriate, e.g: `2(1+2) -> 2 * (1+2)`
  - It takes into account the combination of numbers (`num`), constants (`const`), functions (`func`) and expressions in parentheses (`(exp)`).
  - The blank spaces between them are also considered.
  - Some combinations were not handled as they are not common such as `'const num'` or `'func const'`

### [`CalculateEngine`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs)
- The main computation is done in the [`CalculateEngine.cs`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs) file using the `Mages` library.

```csharp
var result = CalculateEngine.Interpret(query.Search, CultureInfo.CurrentUICulture);
```

### [`CalculateResult`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateResult.cs)
- The class which encapsulates the result of the computation.
- It comprises of the `Result` and `RoundedResult` properties.

### [`ErrorHandler`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/ErrorHandler.cs)
- The class which encapsulates the code to log errors and format the user message.
- It returns an error result if the user searches with the activation command. This error result is shown to the user.

### Score
The score of each result from the calculator plugin is `300`.


## [Unit Tests](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests)
We have a [Unit Test project](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests) that executes various test to ensure that the plugin works as expected.

### [`BracketHelperTests`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/BracketHelperTests.cs)
- The [`BracketHelperTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/BracketHelperTests.cs) class contains tests to validate that brackets are handled correctly.

### [`ExtendedCalculatorParserTests`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/ExtendedCalculatorParserTests.cs)
- The [`ExtendedCalculatorParserTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/ExtendedCalculatorParserTests.cs) class contains tests to validate that the input is parsed correctly and the result is correct.

### [`NumberTranslatorTests`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/NumberTranslatorTests.cs)
- The [`NumberTranslatorTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/NumberTranslatorTests.cs) class contains tests to validate that each number is converted correctly based on the defined locals.

### [`QueryTests`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/QueryTests.cs)
- The [`QueryTests.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests/QueryTests.cs) class contains tests to validate that the user gets the correct results when searching.

