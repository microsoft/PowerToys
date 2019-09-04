# Code Organization

## Rules

- **Follow the pattern of what you already see in the code**
- Try to package new ideas/components into libraries that have nicely defined interfaces
- Package new ideas into classes or refactor existing ideas into a class as you extend

## Code Overview

General project organization:

#### The [`build`](/build) folder
Contains the Azure pipeline CI build scripts.

#### The [`deps`](/deps) folder
Contains other projects, that PowerToys uses as dependency.

#### The [`doc`](/doc) folder
Documentation for the project, including a [coding guide](/doc/coding) and [design docs](/doc/specs).

#### The [`installer`](/installer) folder
Contains the source code of the PowerToys installer.

#### The [`src`](/src) folder
Contains the source code of the PowerToys runner and of all of the PowerToys modules. **This is where the most of the magic happens.**

#### The [`tools`](/tools) folder
Various tools used by PowerToys. Includes the Visual Studio 2019 project template for new PowerToys.
