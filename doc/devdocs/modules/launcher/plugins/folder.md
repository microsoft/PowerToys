# Folder Plugin
The Folder plugin is used to navigate the directory structure and display the sub-folders and files within a folder.
![Image of Folder plugin](/doc/images/launcher/plugins/folder.png)

### [`FolderHelper.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/Sources/Path/FolderHelper.cs)
- The [`FolderHelper`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/Sources/Path/FolderHelper.cs) class leverages the `DriveInformation` and `folderLinks` to get the folder results for a user query.
- The [`DriveInformation`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/Sources/Path/DriveInformation.cs) class gets the list of all drives on the system.
- The [`FolderLink`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/Sources/FolderLink.cs) object corresponds to a user created link for frequently accessed projects. This was inherited from Wox but is presently not functional as we don't have the UI setup in settings to get this user input. Each folderLink object has a `nickname`, which is the name of the folder and this can be used to directly access that folder instead of entering the entire path.

### [`IFolderProcessor.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/Sources/IFolderProcessor.cs)
The `IFolderProcessor` utilizes the `FolderHelper` class to extract the folders and return the results.
There are two types of Folder Processors, based on the type of information they are processing -
1. [`UserFolderProcessor`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/UserFolderProcessor.cs) - This Processor is currently not used in PT Run but it is used to process the user created folder links.
2. [`InternalDirectoryProcessor`](src/modules/launcher/Plugins/Microsoft.Plugin.Folder/InternalDirectoryProcessor.cs) - This processor is used to retrieve the files and folders located within the current drive or shared folder.

### Score
The first result is of score 500 and the following results are scored 10.