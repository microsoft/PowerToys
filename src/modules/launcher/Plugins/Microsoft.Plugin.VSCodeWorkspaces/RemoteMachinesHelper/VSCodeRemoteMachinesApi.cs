using Microsoft.Plugin.VSCodeWorkspaces.SshConfigParser;
using Microsoft.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Plugin.VSCodeWorkspaces.RemoteMachinesHelper
{
    public class VSCodeRemoteMachinesApi
    {
        public VSCodeRemoteMachinesApi() { }

        public List<VSCodeRemoteMachine> Search(string query)
        {
            var results = new List<VSCodeRemoteMachine>();

            foreach (var vscodeInstance in VSCodeInstances.instances)
            {
                // storage.json contains path of ssh_config
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
                                SshConfig config = SshConfig.ParseFile(path.Value);

                                foreach (var h in config.AsEnumerable())
                                {
                                    if (h.Param.ToLower().Equals("host") && h.Value.Contains(query.ToLower()))
                                    {
                                        var machine = new VSCodeRemoteMachine();
                                        machine.Host = h.Value;
                                        machine.VSCodeInstance = vscodeInstance;

                                        foreach(var r in h.Config.AsEnumerable())
                                        {
                                            if (r.Param.ToLower().Equals("hostname"))
                                            {
                                                machine.HostName = r.Value;
                                            }
                                            else if (r.Param.ToLower().Equals("user"))
                                            {
                                                machine.User = r.Value;
                                            }
                                        }

                                        results.Add(machine);
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