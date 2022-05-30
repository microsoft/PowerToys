# OneNote Plugin
The OneNote plugin searches your locally synced OneNote notebooks based on the user query.

![Image of OneNote plugin](/doc/images/launcher/plugins/OneNote.png)

### [`CalculateHelper`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/CalculateHelper.cs)
- The [`CalculateHelper.cs`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateHelper.cs) class checks to see if the user entered query is a valid input to the calculator and only if the input is valid does it perform the operation.
- It does so by matching the user query to a valid regex.

### [`CalculateEngine`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs)
- The main computation is done in the [`CalculateEngine.cs`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs) file using the `Mages` library.

```csharp
var result = CalculateEngine.Interpret(query.Search, CultureInfo.CurrentUICulture);
```

### [`CalculateResult`](src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.Calculator/CalculateResult.cs)
- The class which encapsulates the result of the computation.
- It comprises of the `Result` and `RoundedResult` properties.

### Score
The score of each result from the calculator plugin is `300`.

