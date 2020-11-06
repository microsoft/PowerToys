using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Plugin.VSCodeWorkspaces.RemoteMachinesHelper;
using Microsoft.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Microsoft.Plugin.VSCodeWorkspaces.WorkspacesHelper;
using Wox.Plugin;

namespace Microsoft.Plugin.VSCodeWorkspaces
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

            foreach (var a in _workspacesApi.Search(query.Search))
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
                    IcoPath = a.VSCodeInstance.VSCodeVersion == VSCodeVersion.Stable ? "Images/code_workspace.png" : "Images/code_insiders_workspace.png",
                    Score = 100 - a.FolderName.ToLower().CompareTo(query.Search.ToLower()),
                    SubTitle = $"Workspace{(a.TypeWorkspace != TypeWorkspace.Local ? $" in {typeWorkspace}" : "")}: {a.RelativePath}",
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
                    }
                });
            }

            foreach (var a in _machinesApi.Search(query.Search))
            {
                var title = $"{a.Host}";

                if (a.User != null && a.User != String.Empty && a.HostName != null && a.HostName != String.Empty)
                {
                    title += $" [{a.User}@{a.HostName}]";
                }

                results.Add(new Result
                {
                    Title = title,
                    IcoPath = a.VSCodeInstance.VSCodeVersion == VSCodeVersion.Stable ? "Images/code_machine.png" : "Images/code_insiders_machine.png",
                    Score = 100 - a.Host.ToLower().CompareTo(query.Search.ToLower()),
                    SubTitle = "SSH Remote machine",
                    Action = c =>
                    {
                        bool hide;
                        try
                        {
                            var process = new ProcessStartInfo()
                            {
                                FileName = a.VSCodeInstance.ExecutablePath,
                                UseShellExecute = true,
                                Arguments = $"--remote ssh-remote+\"{a.Host}\"",
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
            }

            results.Sort((a, b) => { return b.Score - a.Score; });

            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return "VSCode Workspaces";
        }

        public string GetTranslatedPluginDescription()
        {
            return "Opened VSCode Workspaces";
        }
    }
}
