using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeStorageFile
    {
        [JsonPropertyName("openedPathsList")]
        public OpenedPathsList OpenedPathsList { get; set; }
    }

    public class OpenedPathsList
    {
        [JsonPropertyName("workspaces3")]
        public List<string> Workspace3 { get; set; }

        [JsonPropertyName("entries")]
        public List<VSCodeWorkspaceEntry> Entries { get; set; }
    }

    public class VSCodeWorkspaceEntry
    {
	    [JsonPropertyName("folderUri")]
	    public string FolderUri { get; set; }
	    [JsonPropertyName("label")]
	    public string Label { get; set; }
    }
}
