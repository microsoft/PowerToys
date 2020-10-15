# URI Plugin
The URI Plugin, as the name suggests is used to dierctly run the URI that has been entered by the user as a query. This is done by parsing the entry and validating thr URI, followed by executing it.

### [`URI Parser`](src/modules/launcher/Plugins/Microsoft.Plugin.Uri/UriHelper/ExtendedUriParser.cs)
- he [`ExtendedUriParser.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Uri/UriHelper/ExtendedUriParser.cs) file tries to parse the user input and returns a `System.Uri` result  by using the `UriBuilder`. 
- It also captures other cases which the UriBuilder does not handle such as when the input ends with a `:`, `.` or `:/`.

### [`URI Resolver`](src/modules/launcher/Plugins/Microsoft.Plugin.Uri/UriHelper/UriResolver.cs)
- The [`UriResolver.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Uri/UriHelper/UriResolver.cs) file returns true for Valid hosts.
- Currently there is no additional logic for filtering out invalid hosts and it always returns true for a valid Uri that was created by parsing the user query.

### Default Browser Icon
- The icon for each uri result is that of the default browser set by the user.
- These details are obtained from the user registry and updated each time the theme of PT Run is changed.

### Score
- All uri plugin results have a score of 0 which indicates that they would show up after each of the other plugins, other than the indexer plugin which also has a score of 0.
