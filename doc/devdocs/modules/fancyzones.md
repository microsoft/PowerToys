# FancyZones UI tests

UI tests are implemented using [Windows Application Driver](https://github.com/microsoft/WinAppDriver).

## Before running tests

  - Install Windows Application Driver v1.2.1 from https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1. 
  - Enable Developer Mode in Windows settings

## Running tests
  
  - Exit PowerToys if it's running
  - Run WinAppDriver.exe from the installation directory. Skip this step if installed in the default directory (`C:\Program Files (x86)\Windows Application Driver`); in this case, it'll be launched automatically during tests.
  - Open `PowerToys.sln` in Visual Studio and build the solution.
  - Run tests in the Test Explorer (`Test > Test Explorer` or `Ctrl+E, T`). 

>Note: notifications or other application windows, that are shown above the window under test, can disrupt the testing process.


## Extra tools and information

**Test samples**: https://github.com/microsoft/WinAppDriver/tree/master/Samples

While working on tests, you may need a tool that helps you to view the element's accessibility data, e.g. for finding the button to click. For this purpose, you could use [AccessibilityInsights](https://accessibilityinsights.io/docs/windows/overview) or [WinAppDriver UI Recorder](https://github.com/microsoft/WinAppDriver/wiki/WinAppDriver-UI-Recorder).

>Note: close helper tools while running tests. Overlapping windows can affect test results.