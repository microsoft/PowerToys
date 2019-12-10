#### [main.cpp](./main.cpp)
Contains the main executable code, initializing and managing the Window containing the WebView and communication with the main PowerToys executable.

#### [StreamURIResolverFromFile.cpp](./StreamURIResolverFromFile.cpp)
Defines a class implementing `IUriToStreamResolver`. Allows the WebView to navigate to filesystem files in this Win32 project.

#### [settings-html/](./settings-html/)
Contains the assets file from building the [Web project for the Settings UI](../settings-web). It will be loaded by the WebView.
