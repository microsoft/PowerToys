using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspacesApi
    {
        public VSCodeWorkspacesApi() { }

        private VSCodeWorkspace parseVSCodeUri(string uri, VSCodeInstance vscodeInstance)
        {
            if (uri != null && uri is String)
            {
                string unescapeUri = Uri.UnescapeDataString(uri);
                var typeWorkspace = ParseVSCodeUri.GetTypeWorkspace(unescapeUri);
                if (typeWorkspace.TypeWorkspace.HasValue)
                {
                    var folderName = Path.GetFileName(unescapeUri);
                    return new VSCodeWorkspace()
                    {
                        Path = uri,
                        RelativePath = typeWorkspace.Path,
                        FolderName = folderName,
                        ExtraInfo = typeWorkspace.MachineName,
                        TypeWorkspace = typeWorkspace.TypeWorkspace.Value,
                        VSCodeInstance = vscodeInstance
                    };
                }
            }

            return null;
        }

        public List<VSCodeWorkspace> Workspaces
        {
            get
            {

                var results = new List<VSCodeWorkspace>();

                foreach (var vscodeInstance in VSCodeInstances.instances)
                {
                    // storage.json contains opened Workspaces
                    var vscode_storage = Path.Combine(vscodeInstance.AppData, "storage.json");

                    if (File.Exists(vscode_storage))
                    {
                        var fileContent = File.ReadAllText(vscode_storage);

                        try
                        {
                            VSCodeStorageFile vscodeStorageFile = JsonConvert.DeserializeObject<VSCodeStorageFile>(fileContent);

                            if (vscodeStorageFile != null)
                            {
                                //for previous versions of vscode
                                if (vscodeStorageFile.openedPathsList.workspaces3 != null)
                                {
                                    foreach (var workspaceUri in vscodeStorageFile.openedPathsList.workspaces3)
                                    {
                                        var uri = parseVSCodeUri(workspaceUri, vscodeInstance);
                                        if (uri != null)
                                        {
                                            results.Add(uri);
                                        }
                                    }
                                }

                                //vscode v1.55.0 or later
                                if (vscodeStorageFile.openedPathsList.entries != null)
                                {
                                    foreach (var workspaceUri in vscodeStorageFile.openedPathsList.entries.Select(x => x.folderUri))
                                    {
                                        var uri = parseVSCodeUri(workspaceUri, vscodeInstance);
                                        if (uri != null)
                                        {
                                            results.Add(uri);
                                        }
                                    }
                                }

                            }
                        }
                        catch (Exception ex)
                        {
                            var message = $"Failed to deserialize ${vscode_storage}";
                            Log.Exception(message, ex, GetType());
                        }

                    }

                }


                return results;
            }
        }
    }
}
