using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Mages.Core;
using Microsoft.Plugin.VSCodeWorkspaces.WorkspacesHelper;
using Wox.Plugin;

namespace Microsoft.Plugin.VSCodeWorkspaces
{
    public class Main : IPlugin, IPluginI18n
    {
        public PluginInitContext _context { get; private set; }

        public readonly VSCodeWorkspacesApi _api = new VSCodeWorkspacesApi();

        public string GetTypeWorkspace(TypeWorkspace type)
        {
            switch (type)
            {
                case TypeWorkspace.Local: return "Local";
                case TypeWorkspace.Codespaces: return "Codespaces";
                case TypeWorkspace.RemoteContainers: return "Container";
                case TypeWorkspace.RemoteSSH: return "SSH";
                case TypeWorkspace.RemoteWSL: return "WSL";
            }

            return string.Empty;
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            foreach (var a in _api.Search(query.Search))
            {
                var title = $"{a.FolderName}";

                var typeWorkspace = GetTypeWorkspace(a.TypeWorkspace);
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
                    IcoPath = a.VSCodeVersion == VSCodeVersion.Stable ? "Images/code.png" : "Images/code_insiders.png",
                    Score = 200 - Math.Abs(a.FolderName.ToLower().CompareTo(query.Search.ToLower())),
                    SubTitle = $"Workspace{(a.TypeWorkspace != TypeWorkspace.Local ? $" in {typeWorkspace}" : "")}: {a.RelativePath}",
                    Action = c =>
                    {
                        bool hide;
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = a.VSCodeExecutable,
                                UseShellExecute = false,
                                Arguments = $"--folder-uri {a.Path}"
                            });
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
