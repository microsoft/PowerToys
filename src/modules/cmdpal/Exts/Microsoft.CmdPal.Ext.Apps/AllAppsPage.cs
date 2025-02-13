// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Storage;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AllAppsPage : ListPage
{
    private IListItem[] allAppsSection = [];

    private object l = new();

    public AllAppsPage()
    {
        this.Name = Resources.all_apps;
        this.Icon = new IconInfo("\ue71d");
        this.ShowDetails = true;
        this.IsLoading = true;
        this.PlaceholderText = Resources.search_installed_apps_placeholder;

        Task.Run(() =>
        {
            lock (l)
            {
                this.allAppsSection = GetPrograms()
                                .Select((app) => new AppListItem(app))
                                .ToArray();

                this.IsLoading = false;
            }
        });
    }

    public override IListItem[] GetItems()
    {
        if (this.allAppsSection == null || allAppsSection.Length == 0 || AppCache.Instance.Value.ShouldReload())
        {
            lock (l)
            {
                this.IsLoading = true;

                var apps = GetPrograms();
                this.allAppsSection = apps
                                .Select((app) => new AppListItem(app))
                                .ToArray();

                this.IsLoading = false;

                AppCache.Instance.Value.ResetReloadFlag();
            }
        }

        return allAppsSection;
    }

    internal List<AppItem> GetPrograms()
    {
        var uwpResults = AppCache.Instance.Value.UWPs
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

        var win32Results = AppCache.Instance.Value.Win32s
            .Where((application) => application.Enabled && application.Valid)
            .Select(app =>
            {
                return new AppItem()
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    Type = app.Type(),
                    IcoPath = app.IcoPath,
                    ExePath = !string.IsNullOrEmpty(app.LnkFilePath) ? app.LnkFilePath : app.FullPath,
                    DirPath = app.Location,
                    Commands = app.GetCommands(),
                };
            });

        return uwpResults.Concat(win32Results).OrderBy(app => app.Name).ToList();
    }
}
