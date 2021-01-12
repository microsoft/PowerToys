using Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.SshConfigParser;
using Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.RemoteMachinesHelper
{
    public class VSCodeRemoteMachinesApi
    {
        public VSCodeRemoteMachinesApi() { }

        public List<VSCodeRemoteMachine> Search(string query)
        {
            var results = new List<VSCodeRemoteMachine>();

            foreach (var vscodeInstance in VSCodeInstances.instances)
            {
                // settings.json contains path of ssh_config
                var vscode_settings = Path.Combine(vscodeInstance.AppData, "User\\settings.json");

                if (File.Exists(vscode_settings))
                {
                    var fileContent = File.ReadAllText(vscode_settings);

                    try
                    {
                        dynamic vscodeSettingsFile = JsonConvert.DeserializeObject<dynamic>(fileContent);
                        if (vscodeSettingsFile.ContainsKey("remote.SSH.configFile"))
                        {
                            var path = vscodeSettingsFile["remote.SSH.configFile"];
                            if (File.Exists(path.Value))
                            {
                                foreach (SshHost h in SshConfig.ParseFile(path.Value))
                                {
                                    if (!h.Host.Equals(String.Empty))
                                    {
                                        var machine = new VSCodeRemoteMachine();
                                        machine.Host = h.Host;
                                        machine.VSCodeInstance = vscodeInstance;
                                        machine.HostName = h.HostName!=null?h.HostName:String.Empty;
                                        machine.User = h.User != null ? h.User : String.Empty;

                                        if (h.Host.ToLower().Contains(query.ToLower()) || h.HostName.ToLower().Contains(query.ToLower()) || h.User.ToLower().Contains(query.ToLower()))
                                        {
                                            results.Add(machine);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            return results;
        }
    }
}