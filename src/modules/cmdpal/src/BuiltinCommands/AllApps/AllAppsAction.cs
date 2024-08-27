// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Windows.CommandPalette.Extensions;
using AllApps.Programs;
using System.Diagnostics;
using Wox.Infrastructure;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace AllApps;

public sealed class AppCache
{
    internal IList<Win32Program> Win32s = AllApps.Programs.Win32Program.All();
    internal IList<UWPApplication> UWPs = Programs.UWP.All();
    public static readonly Lazy<AppCache> Instance = new(() => new());
}

internal sealed class AppItem {
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string IcoPath { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string DirPath { get; set; } = "";
    public string UserModelId { get; set; } = "";

    public AppItem()
    {
    }
}

// NOTE this is pretty close to what we'd put in the SDK
internal sealed class OpenPathAction(string target) : InvokableCommand {
    private readonly string _Target = target;
    internal static async Task LaunchTarget(string t)
    {
        await Task.Run(() =>
        {
            Process.Start(new ProcessStartInfo(t) { UseShellExecute = true });
        });
    }
    public override ActionResult Invoke()
    {
        LaunchTarget(this._Target).Start();
        return ActionResult.GoHome();
    }
}
internal sealed class AppAction : InvokableCommand
{
    private readonly AppItem _app;

    internal AppAction(AppItem app) {
        this._Name = "Run";
        this._app = app;
        this._Icon = new(_app.IcoPath);
    }
    internal static async Task StartApp(string amuid)
    {
        var appManager = new ApplicationActivationManager();
        const ActivateOptions noFlags = ActivateOptions.None;
        await Task.Run(() =>
        {
            try
            {
                appManager.ActivateApplication(amuid, /*queryArguments*/ "", noFlags, out var unusedPid);
            }
            catch (System.Exception)
            {
            }
        }).ConfigureAwait(false);
    }
    internal static async Task StartExe(string path)
    {
        var appManager = new ApplicationActivationManager();
        // const ActivateOptions noFlags = ActivateOptions.None;
        await Task.Run(() =>
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }
    internal async Task Launch()
    {
        if (string.IsNullOrEmpty(_app.ExePath))
        {
            await StartApp(_app.UserModelId);
        }
        else
        {
            await StartExe(_app.ExePath);
        }
    }
    public override ActionResult Invoke()
    {
        _ = Launch();
        return ActionResult.GoHome();
    }
}

internal sealed class AppListItem : ListItem
{
    private readonly AppItem app;
    public AppListItem(AppItem app) : base(new AppAction(app))
    {
        this.app = app;
        this.Title = app.Name;
        this.Subtitle = app.Subtitle;
        this.Details = new Details() { Title = this.Title, HeroImage = this.Command.Icon, Body="### App" };
        this.Tags = [new Tag() { Text = "App" }];

        if (string.IsNullOrEmpty(app.UserModelId))
        {
            // Win32 exe or other non UWP app
            this._MoreCommands = [
                new CommandContextItem(new OpenPathAction(app.DirPath){ Name = "Open location", Icon=new("\ue838") })
            ];
        }
        else
        {
            // UWP app
            this._MoreCommands = [];
        }
    }
}

public sealed class AllAppsPage : ListPage
{
    private ISection allAppsSection;

    public AllAppsPage()
    {
        StringMatcher.Instance = new StringMatcher();
        this.Name = "All Apps";
        this.Icon = new("\ue71d");
        this.ShowDetails = true;
        this.Loading = true;
        this.PlaceholderText = "Search installed apps...";
    }

    public override ISection[] GetItems()
    {
        if (this.allAppsSection == null)
        {
            PopulateApps();
        }
        return [ this.allAppsSection ];
    }
    private void PopulateApps()
    {
        var apps = GetPrograms();
        this.Loading = false;
        this.allAppsSection = new ListSection()
        {
            Title = "Apps",
            Items = apps
                        .Select((app) => new AppListItem(app))
                        .ToArray()
        };
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
        // This also doesn't include Packaged apps, cause they're enumerated entirely seperately.

        var cache = AppCache.Instance.Value;
        var uwps = cache.UWPs;
        var win32s = cache.Win32s;
        var uwpResults = uwps
            .Where((application)=>application.Enabled /*&& application.Valid*/)
            .Select(app =>
                new AppItem
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    IcoPath = app.LogoType != LogoType.Error? app.LogoPath : "",
                    //ExePath = app.FullPath,
                    DirPath = app.Location,
                    UserModelId = app.UserModelId,
                });
        var win32Results = win32s
            .Where((application) => application.Enabled /*&& application.Valid*/)
            .Select(app =>
                new AppItem
                {
                    Name = app.Name,
                    Subtitle = app.Description,
                    IcoPath = app.FullPath, // similarly, this should be IcoPath, but :shrug:
                    ExePath = app.FullPath,
                    DirPath = app.Location,
                });

        return uwpResults.Concat(win32Results).OrderBy(app=>app.Name).ToList();
    }
}

//internal sealed class AppAndScore
//{
//    public AppItem app;
//    public int score;
//}

//internal sealed class AppSearchState
//{
//    public string Query { get; set; } = "";
//    public List<AppAndScore> Results { get; set; } = new();
//}
