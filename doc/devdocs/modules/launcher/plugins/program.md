# Program Plugin
The program plugin as the name suggests is used to search for programs installed on the system.

![Image of Program plugin](/doc/images/launcher/plugins/program.png)

There are broadly two different categories of applications:

1. Packaged applications
2. Win32 applications

### [UWP](src/modules/launcher/Plugins/Microsoft.Plugin.Program/Programs/UWP.cs)
- The logic for indexing Packaged applications is present within the [`UWP.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Program/Programs/UWP.cs) file.
- There can be multiple applications present within a package. The [`UWPApplication.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Program/Programs/UWPApplication.cs) file encapsulates the properties of a packaged application.
- To index packaged applications, the `PackageManager` retrieves all the packages for the current user and indexes all the applications.
- To retrieve the app icon for packaged applications, the assets path is retrieved from the `Application Manifest` file. There are multiple icons corresponding to each scale, target size and theme. The best icon is chosen given the theme of powerToys Run.

### [Win32Program](src/modules/launcher/Plugins/Microsoft.Plugin.Program/Programs/Win32Program.cs)
- Win32 programs in the following locations are indexed by PT Run-
    1. Desktop
    2. Public Desktop (Applications present on the desktop of all the users)
    3. Registry (Some programs)
    4. Start Menu
    5. Common start menu (Applications which are common to all users)
    8. Locations pointed to by the PATH environment variable.
- To prevent applications and shortcuts present in multiple locations from showing up as duplicate results, we consider apps with the same name, executable name and full path to be the same.
- The subtitle of the application result is set based on it's application type. It could be one of the following:
    1. Lnk Shortcuts
    2. Appref files
    3. Internet shortcut - steam and epic games
    4. PWAs
    5. Run commands - these are indexed by the PATH environment variable

### Score
- The score for each application result is based on the how many letters are matched, how close the matched letters are to the actual word and the index of the matched characters.
- There is a threshold score to decide the apps which are to be displayed and applications which have a lower score are not displayed by PT Run.

### Update Program List in Runtime
- Packaged and Win32 app helpers exist to reflect changes in the list of indexed apps when applications are installed on the system while PT Run is executing.
- Packaged applications trigger events when the package is being installed and uninstalled. PT Run listens to those events to index applications which are newly installed or to delete an app which no longer exists from the database.
- No such events exist for Win32 applications. We therefore use FileSystem Watchers to monitor the locations that we index for newly created, deleted or renamed application files and update the indexed Win32 catalog accordingly.

### Additional Notes
- Arguments can be provided to the program plugin by entering them after `--` (a double dash).
- The localization is done using the `Localization Helper`from `Wox.Plugin.Common` hosted at runtime in a variable of plugin's main class.
- The `Run commands` differ in two points from the normal `Win32Programs`:
   - The result title contains the executable type.
   - The file types `.cpl` and `.msc` are supported too.
