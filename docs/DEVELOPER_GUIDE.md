# Developer Guide

## Overview of the Project Structure

The PowerToys project is organized into several directories, each serving a specific purpose. Below is an overview of the main directories and their contents:

- `src`: Contains the source code for all PowerToys modules and utilities.
- `tests`: Contains unit and integration tests for the PowerToys modules.
- `docs`: Contains documentation for developers and users.
- `.github`: Contains GitHub-specific files, including workflows and issue templates.
- `.pipelines`: Contains CI/CD pipeline configurations and templates.

## Setup Instructions for the Development Environment

To set up your development environment for PowerToys, follow these steps:

1. **Clone the Repository**:
   ```sh
   git clone https://github.com/microsoft/PowerToys.git
   cd PowerToys
   ```

2. **Install Dependencies**:
   - Ensure you have the following installed:
     - [Visual Studio 2019 or later](https://visualstudio.microsoft.com/)
     - [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
     - [Node.js](https://nodejs.org/)
     - [Yarn](https://yarnpkg.com/)

3. **Restore NuGet Packages**:
   ```sh
   nuget restore PowerToys.sln
   ```

4. **Build the Solution**:
   Open `PowerToys.sln` in Visual Studio and build the solution.

5. **Run the Application**:
   Set `PowerToys` as the startup project and run the application.

## Guidelines for Writing and Running Tests

### Writing Tests

- **Unit Tests**:
  - Place unit tests in the `tests/unit` directory.
  - Use a testing framework like [xUnit](https://xunit.net/) or [MSTest](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest).

- **Integration Tests**:
  - Place integration tests in the `tests/integration` directory.
  - Ensure that integration tests cover interactions between different modules and components.

### Running Tests

To run the tests, use the following commands:

- **Run Unit Tests**:
  ```sh
  dotnet test tests/unit
  ```

- **Run Integration Tests**:
  ```sh
  dotnet test tests/integration
  ```

Ensure that all tests pass before submitting a pull request.

