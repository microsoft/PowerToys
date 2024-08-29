// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using ABI.System;
using Microsoft.UI;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace HackerNewsExtension;

internal sealed class NewsPost
{
    internal string Title { get; init; } = "";
    internal string Link { get; init; } = "";
    internal string CommentsLink { get; init; } = "";
    internal string Poster { get; init; } = "";
}
internal sealed class LinkAction : InvokableCommand {
    private readonly NewsPost post;
    internal LinkAction(NewsPost post)
    {
        this.post = post;
        this.Name = "Open link";
        this.Icon = new("\uE8A7");
    }
    public override ActionResult Invoke()
    {
        Process.Start(new ProcessStartInfo(post.Link) { UseShellExecute = true });
        return ActionResult.KeepOpen();
    }
}
internal sealed class CommentAction : InvokableCommand
{
    private readonly NewsPost post;
    internal CommentAction(NewsPost post)
    {
        this.post = post;
        this.Name = "Open comments";
        this.Icon = new("\ue8f2"); // chat bubbles
    }
    public override ActionResult Invoke()
    {
        Process.Start(new ProcessStartInfo(post.CommentsLink) { UseShellExecute = true });
        return ActionResult.KeepOpen();
    }
}

sealed class HackerNewsPage : ListPage {

    public HackerNewsPage()
    {
        this.Icon = new("https://news.ycombinator.com/favicon.ico");
        this.Name = "Hacker News";
    }

    private static async Task<List<NewsPost>> GetHackerNewsTopPosts()
    {
        var posts = new List<NewsPost>();

        using (HttpClient client = new HttpClient())
        {
            var response = await client.GetStringAsync("https://news.ycombinator.com/rss");
            var xdoc = XDocument.Parse(response);
            var x = xdoc.Descendants("item").First();
            posts = xdoc.Descendants("item")
                .Take(20)
                .Select(item => new NewsPost()
                {
                    Title = item.Element("title")?.Value ?? "",
                    Link = item.Element("link")?.Value ?? "",
                    CommentsLink = item.Element("comments")?.Value ?? "",
                }).ToList();
        }

        return posts;
    }

    public override ISection[] GetItems()
    {
        var t = DoGetItems();
        t.ConfigureAwait(false);
        return t.Result;
    }
    private async Task<ISection[]> DoGetItems()
    {
        List<NewsPost> items = await GetHackerNewsTopPosts();
        this.Loading = false;
        var s = new ListSection()
        {
            Title = "Posts",
            Items = items.Select((post) => new ListItem(new LinkAction(post))
            {
                Title = post.Title,
                Subtitle = post.Link,
                MoreCommands = [
                                new CommandContextItem(new CommentAction(post))
                            ]
            }).ToArray()
        };
        return [ s ] ;
    }
}

public class HackerNewsActionsProvider : ICommandProvider
{
    public string DisplayName => $"Hacker News Commands";
    public IconDataType Icon => new("");

    private readonly IListItem[] _Actions = [
        new ListItem(new HackerNewsPage()),
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize


    public IListItem[] TopLevelCommands()
    {
        return _Actions;
    }
}
