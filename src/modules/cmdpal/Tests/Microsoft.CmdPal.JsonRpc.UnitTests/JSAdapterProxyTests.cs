// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.JsonRpc.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.JsonRpc.UnitTests;

/// <summary>
/// Exercises JSON-RPC adapters and proxies against an in-memory fake extension
/// running through a real JsonRpcConnection.
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
    public async Task ItemAdapters_ApplyCanonicalPropertyChanges()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult(
            "provider/getTopLevelCommands",
            """[ { "id": "top-item", "title": "Old top", "command": { "id": "top-item", "name": "Top" } } ]""");
        fake.OnResult(
            "provider/getCommand",
            """{ "id": "list-page", "pageType": "listPage", "name": "List" }""");
        fake.OnResult(
            "listPage/getItems",
            """[ { "id": "list-item", "title": "Old list", "command": { "id": "list-item", "name": "List item" } } ]""");

        var provider = CreateProvider(fake);
        var topItem = provider.TopLevelCommands()[0];
        var listItem = ((IListPage)provider.GetCommand("list-page")!).GetItems()[0];
        var topChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        topItem.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ICommandItem.Title))
            {
                topChanged.TrySetResult();
            }
        };
        listItem.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IListItem.Title))
            {
                listChanged.TrySetResult();
            }
        };

        await fake.PushNotificationAsync(
            "command/propChanged",
            new JsonObject
            {
                ["commandId"] = "top-item",
                ["properties"] = new JsonObject
                {
                    ["title"] = "New top",
                    ["command"] = new JsonObject { ["id"] = "replacement", ["name"] = "Replacement" },
                },
            });
        await fake.PushNotificationAsync(
            "command/propChanged",
            new JsonObject
            {
                ["commandId"] = "list-item",
                ["properties"] = new JsonObject { ["title"] = "New list" },
            });

        await Task.WhenAll(topChanged.Task, listChanged.Task).WaitAsync(Timeout);
        Assert.AreEqual("New top", topItem.Title);
        Assert.AreEqual("replacement", topItem.Command!.Id);
        Assert.AreEqual("New list", listItem.Title);
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

        AssertKind(fake, invokable, "{ \"kind\": 0 }", CommandResultKind.Dismiss);
        AssertKind(fake, invokable, "{ \"kind\": 1 }", CommandResultKind.GoHome);
        AssertKind(fake, invokable, "{ \"kind\": 2 }", CommandResultKind.GoBack);
        AssertKind(fake, invokable, "{ \"kind\": 3 }", CommandResultKind.Hide);
        AssertKind(fake, invokable, "{ \"kind\": 4 }", CommandResultKind.KeepOpen);

        fake.OnResult("command/invoke", """{ "kind": 5, "args": { "pageId": "target-page" } }""");
        var goToPage = invokable.Invoke(null);
        Assert.AreEqual(CommandResultKind.GoToPage, goToPage.Kind);
        Assert.AreEqual("target-page", ((IGoToPageArgs)goToPage.Args).PageId);

        fake.OnResult(
            "command/invoke",
            """{ "kind": 6, "args": { "message": "toasted", "icon": { "light": { "icon": "\uE700" } }, "command": { "id": "undo", "name": "Undo" } } }""");
        var toast = invokable.Invoke(null);
        Assert.AreEqual(CommandResultKind.ShowToast, toast.Kind);
        Assert.AreEqual("toasted", ((IToastArgs)toast.Args).Message);
        var toastArgs2 = (IToastArgs2)toast.Args;
        Assert.AreEqual("\uE700", toastArgs2.Icon.Light.Icon);
        Assert.AreEqual("undo", toastArgs2.Command.Id);
        Assert.AreEqual("Undo", toastArgs2.Command.Name);

        fake.OnResult("command/invoke", """{ "kind": 7, "args": { "title": "Are you sure?" } }""");
        var confirm = invokable.Invoke(null);
        Assert.AreEqual(CommandResultKind.Confirm, confirm.Kind);
        Assert.AreEqual("Are you sure?", ((IConfirmationArgs)confirm.Args).Title);
    }

    [TestMethod]
    public void GetCommandItem_MapsFullCommandItem()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult(
            "provider/getCommandItem",
            """{ "id": "pinned", "title": "Pinned", "subtitle": "From anywhere", "command": { "id": "pinned", "name": "Pinned" }, "moreCommands": [] }""");

        var provider = CreateProvider(fake);
        var item = ((ICommandProvider4)provider).GetCommandItem("pinned");

        Assert.IsNotNull(item);
        Assert.AreEqual("Pinned", item.Title);
        Assert.AreEqual("From anywhere", item.Subtitle);
        Assert.IsNotNull(item.Command);
        Assert.AreEqual("pinned", item.Command.Id);
        Assert.AreEqual("Pinned", item.Command.Name);
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

        // Separator items have no command.
        Assert.IsNull(items[1].Command);
        Assert.AreEqual("Item B", items[2].Title);
    }

    [TestMethod]
    public void ListPage_MapsDetailsSizeNamesIgnoringCaseAndNumericLeniency()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "list1", "pageType": "listPage", "name": "My List" }""");
        var itemsJson =
            """
            {
              "items": [
                { "title": "Large by name", "details": { "title": "A", "size": "large" } },
                { "title": "Medium mixed case", "details": { "title": "B", "size": "MeDiUm" } },
                { "title": "Small upper case", "details": { "title": "C", "size": "SMALL" } },
                { "title": "Small by number", "details": { "title": "D", "size": 0 } },
                { "title": "Medium by number", "details": { "title": "E", "size": 1 } },
                { "title": "Large by number", "details": { "title": "F", "size": 2 } },
                { "title": "Default size", "details": { "title": "G" } },
                { "title": "Unknown name", "details": { "title": "H", "size": "extra-large" } },
                { "title": "Unknown number", "details": { "title": "I", "size": 99 } }
              ]
            }
            """;
        fake.OnResult("listPage/getItems", itemsJson);

        var provider = CreateProvider(fake);
        var page = (IListPage)provider.GetCommand("list1")!;
        var items = page.GetItems();

        Assert.AreEqual((int)ContentSize.Large, GetDetailsSize(items[0].Details));
        Assert.AreEqual((int)ContentSize.Medium, GetDetailsSize(items[1].Details));
        Assert.AreEqual((int)ContentSize.Small, GetDetailsSize(items[2].Details));
        Assert.AreEqual((int)ContentSize.Small, GetDetailsSize(items[3].Details));
        Assert.AreEqual((int)ContentSize.Medium, GetDetailsSize(items[4].Details));
        Assert.AreEqual((int)ContentSize.Large, GetDetailsSize(items[5].Details));
        Assert.AreEqual((int)ContentSize.Small, GetDetailsSize(items[6].Details));
        Assert.AreEqual((int)ContentSize.Small, GetDetailsSize(items[7].Details));
        Assert.AreEqual((int)ContentSize.Small, GetDetailsSize(items[8].Details));
    }

    [TestMethod]
    public void ParseContentSize_BoundsMalformedValueDiagnosticPreview()
    {
        var oversizedValue = new string('x', JSModelMapper.JsonDiagnosticPreviewMaxLength * 4);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { size = oversizedValue }));
        var sizeElement = document.RootElement.GetProperty("size");

        var preview = JSModelMapper.GetBoundedJsonPreview(sizeElement);
        var size = JSModelMapper.ParseContentSize(document.RootElement, "size");

        Assert.AreEqual(JSModelMapper.JsonDiagnosticPreviewMaxLength + 3, preview.Length);
        Assert.IsTrue(preview.EndsWith("...", StringComparison.Ordinal));
        Assert.AreEqual(ContentSize.Small, size);
        Assert.AreEqual("<undefined>", JSModelMapper.GetBoundedJsonPreview(default));
    }

    [TestMethod]
    public void ListPage_DetailsCommandInvokeSendsCommandInvokeWithId()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "list1", "pageType": "listPage", "name": "My List" }""");
        var itemsJson =
            """
            {
              "items": [
                {
                  "title": "Item with detail buttons",
                  "details": {
                    "title": "Detail",
                    "metadata": [
                      {
                        "key": "actions",
                        "data": {
                          "type": "commands",
                          "commands": [
                            { "id": "details-cmd-1", "name": "Do It" }
                          ]
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;
        fake.OnResult("listPage/getItems", itemsJson);

        string? invokedCommandId = null;
        fake.OnRequest("command/invoke", element =>
        {
            invokedCommandId = element.GetProperty("commandId").GetString();
            return new JsonObject { ["kind"] = 4 };
        });

        var provider = CreateProvider(fake);
        var page = (IListPage)provider.GetCommand("list1")!;
        var items = page.GetItems();

        var details = items[0].Details;
        Assert.IsNotNull(details, "The list item should carry details.");

        var commandsElement = Array.Find(
            details!.Metadata,
            e => e.Data is IDetailsCommands);
        Assert.IsNotNull(commandsElement, "The details metadata should include a commands element.");

        var detailsCommands = (IDetailsCommands)commandsElement!.Data!;
        Assert.AreEqual(1, detailsCommands.Commands.Length);

        var invokable = (IInvokableCommand)detailsCommands.Commands[0];
        Assert.AreEqual("details-cmd-1", invokable.Id);

        var result = invokable.Invoke(null);

        Assert.AreEqual("details-cmd-1", invokedCommandId, "Invoking a details command should send command/invoke with the command id.");
        Assert.AreEqual(CommandResultKind.KeepOpen, result.Kind);
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

        // The root item has the first nested command.
        var firstLevel = items[0].MoreCommands;
        Assert.AreEqual(1, firstLevel.Length);
        var firstLevelCommand = (ICommandContextItem)firstLevel[0];
        Assert.AreEqual("Level 1", firstLevelCommand.Title);

        // That command has a second nested command.
        Assert.AreEqual(1, firstLevelCommand.MoreCommands.Length);
        var secondLevelCommand = (ICommandContextItem)firstLevelCommand.MoreCommands[0];
        Assert.AreEqual("Level 2", secondLevelCommand.Title);
        Assert.AreEqual(0, secondLevelCommand.MoreCommands.Length);

        // The leaf item has no moreCommands, so it yields no children.
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
        fake.OnResult("form/submit", """{ "kind": 3 }""");

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
    public async Task ContentPage_NotificationRaisesItemsChangedAndRefreshesContent()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult("provider/getCommand", """{ "id": "content-refresh", "pageType": "contentPage", "name": "Content" }""");

        var contentBody = "initial";
        fake.OnRequest("contentPage/getContent", _ => new JsonArray(
            new JsonObject { ["type"] = "plainText", ["text"] = contentBody }));

        var provider = CreateProvider(fake);
        var page = (IContentPage)provider.GetCommand("content-refresh")!;
        var initial = (IPlainTextContent)page.GetContent()[0];
        Assert.AreEqual("initial", initial.Text);

        var raised = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => raised.TrySetResult(args.TotalItems);

        contentBody = "updated";
        await fake.PushNotificationAsync(
            "contentPage/itemsChanged",
            new JsonObject { ["pageId"] = "content-refresh" });

        Assert.AreEqual(-1, await raised.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        var refreshed = (IPlainTextContent)page.GetContent()[0];
        Assert.AreEqual("updated", refreshed.Text);
    }

    [TestMethod]
    public async Task FallbackCommands_UpdateDisplayTitleOnPropChanged()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult(
            "provider/getFallbackCommands",
            """[ { "id": "fallback-item", "displayTitle": "Initial", "title": "T", "command": { "id": "fb1", "name": "Fallback" } } ]""");

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
                ["properties"] = new JsonObject
                {
                    ["displayTitle"] = "Updated",
                    ["title"] = "Updated title",
                },
            });

        await changed.Task.WaitAsync(Timeout);
        Assert.AreEqual("Updated", fallback.DisplayTitle);
        Assert.AreEqual("Updated title", fallback.Title);
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
              "gridProperties": { "type": "MeDiUm", "showTitle": true },
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
            """[ { "id": "fallback-item", "displayTitle": "Initial", "title": "T", "command": { "id": "fallback-command", "name": "T" } } ]""");

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("fallback/updateQuery", element =>
        {
            received.TrySetResult(
                $"{element.GetProperty("commandId").GetString()}|{element.GetProperty("query").GetString()}");
            return null;
        });

        var provider = CreateProvider(fake);
        var fallback = provider.FallbackCommands()![0];

        Assert.IsInstanceOfType(fallback, typeof(IFallbackCommandItem2));
        Assert.AreEqual("fallback-item", ((IFallbackCommandItem2)fallback).Id);

        await Task.Run(() => fallback.FallbackHandler.UpdateQuery("typed"));

        var request = await received.Task.WaitAsync(Timeout);
        Assert.AreEqual("fallback-command|typed", request);
    }

    [TestMethod]
    public async Task CommandPropChanged_UpdatesPageStateAndRaisesAbiProperty()
    {
        using var fake = new JSFakeExtension();
        fake.OnResult(
            "provider/getCommand",
            """{ "id": "live-page", "pageType": "listPage", "name": "Page", "title": "Old", "isLoading": false }""");

        var provider = CreateProvider(fake);
        var page = (IListPage)provider.GetCommand("live-page")!;
        var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((INotifyPropChanged)page).PropChanged += (_, args) => changed.TrySetResult(args.PropertyName);

        await fake.PushNotificationAsync(
            "command/propChanged",
            new JsonObject
            {
                ["commandId"] = "live-page",
                ["properties"] = new JsonObject
                {
                    ["isLoading"] = true,
                    ["title"] = "New",
                },
            });

        Assert.AreEqual("IsLoading", await changed.Task.WaitAsync(Timeout));
        Assert.IsTrue(page.IsLoading);
        Assert.AreEqual("New", page.Title);
    }

    private static void AssertKind(JSFakeExtension fake, IInvokableCommand invokable, string resultJson, CommandResultKind expected)
    {
        fake.OnResult("command/invoke", resultJson);
        var result = invokable.Invoke(null);
        Assert.AreEqual(expected, result.Kind);
    }

    private static int GetDetailsSize(IDetails? details)
    {
        Assert.IsNotNull(details);
        var provider = details as IExtendedAttributesProvider;
        Assert.IsNotNull(provider, "Details should expose extended attributes for its size.");
        var properties = provider!.GetProperties();
        Assert.IsNotNull(properties);
        Assert.IsTrue(properties!.TryGetValue("Size", out var size));
        return (int)size!;
    }

    private static JSCommandProviderProxy CreateProvider(JSFakeExtension fake) =>
        new(fake.Connection, "test.ext", "Test Extension");
}
