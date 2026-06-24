# Issue/PR commands

The PowerToys repository uses some special keywords to help manage issues and pull requests. Here is a list of the most important commands you can use in issue and PR descriptions or comments.

| Command | Description |
|---------|-------------|
| `/azp run` | Triggers the Azure Pipelines CI build for the current PR. Useful if you want to re-run the build without creating a new commit. |
| `/bugreport` / `/reportbug` | Adds a comment with a manual for the Bug Report Tool, which helps users collect logs and system information for debugging purposes. It requests to upload this file and adds the `Needs-Author-Feedback` label. |
| `/feedbackhub` | Adds a comment with a link to the Feedback Hub app on Windows, where users can submit feedback about PowerToys. Closes the issue and adds the `Resolution-Please File on Feedback Hub` label. |
| `/dup #...` / `/duplicate #...` / `/dup https://...` / `/duplicate https://...` | Marks the current issue as a duplicate of another issue. It closes the current issue and applies the `Resolution-Duplicate` label. Replace `#...` with the issue number or a link to the issue. |
| `/needinfo` | Adds the `Needs-Author-Feedback` label to the issue or PR, indicating that more information is needed from the author. |
| `/helped` | Closes the issue and adds the `Resolution-Helped User` label. Furthermore a comment is added with a link to the PowerToys user documentation. |
| `/loc` | Adds a comment informing the user that the issue was forwarded to the localization team and will soon be fixed. It adds the `Loc-Sent To Team` label. |

## Defining new commands

Most of these commands are using the [Microsoft GitHub Policy Service](https://github.com/apps/microsoft-github-policy-service) bot. Its commands are defined in the [PowerToys policy configuration file](/.github/policies/resourceManagement.yml).

## Other automated tasks

### Automatic labeling

The bot can automatically apply the correct `product-...` label for any opened issue.

> [!NOTE]
> This feature is currently only available for the Workspaces module as a test.

### The `Needs-Author-Feedback` label

If an issue has this label and had no activity for 5 days, the bot will post a comment reminding the author to provide the needed information. It also adds the `Status-No recent activity` label. If no further activity occurs for another 5 days, the bot will close the issue.

### Filtering users that want to contribute

If a user utters their intention to contribute (e.g., by using the phrase "I want to contribute" in an issue or PR), the bot will add a comment with a link to the ["Would you like to contribute to PowerToys?" thread](https://github.com/microsoft/PowerToys/issues/28769).
