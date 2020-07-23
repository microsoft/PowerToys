using Microsoft.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace Microsoft.Plugin.VSCodeWorkspaces.WorkspacesHelper
{
    public class VSCodeWorkspacesApi
    {
        public VSCodeWorkspacesApi() { }

        public List<VSCodeWorkspace> Search(string query)
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
                            foreach (var workspaceUri in vscodeStorageFile.openedPathsList.workspaces3)
                            {
                                if (workspaceUri != null && workspaceUri is String)
                                {
                                    string unescapeUri = Uri.UnescapeDataString(workspaceUri);
                                    var typeWorkspace = ParseVSCodeUri.GetTypeWorkspace(unescapeUri);
                                    if (typeWorkspace.TypeWorkspace.HasValue)
                                    {
                                        var folderName = Path.GetFileName(unescapeUri);
                                        if (folderName.ToLower().Contains(query.ToLower()))
                                        {
                                            results.Add(new VSCodeWorkspace()
                                            {
                                                Path = workspaceUri,
                                                RelativePath = typeWorkspace.Path,
                                                FolderName = folderName,
                                                ExtraInfo = typeWorkspace.MachineName,
                                                TypeWorkspace = typeWorkspace.TypeWorkspace.Value,
                                                VSCodeInstance = vscodeInstance
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex){}

                }

            }


            return results;
        }
    }
}
