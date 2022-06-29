# Translate Plugin
This plugin simply translates the text into the language you need

![Image of Translate plugin](/doc/images/launcher/plugins/translate.png)

The code itself is very simple, basically just a call Translate functions via the https://github.com/d4n3436/GTranslate library.

```csharp
var result = await _translator.TranslateAsync("Hello world", "ru");
Console.WriteLine(result.Translation) // Привет мир
```
