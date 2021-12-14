# Compatibility with legacy settings and runner
The following must be kept in mind regarding compatibility with settings v1 and runner.

### 1. Folder Naming structure
- Each of the modules has a folder within the `Local/Microsoft/PowerToys` directory which contains the module configurations within the `settings.json` file. The name of this folder must be the same across settingsv1 and settingsv2. 
- The name of the settings folder for each powertoy is the same as the `ModuleName`. It is set within each of the viewModel files. This name must not be changed to ensure that the user configurations for each of the powertoys rolls over on update.

### 2. Communication with runner
- The status of each of the modules is communicated with the runner in the form of a json object. The names of all the powerToys is set in the [`EnableModules.cs`](src/settings-ui/Settings.UI.Library/EnabledModules.cs) file. The `JsonPropertyName` must not be changed to ensure that the information is dispatched properly to all the modules by the runner.

### ImageResizer anomaly
All the powertoys have the same folder name as well as JsonPropertyName to communicate information with the runner. However that is not the case with ImageResizer. The folder name is `ImageResizer` whereas the JsonPropertyName is `Image Resizer`(Note the additional space). This should not be changed to ensure backward compatibility as well as proper functioning of the module.
