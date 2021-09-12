# Shell Plugin
- Shell plugin emulates the Windows Run Prompt (Win+R).
- Shell Plugin is one of the non-global plugins which has an action keyword set to `>`.

![Image of Shell plugin](/doc/images/launcher/plugins/shell.png)

### Functionality
- The Shell command expands environment variables, so `>%appdata%` works as expected.
- On inheriting the Shell plugin from Wox, there are three different ways of executing a command, using the command prompt, powershell or the run prompt. To uphold the name of PT Run, the Shell plugin always executes commands as the Run prompt would.
- The Shell plugin has a concept of history where the previously executed commands show up in the drop down list along with the number of times they have been executed.
- The Run prompt has the folder plugin function where we can navigate to different locations and entering the path to a directory displays all the sub-directories. To prevent reimplementing this logic, the shell plugin references the folder plugin to implement this functionality.

### Score
The Shell plugin results have a very high score of 5000. Hence, they are one of the first results in the list.
