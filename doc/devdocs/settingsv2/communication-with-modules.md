# Communication with modules

## Through runner
- The settings process communicates changes in the UI to most modules using the runner using delegates.
- More details on this are mentioned in [`runner-ipc.md`](settingsv2/runner-ipc.md).

## PT Run
- Any changes in the UI are saved by the settings process in the `settings.json` file within the `/Local/Microsoft/PowerToys/Launcher/` folder.
- PT Run watches for any changes within this file and updates it's general settings or propagates the information to the plugins.

## Keyboard Manager
- The Settings process and keyboard manager share access to a common `default.json` file which contains information about the remapped keys and shortcuts.
- To ensure that there is no contention while both processes try to access the common file, there is a named file mutex. 
- The settings process expects the keyboard manager process to create the `default.json` file if it does not exist. It does not create the file in case it is not present.