// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Microsoft.Windows.CommandPalette.Extensions;
using System.Diagnostics;
using System.Xml.Linq;
using System.Net.Http;
using Windows.Foundation.Collections;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using System.Transactions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Globalization;

namespace GitHubExtension;

sealed class RepositoryInfo
{
    public string name { get; set; }
    public string nameWithOwner { get; set; }
}
sealed class GitHubIssue
{
    public string title { get; set; }
    public int number { get; set; }
    public string url { get; set; }
    public string state { get; set; }
    public string body { get; set; }
    // public RepositoryInfo repository { get; set; }
}

sealed class OpenIssueAction : InvokableCommand
{
    private readonly GitHubIssue issue;
    public OpenIssueAction(GitHubIssue issue)
    {
        this.issue = issue;
        Name = "Open";
        Icon = new("\uE8A7");
    }
    public override ICommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(issue.url) { UseShellExecute = true });
        return ActionResult.KeepOpen();
    }
}

sealed class ViewIssueAction : MarkdownPage {

    static readonly string OpenImagePath = "https://github.com/user-attachments/assets/e4144bc6-91dc-4de8-acad-5ddf4e574edf";
    static readonly string ClosedImagePath = "https://github.com/user-attachments/assets/b8cfaa2e-5407-4594-a50f-7f252d8f6baf";

    private readonly GitHubIssue issue;

    public ViewIssueAction(GitHubIssue issue)
    {
        this.issue = issue;
        Name = "View";
        this.Title = issue.title;
        Icon = new(issue.state == "open" ? OpenImagePath : ClosedImagePath);
    }
    public override string[] Bodies()
    {
        return [issue.body];
    }
}

sealed class GithubIssuesPage : ListPage {

    public GithubIssuesPage()
    {
        this.Icon = new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/ae/Github-desktop-logo-symbol.svg/240px-Github-desktop-logo-symbol.svg.png");
        this.Name = "GitHub Issues";
        this.ShowDetails = true;
    }

    public override ISection[] GetItems()
    {
        var t = DoGetItems();
        t.ConfigureAwait(false);
        return t.Result;
    }

    private async Task<ISection[]> DoGetItems()
    {
        List<GitHubIssue> items = await GetGitHubIssues();
        this.Loading = false;
        var s = new ListSection(){
                    Title = "Issues",
                    Items = items
                            .Select((issue) => new ListItem(new ViewIssueAction(issue)) {
                                Title=issue.title,
                                Subtitle=issue.number.ToString(CultureInfo.CurrentCulture),
                                Details = new Details() { Body = issue.body },
                                MoreCommands= [
                                    new CommandContextItem(new OpenIssueAction(issue))
                                ]
                            })
                            .ToArray()
        };
        return [ s ] ;
    }

    private static async Task<List<GitHubIssue>> GetGitHubIssues()
    {
        var issues = new List<GitHubIssue>();
        string result;//  = "";
        string errorResult;//  = "";
        try
        {
            var ghPath = @"gh";

            var processInfo = new ProcessStartInfo
            {
                FileName = ghPath,
                Arguments = "search issues --author \"@me\" --limit 50 --json title,number,url,state,body",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) // Set a valid working directory
            };

            using var process = Process.Start(processInfo);
            result = await process.StandardOutput.ReadToEndAsync();
            errorResult = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0) {
                return new List<GitHubIssue> { new() { title = errorResult, number = -1, url = "" } };
            }

            issues = JsonSerializer.Deserialize<List<GitHubIssue>>(result);

        }
        catch (Exception ex)
        {
            return new List<GitHubIssue>
            {
                new() {
                    title = ex.Message,
                    number = -1,
                    url = ex.Message
                }
            };
        }

        return issues;
    }
}

public class GithubActionsProvider : ICommandProvider
{
    public string DisplayName => $"GitHub actions";
    public IconDataType Icon => new("");

    private readonly IListItem[] _Actions = [
        new ListItem(new GithubIssuesPage()),
        //new ListItem(new GitHubNotificationsPage()),
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize


    public IListItem[] TopLevelCommands()
    {
        return _Actions;
    }
}

