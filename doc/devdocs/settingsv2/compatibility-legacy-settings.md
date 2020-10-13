# Compatibility with legacy settings and runner
A couple of things that need to be kept in mind regarding compatibility with settings v1 and runner.

### 1. Folder Naming structure
- Each of the modules has a folder within the `Local/Microsoft/PowerToys` directory which contains the `settings.json` configurations. The name of this folder must be the same across settingsv1 and settingsv2. 
- The name of the folder is the same as the `ModuleName`. It is set within each of the viewModel files. This name must not be changed to ensure that the user configurations for each of the powertoys rolls over on each update.

### 2. Communication with runner
- The status of each of the modules is communicated with the runner in the form of a json object. The names of all the powerToys is set in the [`EnableModules.cs`](src/core/Microsoft.PowerToys.Settings.UI.Lib/EnabledModules.cs). The `JsonPropertyName` must not be changed to ensure that the information is dispatched properly to all the modules by the runner.

### ImageResizer anomaly
All the powertoys have the same folder name as well as JsonPropertyName to communication information with the runner. However that is not the case with ImageResizer. The folder name is `ImageResizer` whereas the JsonPropertyName is `Image Resizer`. This should not be changed to ensure backward compatibity as well as proper functioning of the module.