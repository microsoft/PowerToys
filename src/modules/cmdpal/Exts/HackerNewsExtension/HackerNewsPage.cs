// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension;

internal sealed class HackerNewsPage : ListPage
{
    public HackerNewsPage()
    {
        Icon = new("https://news.ycombinator.com/favicon.ico");
        Name = "Hacker News";
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
                    Title = item.Element("title")?.Value ?? string.Empty,
                    Link = item.Element("link")?.Value ?? string.Empty,
                    CommentsLink = item.Element("comments")?.Value ?? string.Empty,
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
                MoreCommands = [new CommandContextItem(new CommentAction(post))],
            }).ToArray(),
        };
        return [s];
    }
}
