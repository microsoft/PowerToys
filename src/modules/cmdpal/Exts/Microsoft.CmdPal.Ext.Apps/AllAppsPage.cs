// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public sealed partial class AllAppsPage : ListPage
{
    private IListItem[] allAppsSection = [];

    public AllAppsPage()
    {
        StringMatcher.Instance = new StringMatcher();
        this.Name = "All Apps";
        this.Icon = new IconInfo("\ue71d");
        this.ShowDetails = true;
        this.IsLoading = true;
        this.PlaceholderText = "Search installed apps...";
    }

    public override IListItem[] GetItems()
    {
        if (this.allAppsSection == null || allAppsSection.Length == 0)
        {
            var apps = GetPrograms();
            this.IsLoading = false;
            this.allAppsSection = apps
                            .Select((app) => new AppListItem(app))
                            .ToArray();
        }

        return allAppsSection;
    }

    internal static List<AppItem> GetPrograms()
    {
        // NOTE TO SELF:
        //
        // There's logic in Win32Program.All() here to pick the "sources" for programs.
        // I've manually hardcoded it to:
        // * StartMenuProgramPaths
        // * DesktopProgramPaths
        // * RegistryAppProgramPaths
        // for now. I've disabled the "PATH" source too, because it's n o i s y
        //
        // This also doesn't include Packaged apps, cause they're enumerated entirely separately.
        var cache = AppCache.Instance.Value;
        var uwps = cache.UWPs;
        var win32s = cache.Win32s;
        var uwpResults = uwps
            .Where((application) => application.Enabled /*&& application.Valid*/)
            .Select(app =>
                new AppItem
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    IcoPath = app.LogoType != LogoType.Error ? app.LogoPath : string.Empty,
                    DirPath = app.Location,
                    UserModelId = app.UserModelId,

                    // ExePath = app.FullPath,
                });
        var win32Results = win32s
            .Where((application) => application.Enabled /*&& application.Valid*/)
            .Select(app =>
            {
                return new AppItem
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    IcoPath = app.AppType == Win32Program.ApplicationType.InternetShortcutApplication ?
                       app.IcoPath :
                       app.FullPath,
                    ExePath = !string.IsNullOrEmpty(app.LnkFilePath) ? app.LnkFilePath : app.FullPath,
                    DirPath = app.Location,
                };
            });

        return uwpResults.Concat(win32Results).OrderBy(app => app.Name).ToList();
    }
}
