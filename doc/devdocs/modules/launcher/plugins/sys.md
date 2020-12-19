# Sys Plugin

As the name suggests, the Sys Plugin is used to directly run Windows system commands that have been entered by the user as a query. This is done by parsing the entry and validating the command, followed by executing it.

* Shutdown
* Restart
* Sign Out
* Lock
* Sleep
* Hibernate
* Empty Recycle Bin

![Image of Sys plugin](/doc/images/launcher/plugins/sys.gif)

## [`Sys`](/src/modules/launcher/Plugins/Microsoft.Plugin.Sys/Main.cs)

* Tries to parse the user input and returns a specific Windows system command by using a [`Result`](/src/modules/launcher/Wox.Plugin/Result.cs) list.

* While parsing, the Sys plugin uses [`FuzzyMatch`](/src/modules/launcher/Wox.Infrastructure/StringMatcher.cs) to get characters matching a result in the list.

### Score

* [`CalculateSearchScore`](/src/modules/launcher/Wox.Infrastructure/StringMatcher.cs) A match found near the beginning of a string is scored more than a match found near the end. A match is scored more if the characters in the patterns are closer to each other, while the score is lower if they are more spread out.
