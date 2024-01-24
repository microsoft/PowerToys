// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private PluginInitContext _context;

        public string Name => GetTranslatedPluginTitle();

        public string Description => GetTranslatedPluginDescription();

        public static string PluginID => "525995402BEF4A8CA860D92F6D108092";

        public Main()
        {
            VSCodeInstances.LoadVSCodeInstances();
        }

        private readonly VSCodeWorkspacesApi _workspacesApi = new VSCodeWorkspacesApi();

        private readonly VSCodeRemoteMachinesApi _machinesApi = new VSCodeRemoteMachinesApi();

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (query != null)
            {
                // Search opened workspaces
                _workspacesApi.Workspaces.ForEach(a =>
                {
                    var title = a.WorkspaceType == WorkspaceType.ProjectFolder ? a.FolderName : a.FolderName.Replace(".code-workspace", $" ({Resources.Workspace})");

                    var typeWorkspace = a.WorkspaceEnvironmentToString();
                    if (a.WorkspaceEnvironment != WorkspaceEnvironment.Local)
                    {
                        title = $"{title}{(a.ExtraInfo != null ? $" - {a.ExtraInfo}" : string.Empty)} ({typeWorkspace})";
                    }

                    var tooltip = new ToolTipData(title, $"{(a.WorkspaceType == WorkspaceType.WorkspaceFile ? Resources.Workspace : Resources.ProjectFolder)}{(a.WorkspaceEnvironment != WorkspaceEnvironment.Local ? $" {Resources.In} {typeWorkspace}" : string.Empty)}: {SystemPath.RealPath(a.RelativePath)}");

                    results.Add(new Result
                    {
                        Title = title,
                        SubTitle = $"{(a.WorkspaceType == WorkspaceType.WorkspaceFile ? Resources.Workspace : Resources.ProjectFolder)}{(a.WorkspaceEnvironment != WorkspaceEnvironment.Local ? $" {Resources.In} {typeWorkspace}" : string.Empty)}: {SystemPath.RealPath(a.RelativePath)}",
                        Icon = a.VSCodeInstance.WorkspaceIcon,
                        ToolTipData = tooltip,
                        Action = c =>
                        {
                            bool hide;
                            try
                            {
                                var process = new ProcessStartInfo
                                {
                                    FileName = a.VSCodeInstance.ExecutablePath,
                                    UseShellExecute = true,
                                    Arguments = a.WorkspaceType == WorkspaceType.ProjectFolder ? $"--folder-uri {a.Path}" : $"--file-uri {a.Path}",
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
                        },
                        ContextData = a,
                    });
                });

                // Search opened remote machines
                _machinesApi.Machines.ForEach(a =>
                {
                    var title = $"{a.Host}";

                    if (a.User != null && a.User != string.Empty && a.HostName != null && a.HostName != string.Empty)
                    {
                        title += $" [{a.User}@{a.HostName}]";
                    }

                    var tooltip = new ToolTipData(title, Resources.SSHRemoteMachine);

                    results.Add(new Result
                    {
                        Title = title,
                        SubTitle = Resources.SSHRemoteMachine,
                        Icon = a.VSCodeInstance.RemoteIcon,
                        ToolTipData = tooltip,
                        Action = c =>
                        {
                            bool hide;
                            try
                            {
                                var process = new ProcessStartInfo()
                                {
                                    FileName = a.VSCodeInstance.ExecutablePath,
                                    UseShellExecute = true,
                                    Arguments = $"--new-window --enable-proposed-api ms-vscode-remote.remote-ssh --remote ssh-remote+{((char)34) + a.Host + ((char)34)}",
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
                        },
                        ContextData = a,
                    });
                });
            }

            results = results.Where(a => a.Title.Contains(query.Search, StringComparison.InvariantCultureIgnoreCase)).ToList();

            results.ForEach(x =>
                    {
                        if (x.Score == 0)
                        {
                            x.Score = 100;
                        }

                        // intersect the title with the query
                        var intersection = Convert.ToInt32(x.Title.ToLowerInvariant().Intersect(query.Search.ToLowerInvariant()).Count() * query.Search.Length);
                        var differenceWithQuery = Convert.ToInt32((x.Title.Length - intersection) * query.Search.Length * 0.7);
                        x.Score = x.Score - differenceWithQuery + intersection;

                        // if is a remote machine give it 12 extra points
                        if (x.ContextData is VSCodeRemoteMachine)
                        {
                            x.Score = Convert.ToInt32(x.Score + (intersection * 2));
                        }
                    });

            results = results.OrderByDescending(x => x.Score).ToList();
            if (query.Search == string.Empty || query.Search.Replace(" ", string.Empty) == string.Empty)
            {
                results = results.OrderBy(x => x.Title).ToList();
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
