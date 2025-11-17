// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.Properties;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.RemoteMachinesHelper;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper;
using Community.PowerToys.Run.Plugin.VSCodeWorkspaces.WorkspacesHelper;

using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces
{
    public class Main : IPlugin, IPluginI18n, IContextMenu
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
                            catch (Win32Exception ex)
                            {
                                HandleError("Can't Open this file", ex, showMsg: true);
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
                            catch (Win32Exception ex)
                            {
                                HandleError("Can't Open this file", ex, showMsg: true);
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

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (selectedResult?.ContextData is not VSCodeWorkspace workspace)
            {
                return new List<ContextMenuResult>();
            }

            string realPath = SystemPath.RealPath(workspace.RelativePath);

            return new List<ContextMenuResult>
            {
                new ContextMenuResult
                {
                    PluginName = Name,
                    Title = $"{Resources.CopyPath} (Ctrl+C)",
                    Glyph = "\xE8C8", // Copy
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = context => CopyToClipboard(realPath),
                },
                new ContextMenuResult
                {
                    PluginName = Name,
                    Title = $"{Resources.OpenInExplorer} (Ctrl+Shift+F)",
                    Glyph = "\xEC50", // File Explorer
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.F,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = context => OpenInExplorer(realPath),
                },
                new ContextMenuResult
                {
                    PluginName = Name,
                    Title = $"{Resources.OpenInConsole} (Ctrl+Shift+C)",
                    Glyph = "\xE756", // Command Prompt
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = context => OpenInConsole(realPath),
                },
            };
        }

        private bool CopyToClipboard(string path)
        {
            try
            {
                Clipboard.SetText(path);
                return true;
            }
            catch (Exception ex)
            {
                HandleError("Can't copy to clipboard", ex, showMsg: true);
                return false;
            }
        }

        private bool OpenInConsole(string path)
        {
            try
            {
                Helper.OpenInConsole(path);
                return true;
            }
            catch (Exception ex)
            {
                HandleError($"Unable to open the specified path in the console: {path}", ex, showMsg: true);
                return false;
            }
        }

        private bool OpenInExplorer(string path)
        {
            if (!Helper.OpenInShell("explorer.exe", $"\"{path}\""))
            {
                HandleError($"Failed to open folder in Explorer at path: {path}", showMsg: true);
                return false;
            }

            return true;
        }

        private void HandleError(string msg, Exception exception = null, bool showMsg = false)
        {
            if (exception != null)
            {
                Log.Exception(msg, exception, exception.GetType());
            }
            else
            {
                Log.Error(msg, typeof(VSCodeWorkspaces.Main));
            }

            if (showMsg)
            {
                _context.API.ShowMsg(
                    $"Plugin: {_context.CurrentPluginMetadata.Name}",
                    msg);
            }
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
