// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;

namespace VirtualDesktopExtension;

internal sealed partial class VirtualDesktopExtensionPage : ListPage
{
    public VirtualDesktopExtensionPage()
    {
        Icon = new(string.Empty);
        Name = "Virtual Desktops";
        Id = "com.zadjii.VirtualDesktopsList";
    }

    public override IListItem[] GetItems()
    {
        var desktopsTask = GetDesktopsAsync();
        desktopsTask.ConfigureAwait(false);
        var desktops = desktopsTask.Result;

        if (desktops.Count == 0)
        {
            return [
                new ListItem(new NoOpCommand()) { Title = "Failed to load the list of desktops" }
            ];
        }

        var items = new List<ListItem>();
        foreach (var d in desktops)
        {
            var command = new SwitchToDesktopCommand(d);
            command.SwitchDesktopRequested += HandleSwitchDesktop;
            var li = new ListItem(command)
            {
                Title = d.Name,
                Subtitle = $"Desktop {d.Index + 1}",
                Icon = new(d.Wallpaper),
                Tags = d.IsVisible ? [new Tag() { Text = "Current" }] : [],
            };
            items.Add(li);
        }

        return items.ToArray();
    }

    private void HandleSwitchDesktop(object sender, object args)
    {
        this.SearchText = string.Empty;
    }

    private async Task<List<Desktop>> GetDesktopsAsync()
    {
        try
        {
            var exePath = GetExePath();

            var processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "/li",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), // Set a valid working directory
            };
            using var process = Process.Start(processInfo);
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            return ParseDesktops(output);
        }
        catch (Exception)
        {
            return new();
        }
    }

    public static string GetExePath()
    {
        return "VirtualDesktop11.exe";
    }

    private static List<Desktop> ParseDesktops(string output)
    {
        var desktops = new List<Desktop>();

        // Regex to match the desktop details
        var pattern = @"(?<Name>[^\(]+)\s*(?<Visible>\(visible\))?\s*\(Wallpaper:\s(?<Wallpaper>.+?)\)";

        var lines = output.Split('\n');
        for (var i = 2; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = Regex.Match(line, pattern);

            // If the line matches the desktop pattern, parse it
            if (match.Success)
            {
                desktops.Add(new Desktop
                {
                    Name = match.Groups["Name"].Value.Trim(),
                    Wallpaper = match.Groups["Wallpaper"].Value,
                    IsVisible = match.Groups["Visible"].Success,
                    Index = i - 2,
                });
            }
        }

        return desktops;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public sealed class Desktop
{
    public string Name { get; set; }

    public string Wallpaper { get; set; }

    public bool IsVisible { get; set; }

    public int Index { get; set; }

    public override string ToString()
    {
        return $"Name: {Name}, Wallpaper: {Wallpaper}, IsVisible: {IsVisible}";
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public sealed partial class SwitchToDesktopCommand : InvokableCommand
{
    private readonly Desktop _desktop;

    public event TypedEventHandler<object, object> SwitchDesktopRequested;

    public SwitchToDesktopCommand(Desktop desktop)
    {
        _desktop = desktop;
        Name = "Switch";
    }

    public override ICommandResult Invoke()
    {
        var exePath = VirtualDesktopExtensionPage.GetExePath();
        var processInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"/s:{_desktop.Index}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), // Set a valid working directory
        };
        using var process = Process.Start(processInfo);
        SwitchDesktopRequested?.Invoke(this, null);
        return CommandResult.KeepOpen();
    }
}
