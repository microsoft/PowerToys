// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace MastodonExtension;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class MastodonExtensionPage : ListPage
{
    internal static readonly HttpClient Client = new();
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly List<ListItem> _items = [];

    public MastodonExtensionPage()
    {
        Icon = new("https://mastodon.social/packs/media/icons/android-chrome-36x36-4c61fdb42936428af85afdbf8c6a45a8.png");
        Name = "Mastodon";
        ShowDetails = true;
        HasMoreItems = true;
        IsLoading = true;

        // #6364ff
        AccentColor = ColorHelpers.FromRgb(99, 100, 255);
    }

    private void AddPosts(List<MastodonStatus> posts)
    {
        foreach (var p in posts)
        {
            var postItem = new ListItem(new MastodonPostPage(p))
            {
                Title = p.Account.DisplayName, // p.ContentAsPlainText(),
                Subtitle = $"@{p.Account.Username}",
                Icon = new(p.Account.Avatar),

                // *
                Tags = [
                    new Tag()
                    {
                        Icon = new("\ue734"), // FavoriteStar
                        Text = p.Favorites.ToString(CultureInfo.CurrentCulture),
                    },
                    new Tag()
                    {
                        Icon = new("\ue8ee"), // RepeatAll
                        Text = p.Boosts.ToString(CultureInfo.CurrentCulture),
                    },
                ], // */
                Details = new Details()
                {
                    // It was a cool idea to have a single image as the HeroImage, but the scaling is terrible
                    // HeroImage = new(p.MediaAttachments.Count == 1 ? p.MediaAttachments[0].Url : string.Empty),
                    Body = p.ContentAsMarkdown(true, true),
                },
                MoreCommands = [
                    new CommandContextItem(new OpenUrlCommand(p.Url) { Name = "Open on web" }),
                ],
            };
            this._items.Add(postItem);
        }
    }

    public override IListItem[] GetItems()
    {
        if (_items.Count == 0)
        {
            var postsAsync = FetchExplorePage();
            postsAsync.ConfigureAwait(false);
            var posts = postsAsync.Result;
            this.AddPosts(posts);
        }

        return _items
            .ToArray();
    }

    public override void LoadMore()
    {
        this.IsLoading = true;
        ExtensionHost.LogMessage(new LogMessage() { Message = $"Loading 20 posts, starting with {_items.Count}..." });
        var postsAsync = FetchExplorePage(20, this._items.Count);
        postsAsync.ContinueWith((res) =>
        {
            var posts = postsAsync.Result;
            this.AddPosts(posts);
            ExtensionHost.LogMessage(new LogMessage() { Message = $"... got {posts.Count} new posts" });

            this.IsLoading = false;
            this.RaiseItemsChanged(this._items.Count);
        }).ConfigureAwait(false);
    }

    public async Task<List<MastodonStatus>> FetchExplorePage() => await FetchExplorePage(20, 0);

    public async Task<List<MastodonStatus>> FetchExplorePage(int limit, int offset)
    {
        var statuses = new List<MastodonStatus>();

        try
        {
            // Make a GET request to the Mastodon trends API endpoint
            var response = await Client
                .GetAsync($"https://mastodon.social/api/v1/trends/statuses?limit={limit}&offset={offset}");
            response.EnsureSuccessStatusCode();

            // Read and deserialize the response JSON into a list of MastodonStatus objects
            var responseBody = await response.Content.ReadAsStringAsync();
            statuses = JsonSerializer.Deserialize<List<MastodonStatus>>(responseBody, Options);
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }

        IsLoading = false;

        return statuses;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public partial class MastodonExtensionActionsProvider : CommandProvider
{
    public MastodonExtensionActionsProvider()
    {
        DisplayName = "Mastodon extension for cmdpal Commands";
    }

    private readonly ICommandItem[] _actions = [
        new CommandItem(new MastodonExtensionPage()) { Subtitle = "Explore top posts on mastodon.social" },
    ];

    public override ICommandItem[] TopLevelCommands() => _actions;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public partial class MastodonPostForm : Form
{
    private readonly MastodonStatus post;

    public MastodonPostForm(MastodonStatus post)
    {
        this.post = post;
    }

    public override string DataJson()
    {
        return $$"""
{
    "author_display_name": {{JsonSerializer.Serialize(post.Account.DisplayName)}},
    "author_username": {{JsonSerializer.Serialize(post.Account.Username)}},
    "post_content": {{JsonSerializer.Serialize(post.ContentAsMarkdown(false, false))}},
    "author_avatar_url": "{{post.Account.Avatar}}",
    "timestamp": "2017-02-14T06:08:39Z",
    "post_url": "{{post.Url}}"
}
""";
    }

    public override ICommandResult SubmitForm(string payload) => CommandResult.Dismiss();

    public override string TemplateJson()
    {
        var img_block = string.Empty;
        if (post.MediaAttachments.Count > 0)
        {
            img_block = string.Join(',', post.MediaAttachments
                .Select(media => $$""",{"type": "Image","url":"{{media.Url}}","size": "stretch"}""").ToArray());
        }

        return $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
            "type": "Container",
            "items": [
                {
                    "type": "ColumnSet",
                    "columns": [
                        {
                            "type": "Column",
                            "width": "auto",
                            "items": [
                                {
                                    "type": "Image",
                                    "url": "${author_avatar_url}",
                                    "size": "Medium",
                                    "style": "Person"
                                }
                            ]
                        },
                        {
                            "type": "Column",
                            "width": "stretch",
                            "items": [
                                {
                                    "type": "TextBlock",
                                    "weight": "Bolder",
                                    "wrap": true,
                                    "spacing": "small",
                                    "text": "${author_display_name}"
                                },
                                {
                                    "type": "TextBlock",
                                    "weight": "Lighter",
                                    "wrap": true,
                                    "text": "@${author_username}",
                                    "spacing": "Small",
                                    "isSubtle": true,
                                    "size": "Small"
                                }
                            ]
                        }
                    ]
                },
                {
                    "type": "TextBlock",
                    "text": "${post_content}",
                    "wrap": true
                }{{img_block}}
            ]
        }
    ],
    "actions": [
        {
            "type": "Action.OpenUrl",
            "title": "View on Mastodon",
            "url": "${post_url}"
        }
    ]
}
""";
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public partial class MastodonPostPage : FormPage
{
    private readonly MastodonStatus post;

    public MastodonPostPage(MastodonStatus post)
    {
        Name = "View post";
        this.post = post;
    }

    public override IForm[] Forms()
    {
        var postsAsync = GetRepliesAsync();
        postsAsync.ConfigureAwait(false);
        var posts = postsAsync.Result;
        return posts.Select(p => new MastodonPostForm(p)).ToArray();
    }

    private async Task<List<MastodonStatus>> GetRepliesAsync()
    {
        // Start with our post...
        var replies = new List<MastodonStatus>([this.post]);
        try
        {
            // Make a GET request to the Mastodon context API endpoint
            var url = $"https://mastodon.social/api/v1/statuses/{post.Id}/context";
            var response = await MastodonExtensionPage.Client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Read and deserialize the response JSON into a MastodonContext object
            var responseBody = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<MastodonContext>(responseBody, MastodonExtensionPage.Options);

            // Extract the list of replies (descendants)
            if (context?.Descendants != null)
            {
                // Add others if we need them
                replies.AddRange(context.Descendants);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred: {e.Message}");
        }

        return replies;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public class MastodonStatus
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("uri")]
    public string Uri { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("account")]
    public MastodonAccount Account { get; set; }

    [JsonPropertyName("favourites_count")]
    public int Favorites { get; set; }

    [JsonPropertyName("reblogs_count")]
    public int Boosts { get; set; }

    [JsonPropertyName("replies_count")]
    public int Replies { get; set; }

    [JsonPropertyName("media_attachments")]
    public List<MediaAttachment> MediaAttachments { get; set; }

    public string ContentAsPlainText()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(Content);
        var plainTextBuilder = new StringBuilder();
        foreach (var node in doc.DocumentNode.ChildNodes)
        {
            plainTextBuilder.Append(ParseNodeToPlaintext(node));
        }

        return plainTextBuilder.ToString();
    }

    public string ContentAsMarkdown(bool escapeHashtags, bool addMedia)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(Content.Replace("<br>", "\n\n").Replace("<br />", "\n\n"));
        var markdownBuilder = new StringBuilder();
        foreach (var node in doc.DocumentNode.ChildNodes)
        {
            markdownBuilder.Append(ParseNodeToMarkdown(node, escapeHashtags));
        }

        // change this to >1 if you want to try the HeroImage thing
        if (addMedia && MediaAttachments.Count > 0)
        {
            foreach (var mediaAttachment in MediaAttachments)
            {
                // A newline in a img tag blows up the image parser :upside_down:
                var desc = mediaAttachment.Description ?? string.Empty;
                var img = $"\n![{desc.Replace("\n", " ")}]({mediaAttachment.Url})";
                markdownBuilder.Append(img);
            }
        }

        return markdownBuilder.ToString();
    }

    private static string ParseNodeToMarkdown(HtmlNode node, bool escapeHashtags)
    {
        var innerText = escapeHashtags ? node.InnerText.Replace("#", "\\#") : node.InnerText;
        switch (node.Name)
        {
            case "strong":
            case "b":
                return $"**{node.InnerText}**";
            case "em":
            case "i":
                return $"*{node.InnerText}*";
            case "a":
                return $"[{node.InnerText}]({node.GetAttributeValue("href", "#")})";
            case "p":
                return $"{innerText}\n\n";
            case "li":
                return $"{innerText}\n";
            case "#text":
                return innerText;
            default:
                return innerText;  // For unhandled nodes, just return the text.
        }
    }

    private static string ParseNodeToPlaintext(HtmlNode node) => node.InnerText;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public class MastodonAccount
{
    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public class MediaAttachment
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } // e.g., "image", "video", "gifv", etc.

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("preview_url")]
    public string PreviewUrl { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public class MastodonContext
{
    [JsonPropertyName("ancestors")]
    public List<MastodonStatus> Ancestors { get; set; }

    [JsonPropertyName("descendants")]
    public List<MastodonStatus> Descendants { get; set; }
}
