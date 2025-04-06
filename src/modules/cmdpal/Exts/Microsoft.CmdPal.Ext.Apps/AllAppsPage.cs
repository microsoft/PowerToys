// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : ListPage
{
    private readonly Lock _listLock = new();
    private AppListItem[] allAppsSection = [];

    public AllAppsPage()
    {
        this.Name = Resources.all_apps;
        this.Icon = IconHelpers.FromRelativePath("Assets\\AllApps.svg");
        this.ShowDetails = true;
        this.IsLoading = true;
        this.PlaceholderText = Resources.search_installed_apps_placeholder;

        Task.Run(() =>
        {
            lock (_listLock)
            {
                BuildListItems();
            }
        });
    }

    public override IListItem[] GetItems()
    {
        if (allAppsSection.Length == 0 || AppCache.Instance.Value.ShouldReload())
        {
            lock (_listLock)
            {
                BuildListItems();
            }
        }

        return allAppsSection;
    }

    private void BuildListItems()
    {
        this.IsLoading = true;

        Stopwatch stopwatch = new();
        stopwatch.Start();

        List<AppItem> apps = GetPrograms();

        this.allAppsSection = apps
                        .Select((app) => new AppListItem(app, true))
                        .ToArray();

        this.IsLoading = false;

        AppCache.Instance.Value.ResetReloadFlag();

        stopwatch.Stop();
        Logger.LogTrace($"{nameof(AllAppsPage)}.{nameof(BuildListItems)} took: {stopwatch.ElapsedMilliseconds} ms");
    }

    internal List<AppItem> GetPrograms()
    {
        IEnumerable<AppItem> uwpResults = AppCache.Instance.Value.UWPs
            .Where((application) => application.Enabled)
            .Select(app =>
                new AppItem()
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    Type = UWPApplication.Type(),
                    IcoPath = app.LogoType != LogoType.Error ? app.LogoPath : string.Empty,
                    DirPath = app.Location,
                    UserModelId = app.UserModelId,
                    IsPackaged = true,
                    Commands = app.GetCommands(),
                });

        IEnumerable<AppItem> win32Results = AppCache.Instance.Value.Win32s
            .Where((application) => application.Enabled && application.Valid)
            .Select(app =>
            {
                string icoPath = string.IsNullOrEmpty(app.IcoPath) ?
                    (app.AppType == Win32Program.ApplicationType.InternetShortcutApplication ?
                        app.IcoPath :
                        app.FullPath) :
                    app.IcoPath;

                // icoPath = icoPath.EndsWith(".lnk", System.StringComparison.InvariantCultureIgnoreCase) ? (icoPath + ",0") : icoPath;
                icoPath = icoPath.EndsWith(".lnk", System.StringComparison.InvariantCultureIgnoreCase) ?
                    app.FullPath :
                    icoPath;
                return new AppItem()
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    Type = app.Type(),
                    IcoPath = icoPath,
                    ExePath = !string.IsNullOrEmpty(app.LnkFilePath) ? app.LnkFilePath : app.FullPath,
                    DirPath = app.Location,
                    Commands = app.GetCommands(),
                };
            });

        return uwpResults.Concat(win32Results).OrderBy(app => app.Name).ToList();
    }
}
