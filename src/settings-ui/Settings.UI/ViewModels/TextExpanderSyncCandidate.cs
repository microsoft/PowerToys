// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>A snippet file found in OneDrive, offered in the "another PC" setup.</summary>
    public sealed class TextExpanderSyncCandidate
    {
        public TextExpanderSyncCandidate(TextExpanderSyncAccount account, string filePath, string folderName, string location, string details, string automationName)
        {
            Account = account;
            FilePath = filePath;
            FolderName = folderName;
            Location = location;
            Details = details;
            AutomationName = automationName;
        }

        /// <summary>Gets the OneDrive account the file is in.</summary>
        public TextExpanderSyncAccount Account { get; }

        /// <summary>Gets the snippet file's full path, shown as a tooltip.</summary>
        public string FilePath { get; }

        /// <summary>Gets the name of the folder the file is in, such as "powertoys-textexpander".</summary>
        public string FolderName { get; }

        /// <summary>
        /// Gets where the file is, from the account's folder down to the file's own name, so a wrong
        /// folder is easy to spot: "OneDrive - Contoso › Projects › snippets.txt".
        /// </summary>
        public string Location { get; }

        /// <summary>Gets the date the file changed and, for the first file, the only one read, its snippet count: "11 snippets · October 18".</summary>
        public string Details { get; }

        /// <summary>Gets everything above in one sentence, for screen readers.</summary>
        public string AutomationName { get; }

        // RadioButtons uses this as the accessible name of the generated item.
        public override string ToString() => AutomationName;
    }
}
