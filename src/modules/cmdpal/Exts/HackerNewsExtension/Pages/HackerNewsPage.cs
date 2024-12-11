// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Xml.Linq;
using HackerNewsExtension.Commands;
using HackerNewsExtension.Data;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension;

internal sealed partial class HackerNewsPage : ListPage
{
    public HackerNewsPage()
    {
        Icon = new("https://news.ycombinator.com/favicon.ico");
        Name = "Hacker News";
        AccentColor = ColorHelpers.FromRgb(255, 102, 0);
        IsLoading = true;
        ShowDetails = true;
    }

    private static async Task<List<NewsPost>> GetHackerNewsTopPosts()
    {
        var posts = new List<NewsPost>();

        using (var client = new HttpClient())
        {
            var response = await client.GetStringAsync("https://news.ycombinator.com/rss");
            var xdoc = XDocument.Parse(response);
            var x = xdoc.Descendants("item").First();
            posts = xdoc.Descendants("item")
                .Take(20)
                .Select(item => new NewsPost()
                {
                    Title = item.Element("title")?.Value ?? string.Empty,
                    Link = item.Element("link")?.Value ?? string.Empty,
                    CommentsLink = item.Element("comments")?.Value ?? string.Empty,
                }).ToList();
        }

        return posts;
    }

    public override IListItem[] GetItems()
    {
        try
        {
            IsLoading = true;
            var t = DoGetItems();
            t.ConfigureAwait(false);
            return t.Result;
        }
        catch (Exception ex)
        {
            return [
                new ListItem(new NoOpCommand()) { Title = "Exception getting posts from HN" },
                new ListItem(new NoOpCommand())
                {
                    Title = $"{ex.HResult}",
                    Subtitle = ex.HResult == -2147023174 ? "This is probably zadjii-msft/PowerToys#181" : string.Empty,
                },
                new ListItem(new NoOpCommand())
                {
                    Title = "Stack trace",
                    Details = new Details() { Body = $"```{ex.Source}\n{ex.StackTrace}```" },
                },
            ];
        }
    }

    private async Task<IListItem[]> DoGetItems()
    {
        var items = await GetHackerNewsTopPosts();
        IsLoading = false;
        var s = items.Select((post) => new ListItem(new LinkCommand(post))
        {
            Title = post.Title,
            Subtitle = post.Link,
            MoreCommands = [new CommandContextItem(new CommentCommand(post))],
        }).ToArray();
        return s;
    }
}
