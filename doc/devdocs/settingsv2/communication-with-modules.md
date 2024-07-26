# Communication with modules

## Through runner
- The settings process communicates changes in the UI to most modules using the runner through delegates.
- More details on this are mentioned in [`runner-ipc.md`](runner-ipc.md).

## PT Run
- Any changes to the UI are saved by the settings process in the `settings.json` file located within the `/Local/Microsoft/PowerToys/Launcher/` folder.
- PT Run watches for any changes within this file and updates it's general settings or propagates the information to the plugins, depending on the type of information.
Eg: The maximum number of results drop down updates the maximum number of rows in the results list which updates the general settings of PT Run whereas the drive detection checkbox details are dispatched to the indexer plugin.

## Keyboard Manager
- The Settings process and keyboard manager share access to a common `default.json` file which contains information about the remapped keys and shortcuts.
- To ensure that there is no contention while both processes try to access the common file, there is a named file mutex. 
- The settings process expects the keyboard manager process to create the `default.json` file if it does not exist. It does not create the file in case it is not present.
