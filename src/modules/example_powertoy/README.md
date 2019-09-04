# Example PowerToy

# Introduction
This PowerToy serves as a sample to show how to implement the [PowerToys interface](/src/modules/interface/) when creating a PowerToy. It also showcases the currently implemented settings.

# Options
This module has a setting to serve as an example for each of the currently implemented settings property:
  - BoolToggle property
  - IntSpinner property
  - String property
  - ColorPicker property
  - CustomAction property

![Image of the Options](/doc/images/example_powertoy/settings.png)

# Code organization

#### [`dllmain.cpp`](./dllmain.cpp)
Contains DLL boilerplate code and implementation of the [PowerToys interface](/src/modules/interface/).

#### [`trace.cpp`](./trace.cpp)
Contains code for telemetry.
