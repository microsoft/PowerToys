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
        public readonly string PathUserAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private string _systemPath = String.Empty;

        private string _userAppDataPath = Environment.GetEnvironmentVariable("AppData");

        private List<VSCodeInstance> _vscodeInstances = new List<VSCodeInstance>();

        public VSCodeWorkspacesApi()
        {
            LoadVSCodeInstances();
        }

        // Gets the executablePath and AppData for each instance of VSCode
        public void LoadVSCodeInstances()
        {
            if (_systemPath != Environment.GetEnvironmentVariable("PATH"))
            {

                _vscodeInstances = new List<VSCodeInstance>();

                _systemPath = Environment.GetEnvironmentVariable("PATH");
                var paths = _systemPath.Split(";");
                foreach (var path in paths)
                {
                    if (path.Contains("\\Microsoft VS Code\\"))
                    {
                        var path_executable = System.IO.Path.GetFullPath(path.Replace("\\bin", "\\Code.exe"));
                        if (File.Exists(path_executable))
                        {
                            _vscodeInstances.Add(new VSCodeInstance
                            {
                                ExecutablePath = path_executable,
                                AppData = Path.Combine(_userAppDataPath, "Code"),
                                VSCodeVersion = VSCodeVersion.Stable
                            });
                        }
                    }
                    else if (path.Contains("\\Microsoft VS Code Insiders\\"))
                    {
                        var path_executable = System.IO.Path.GetFullPath(path.Replace("\\bin", "\\Code - Insiders.exe"));
                        if (File.Exists(path_executable))
                        {
                            _vscodeInstances.Add(new VSCodeInstance
                            {
                                ExecutablePath = path_executable,
                                AppData = Path.Combine(_userAppDataPath, "Code - Insiders"),
                                VSCodeVersion = VSCodeVersion.Insiders
                            });
                        }
                    }
                    else if (path.Contains("\\Microsoft VS Code Exploration\\"))
                    {
                        var path_executable = System.IO.Path.GetFullPath(path.Replace("\\bin", "\\Code - Exploration.exe"));
                        if (File.Exists(path_executable))
                        {
                            _vscodeInstances.Add(new VSCodeInstance
                            {
                                ExecutablePath = path_executable,
                                AppData = Path.Combine(_userAppDataPath, "Code - Exploration"),
                                VSCodeVersion = VSCodeVersion.Exploration
                            });
                        }
                    }
                }
            }
        }

        public List<VSCodeWorkspace> Search(string query)
        {
            var results = new List<VSCodeWorkspace>();

            foreach (var vscodeInstance in _vscodeInstances)
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
                                                VSCodeVersion = vscodeInstance.VSCodeVersion,
                                                VSCodeExecutable = vscodeInstance.ExecutablePath
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
