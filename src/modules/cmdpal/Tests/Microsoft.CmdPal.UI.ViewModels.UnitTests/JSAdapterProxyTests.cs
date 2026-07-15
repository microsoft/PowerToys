// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Exercises the JSON-RPC adapters and proxies end to end against an in-memory
/// fake extension driving a real JsonRpcConnection.
/// </summary>
[TestClass]
public class JSAdapterProxyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void TopLevelCommands_MapsTitlesSubtitlesAndIcon()
    {
        using var fake = new JSFakeExtension();
        var topLevelJson =
            """
            [
              { "title": "Alpha", "subtitle": "first", "icon": { "light": { "icon": "\uE700" } } },
              { "title": "Beta", "subtitle": "second" }
            ]
            """;
        fake.OnResult("provider/getTopLevelCommands", topLevelJson);

        var provider = CreateProvider(fake);
        var items = provider.TopLevelCommands();

        Assert.AreEqual(2, items.Length);
        Assert.AreEqual("Alpha", items[0].Title);
        Assert.AreEqual("first", items[0].Subtitle);
        Assert.AreEqual("\uE700", items[0].Icon.Light.Icon);
        Assert.AreEqual("Beta", items[1].Title);
    }

    [TestMethod]
    public void IconPipeline_HandlesGlyphPathBase64AndDataUri()
    {
        using var fake = new JSFakeExtension();
        var base64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        fake.OnRequest("provider/getTopLevelCommands", _ => new JsonArray(
            new JsonObject { ["title"] = "Glyph", ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = "\uE700" } } },
            new JsonObject { ["title"] = "Path", ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = @"C:\images\icon.png" } } },
            new JsonObject { ["title"] = "Base64", ["icon"] = new JsonObject { ["light"] = new JsonObject { ["data"] = base64 } } },
            new JsonObject { ["title"] = "DataUri", ["icon"] = new JsonObject { ["light"] = new JsonObject { ["data"] = "data:image/png;base64," + base64 } } }));

        var provider = CreateProvider(fake);
        var items = provider.TopLevelCommands();

        Assert.AreEqual(4, items.Length);
        Assert.AreEqual("\uE700", items[0].Icon.Light.Icon);
        Assert.AreEqual(@"C:\images\icon.png", items[1].Icon.Light.Icon);
        Assert.IsNotNull(items[2].Icon.Light.Data);
        Assert.IsNotNull(items[3].Icon.Light.Data);
    }

    [TestMethod]
    public void Invoke_MapsAllResultKinds()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "cmd1", "name": "Cmd" }""");

        var provider = CreateProvider(fake);
        var invokable = (IInvokableCommand)provider.GetCommand("cmd1")!;

        AssertKind(fake, invokable, "{ \"Kind\": 0 }", CommandResultKind.Dismiss);
        AssertKind(fake, invokable, "{ \"Kind\": 1 }", CommandResultKind.GoHome);
        AssertKind(fake, invokable, "{ \"Kind\": 2 }", CommandResultKind.GoBack);
        AssertKind(fake, invokable, "{ \"Kind\": 3 }", CommandResultKind.Hide);
        AssertKind(fake, invokable, "{ \"Kind\": 4 }", CommandResultKind.KeepOpen);

        fake.OnResult("command/invoke", """{ "Kind": 5, "Args": { "pageId": "target-page" } }""");
        var goToPage = invokable.Invoke(null);
        Assert.AreEqual(CommandResultKind.GoToPage, goToPage.Kind);
        Assert.AreEqual("target-page", ((IGoToPageArgs)goToPage.Args).PageId);

        fake.OnResult("command/invoke", """{ "Kind": 6, "Args": { "message": "toasted" } }""");
        var toast = invokable.Invoke(null);
        Assert.AreEqual(CommandResultKind.ShowToast, toast.Kind);
        Assert.AreEqual("toasted", ((IToastArgs)toast.Args).Message);

        fake.OnResult("command/invoke", """{ "Kind": 7, "Args": { "title": "Are you sure?" } }""");
        var confirm = invokable.Invoke(null);
        Assert.AreEqual(CommandResultKind.Confirm, confirm.Kind);
        Assert.AreEqual("Are you sure?", ((IConfirmationArgs)confirm.Args).Title);
    }

    [TestMethod]
    public void ListPage_MapsItemsTagsDetailsSectionsSeparatorsAndMoreCommands()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "list1", "pageType": "listPage", "name": "My List" }""");
        var itemsJson =
            """
            {
              "items": [
                {
                  "title": "Item A",
                  "subtitle": "sub",
                  "section": "Sec1",
                  "tags": [ { "text": "tag1" } ],
                  "details": { "title": "DetailTitle", "body": "DetailBody" },
                  "moreCommands": [ { "command": { "id": "c2", "name": "More" }, "title": "MoreCmd" } ]
                },
                { "_isSeparator": true, "title": "---" },
                { "title": "Item B" }
              ]
            }
            """;
        fake.OnResult("listPage/getItems", itemsJson);

        var provider = CreateProvider(fake);
        var page = (IListPage)provider.GetCommand("list1")!;
        var items = page.GetItems();

        Assert.AreEqual(3, items.Length);
        Assert.AreEqual("Item A", items[0].Title);
        Assert.AreEqual("Sec1", items[0].Section);
        Assert.AreEqual(1, items[0].Tags.Length);
        Assert.AreEqual("tag1", items[0].Tags[0].Text);
        Assert.IsNotNull(items[0].Details);
        Assert.AreEqual("DetailTitle", items[0].Details!.Title);
        Assert.AreEqual(1, items[0].MoreCommands.Length);

        // Separator items expose no command.
        Assert.IsNull(items[1].Command);
        Assert.AreEqual("Item B", items[2].Title);
    }

    [TestMethod]
    public void ContextItems_ParseNestedMoreCommandsRecursively()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "nested-list", "pageType": "listPage", "name": "Nested" }""");
        var itemsJson =
            """
            {
              "items": [
                {
                  "title": "Root Item",
                  "moreCommands": [
                    {
                      "command": { "id": "level1", "name": "Level 1" },
                      "title": "Level 1",
                      "moreCommands": [
                        { "command": { "id": "level2", "name": "Level 2" }, "title": "Level 2" }
                      ]
                    }
                  ]
                },
                { "title": "Leaf Item" }
              ]
            }
            """;
        fake.OnResult("listPage/getItems", itemsJson);

        var provider = CreateProvider(fake);
        var page = (IListPage)provider.GetCommand("nested-list")!;
        var items = page.GetItems();

        Assert.AreEqual(2, items.Length);

        // The root item carries a first-level nested command.
        var firstLevel = items[0].MoreCommands;
        Assert.AreEqual(1, firstLevel.Length);
        var firstLevelCommand = (ICommandContextItem)firstLevel[0];
        Assert.AreEqual("Level 1", firstLevelCommand.Title);

        // That first-level command carries its own second-level nested command.
        Assert.AreEqual(1, firstLevelCommand.MoreCommands.Length);
        var secondLevelCommand = (ICommandContextItem)firstLevelCommand.MoreCommands[0];
        Assert.AreEqual("Level 2", secondLevelCommand.Title);
        Assert.AreEqual(0, secondLevelCommand.MoreCommands.Length);

        // The leaf item with no moreCommands yields no children.
        Assert.AreEqual(0, items[1].MoreCommands.Length);
    }

    [TestMethod]
    public async Task DynamicListPage_ForwardsSearchTextAndRaisesItemsChanged()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "dyn1", "pageType": "dynamicListPage", "name": "Dyn" }""");

        string? capturedSearch = null;
        fake.OnRequest("listPage/setSearchText", element =>
        {
            capturedSearch = element.GetProperty("searchText").GetString();
            return null;
        });

        var provider = CreateProvider(fake);
        var page = (IDynamicListPage)provider.GetCommand("dyn1")!;

        var raised = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => raised.TrySetResult(args.TotalItems);

        await Task.Run(() => page.SearchText = "query");
        Assert.AreEqual("query", capturedSearch);

        await fake.PushNotificationAsync(
            "listPage/itemsChanged",
            new JsonObject { ["pageId"] = "dyn1", ["totalItems"] = 7 });

        var total = await raised.Task.WaitAsync(Timeout);
        Assert.AreEqual(7, total);
    }

    [TestMethod]
    public void ContentPage_MapsAllContentTypesDetailsAndCommands()
    {
        using var fake = new JSFakeExtension();
        var commandJson =
            """
            {
              "id": "content1",
              "pageType": "contentPage",
              "name": "Content",
              "details": { "title": "DetailTitle" },
              "commands": [ { "command": { "id": "x", "name": "X" }, "title": "Cmd" } ]
            }
            """;
        fake.OnResult("provider/getCommand", commandJson);
        var contentJson =
            """
            [
              { "type": "markdown", "body": "# Hi" },
              { "type": "plainText", "text": "plain" },
              { "type": "image", "image": { "light": { "icon": "C:\\i.png" } } },
              { "type": "form", "template": { "a": 1 } },
              { "type": "tree", "rootContent": { "type": "markdown", "body": "root" } }
            ]
            """;
        fake.OnResult("contentPage/getContent", contentJson);
        fake.OnResult("form/submit", """{ "Kind": 3 }""");

        var provider = CreateProvider(fake);
        var page = (IContentPage)provider.GetCommand("content1")!;
        var content = page.GetContent();

        Assert.AreEqual(5, content.Length);
        Assert.IsInstanceOfType(content[0], typeof(IMarkdownContent));
        Assert.AreEqual("# Hi", ((IMarkdownContent)content[0]).Body);
        Assert.IsInstanceOfType(content[1], typeof(IPlainTextContent));
        Assert.AreEqual("plain", ((IPlainTextContent)content[1]).Text);
        Assert.IsInstanceOfType(content[2], typeof(IImageContent));
        Assert.IsInstanceOfType(content[3], typeof(IFormContent));
        Assert.IsInstanceOfType(content[4], typeof(ITreeContent));

        Assert.IsNotNull(page.Details);
        Assert.AreEqual("DetailTitle", page.Details!.Title);
        Assert.AreEqual(1, page.Commands.Length);

        var submitResult = ((IFormContent)content[3]).SubmitForm("{}", "{}");
        Assert.AreEqual(CommandResultKind.Hide, submitResult.Kind);
    }

    [TestMethod]
    public async Task FallbackCommands_UpdateDisplayTitleOnPropChanged()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult(
            "provider/getFallbackCommands",
            """[ { "id": "fb1", "displayTitle": "Initial", "title": "T" } ]""");

        var provider = CreateProvider(fake);
        var fallbacks = provider.FallbackCommands();

        Assert.IsNotNull(fallbacks);
        Assert.AreEqual(1, fallbacks!.Length);

        var fallback = fallbacks[0];
        Assert.AreEqual("Initial", fallback.DisplayTitle);

        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fallback.PropChanged += (_, args) =>
        {
            if (args.PropertyName == "DisplayTitle")
            {
                changed.TrySetResult();
            }
        };

        await fake.PushNotificationAsync(
            "command/propChanged",
            new JsonObject
            {
                ["commandId"] = "fb1",
                ["properties"] = new JsonObject { ["displayTitle"] = "Updated" },
            });

        await changed.Task.WaitAsync(Timeout);
        Assert.AreEqual("Updated", fallback.DisplayTitle);
    }

    [TestMethod]
    public void Settings_ExposeSettingsPageFromProvider()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getSettings", """{ "id": "settings-page" }""");

        var provider = CreateProvider(fake);
        var settings = provider.Settings;

        Assert.IsNotNull(settings);
        Assert.IsNotNull(settings!.SettingsPage);
    }

    [TestMethod]
    public void ListPage_ExposesFiltersWithSeparatorAndGridType()
    {
        using var fake = new JSFakeExtension();
        var commandJson =
            """
            {
              "id": "list-fg",
              "pageType": "listPage",
              "name": "Filtered",
              "gridProperties": { "type": "medium", "showTitle": true },
              "filters": {
                "currentFilterId": "all",
                "filters": [
                  { "id": "all", "name": "All" },
                  { "separator": true },
                  { "id": "recent", "name": "Recent" }
                ]
              }
            }
            """;
        fake.OnResult("provider/getCommand", commandJson);

        var provider = CreateProvider(fake);
        var page = (IListPage)provider.GetCommand("list-fg")!;

        Assert.IsInstanceOfType(page.GridProperties, typeof(IMediumGridLayout));

        Assert.IsNotNull(page.Filters);
        var filters = page.Filters!.GetFilters();
        Assert.AreEqual(3, filters.Length);
        Assert.IsInstanceOfType(filters[0], typeof(IFilter));
        Assert.AreEqual("all", ((IFilter)filters[0]).Id);
        Assert.IsInstanceOfType(filters[1], typeof(ISeparatorFilterItem));
        Assert.IsInstanceOfType(filters[2], typeof(IFilter));
        Assert.AreEqual("recent", ((IFilter)filters[2]).Id);
    }

    [TestMethod]
    public async Task FallbackHandler_SendsUpdateQueryAsRequest()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult(
            "provider/getFallbackCommands",
            """[ { "id": "fb-req", "displayTitle": "Initial", "title": "T" } ]""");

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("fallback/updateQuery", element =>
        {
            received.TrySetResult(element.GetProperty("query").GetString() ?? string.Empty);
            return null;
        });

        var provider = CreateProvider(fake);
        var fallback = provider.FallbackCommands()![0];

        await Task.Run(() => fallback.FallbackHandler.UpdateQuery("typed"));

        var query = await received.Task.WaitAsync(Timeout);
        Assert.AreEqual("typed", query);
    }

    private static void AssertKind(JSFakeExtension fake, IInvokableCommand invokable, string resultJson, CommandResultKind expected)
    {
        fake.OnResult("command/invoke", resultJson);
        var result = invokable.Invoke(null);
        Assert.AreEqual(expected, result.Kind);
    }

    private static JSCommandProviderProxy CreateProvider(JSFakeExtension fake) =>
        new(fake.Connection, new JSExtensionManifest { Name = "test.ext", DisplayName = "Test Extension" });
}
