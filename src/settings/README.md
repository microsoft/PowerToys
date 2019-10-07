# PowerToys Settings project

## Introduction

This path contains the WebView project for editing the PowerToys settings.

The html portion of the project that is shown in the WebView is contained in `settings-html`.
Instructions on how build a new version and update this project are in the [Web project for the Settings UI](../settings-web).

While developing, it's possible to connect the WebView to the development server running in localhost by setting the `_DEBUG_WITH_LOCALHOST` flag to `1` and following the instructions near it in `./main.cpp`.

## Code Organization

#### [main.cpp](./main.cpp)
Contains the main executable code, initializing and managing the Window containing the WebView and communication with the main PowerToys executable.

#### [StreamURIResolverFromFile.cpp](./StreamURIResolverFromFile.cpp)
Defines a class implementing `IUriToStreamResolver`. Allows the WebView to navigate to filesystem files in this Win32 project.

#### [settings-html/](./settings-html/)
Contains the assets file from building the [Web project for the Settings UI](../settings-web). It will be loaded by the WebView.
