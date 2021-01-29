using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.Properties;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.RemoteMachinesHelper;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces
{
    public class Main : IPlugin, IPluginI18n
    {
        public PluginInitContext _context { get; private set; }

        public Main()
        {
            VSCodeInstances.LoadVSCodeInstances();
        }

        public readonly VSCodeWorkspacesApi _workspacesApi = new VSCodeWorkspacesApi();

        public readonly VSCodeRemoteMachinesApi _machinesApi = new VSCodeRemoteMachinesApi();

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (query != null)
            {
                // Search opened workspaces
                _workspacesApi.Workspaces.ForEach(a =>
                {
                    var title = $"{a.FolderName}";

                    var typeWorkspace = a.WorkspaceTypeToString();
                    if (a.TypeWorkspace == TypeWorkspace.Codespaces)
                    {
                        title += $" - {typeWorkspace}";
                    }
                    else if (a.TypeWorkspace != TypeWorkspace.Local)
                    {
                        title += $" - {(a.ExtraInfo != null ? $"{a.ExtraInfo} ({typeWorkspace})" : typeWorkspace)}";
                    }

                    results.Add(new Result
                    {
                        Title = title,
                        SubTitle = $"${Resources.Workspace}{(a.TypeWorkspace != TypeWorkspace.Local ? $" ${Resources.In} {typeWorkspace}" : "")}: {SystemPath.RealPath(a.RelativePath)}",
                        Icon = a.VSCodeInstance.WorkspaceIcon,
                        Action = c =>
                        {
                            bool hide;
                            try
                            {
                                var process = new ProcessStartInfo
                                {
                                    FileName = a.VSCodeInstance.ExecutablePath,
                                    UseShellExecute = true,
                                    Arguments = $"--folder-uri {a.Path}",
                                    WindowStyle = ProcessWindowStyle.Hidden
                                };
                                Process.Start(process);

                                hide = true;
                            }
                            catch (Win32Exception)
                            {
                                var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                                var msg = "Can't Open this file";
                                _context.API.ShowMsg(name, msg, string.Empty);
                                hide = false;
                            }
                            return hide;
                        },
                    });
                });

                // Search opened remote machines
                _machinesApi.Machines.ForEach(a =>
                {
                    var title = $"{a.Host}";

                    if (a.User != null && a.User != String.Empty && a.HostName != null && a.HostName != String.Empty)
                    {
                        title += $" [{a.User}@{a.HostName}]";
                    }

                    results.Add(new Result
                    {
                        Title = title,
                        SubTitle = Resources.SSHRemoteMachine,
                        Icon = a.VSCodeInstance.RemoteIcon,
                        Action = c =>
                        {
                            bool hide;
                            try
                            {
                                var process = new ProcessStartInfo()
                                {
                                    FileName = a.VSCodeInstance.ExecutablePath,
                                    UseShellExecute = true,
                                    Arguments = $"--remote ssh-remote+{((char)34) + a.Host + ((char)34)}",
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                };
                                Process.Start(process);

                                hide = true;
                            }
                            catch (Win32Exception)
                            {
                                var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                                var msg = "Can't Open this file";
                                _context.API.ShowMsg(name, msg, string.Empty);
                                hide = false;
                            }
                            return hide;
                        }
                    });
                });
            }

            results.ForEach(x => x.Score = 100);
            results.OrderBy(x => x.Title);

            if (query.ActionKeyword == String.Empty || (query.ActionKeyword != String.Empty && query.Search != String.Empty))
            {
                results = results.Where(a => a.Title.ToLower().Contains(query.Search.ToLower())).ToList();

                results.Sort((a, b) => b.Title.ToLower().CompareTo(query.Search.ToLower()) - a.Title.ToLower().CompareTo(query.Search.ToLower()));
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.PluginTitle;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.PluginDescription;
        }
    }
}
