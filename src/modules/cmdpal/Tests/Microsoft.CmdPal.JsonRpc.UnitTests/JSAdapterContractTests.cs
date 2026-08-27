// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.JsonRpc.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.JsonRpc.UnitTests;

/// <summary>
/// Parser assertions use shared TS SDK wire fixtures so the C# adapters match the SDK.
/// </summary>
[TestClass]
public partial class JSAdapterTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    // A page with multiple forms submits each one with its own formId.
    [TestMethod]
    public async Task Form_MultipleFormsSubmitWithTheirOwnFormId()
    {
        using var fake = new JSFakeExtension();
        var captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("form/submit", element =>
        {
            captured.TrySetResult(element.GetProperty("formId").GetString() ?? string.Empty);
            return JsonNode.Parse("""{ "kind": 4 }""");
        });

        var firstForm = new JSFormContentProxy("page-1", Fixture("content-form.json"), fake.Connection);
        var firstId = await SubmitAndReadFormId(firstForm, captured);
        Assert.AreEqual("form-0", firstId);

        var secondData = ParseElement(new JsonObject
        {
            ["type"] = "form",
            ["formId"] = "form-1",
            ["templateJson"] = "{}",
            ["dataJson"] = "{}",
        });
        captured = ResetCapture(fake);
        var secondForm = new JSFormContentProxy("page-1", secondData, fake.Connection);
        var secondId = await SubmitAndReadFormId(secondForm, captured);
        Assert.AreEqual("form-1", secondId);
    }

    // A form nested inside tree content submits with its own formId.
    [TestMethod]
    public async Task Form_NestedFormSubmitsWithItsOwnFormId()
    {
        using var fake = new JSFakeExtension();
        var captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("form/submit", element =>
        {
            captured.TrySetResult(element.GetProperty("formId").GetString() ?? string.Empty);
            return JsonNode.Parse("""{ "kind": 4 }""");
        });

        var tree = Fixture("content-tree-nested-form.json");
        var nestedForm = tree.GetProperty("children")[0].Clone();
        var proxy = new JSFormContentProxy("page-1", nestedForm, fake.Connection);

        var formId = await SubmitAndReadFormId(proxy, captured);
        Assert.AreEqual("child-form", formId);
    }

    // A toast result preserves its nested continuation result.
    [TestMethod]
    public void Toast_PreservesNestedContinuationResult()
    {
        var element = Fixture("command-result-showToast-nested.json");
        var result = JSCommandResultParser.ParseCommandResult(element, null);

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toastArgs = (IToastArgs)result.Args;
        Assert.AreEqual("Saved", toastArgs.Message);
        Assert.IsNotNull(toastArgs.Result);
        Assert.AreEqual(CommandResultKind.GoHome, toastArgs.Result!.Kind);
    }

    [TestMethod]
    public void Toast_ParsesIconAndGracefullyOmitsActionWithoutConnection()
    {
        using var document = JsonDocument.Parse(
            """{ "kind": 6, "args": { "message": "Saved", "icon": { "light": { "icon": "\uE700" } }, "command": { "id": "undo", "name": "Undo" } } }""");

        var result = JSCommandResultParser.ParseCommandResult(document.RootElement, null);
        var toastArgs = (IToastArgs2)result.Args;

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual("Saved", toastArgs.Message);
        Assert.AreEqual("\uE700", toastArgs.Icon.Light.Icon);
        Assert.IsNull(toastArgs.Command);
    }

    // Two references to the same pageId both receive items changed.
    [TestMethod]
    public async Task ListPage_DuplicatePageReferencesBothReceiveNotifications()
    {
        using var fake = new JSFakeExtension();
        var first = new JSListPageProxy("shared-page", fake.Connection);
        var second = new JSListPageProxy("shared-page", fake.Connection);

        var firstRaised = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRaised = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.ItemsChanged += (_, args) => firstRaised.TrySetResult(args.TotalItems);
        second.ItemsChanged += (_, args) => secondRaised.TrySetResult(args.TotalItems);

        await fake.PushNotificationAsync(
            "listPage/itemsChanged",
            new JsonObject { ["pageId"] = "shared-page", ["totalItems"] = 5 });

        var firstTotal = await firstRaised.Task.WaitAsync(Timeout);
        var secondTotal = await secondRaised.Task.WaitAsync(Timeout);
        Assert.AreEqual(5, firstTotal);
        Assert.AreEqual(5, secondTotal);

        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    // HasMoreItems flips true to false at the last page and no further
    // LoadMore is issued once the extension reports the final page.
    [TestMethod]
    public void ListPage_StopsLoadingMoreAtTheFinalPage()
    {
        using var fake = new JSFakeExtension();
        var loadMoreCount = 0;
        fake.OnRequest("listPage/getItems", _ => new JsonObject
        {
            ["items"] = new JsonArray(),
            ["hasMoreItems"] = true,
        });
        fake.OnRequest("listPage/loadMore", _ =>
        {
            Interlocked.Increment(ref loadMoreCount);
            return new JsonObject { ["hasMoreItems"] = false };
        });

        var page = new JSListPageProxy("pager", fake.Connection);

        page.GetItems();
        Assert.IsTrue(page.HasMoreItems);

        page.LoadMore();
        Assert.IsFalse(page.HasMoreItems);

        page.LoadMore();
        Assert.AreEqual(1, loadMoreCount);
    }

    // The settings page exposes full metadata, not just id.
    [TestMethod]
    public void Settings_ExposesFullPageMetadata()
    {
        using var fake = new JSFakeExtension();
        var settingsJson =
            """
            {
              "id": "settings-page",
              "name": "SettingsName",
              "title": "SettingsTitle",
              "icon": { "light": { "icon": "\uE713" } },
              "commands": [ { "command": { "id": "s", "name": "S" }, "title": "SettingsCommand" } ]
            }
            """;
        fake.OnResult("provider/getSettings", settingsJson);

        var provider = CreateProvider(fake);
        var settingsPage = provider.Settings!.SettingsPage;

        Assert.AreEqual("SettingsName", settingsPage.Name);
        Assert.AreEqual("SettingsTitle", settingsPage.Title);
        Assert.AreEqual("\uE713", settingsPage.Icon.Light.Icon);
        Assert.AreEqual(1, settingsPage.Commands.Length);
    }

    // A valid requested shortcut maps to a KeyChord.
    [TestMethod]
    public void ContextItem_ValidRequestedShortcutMapsToKeyChord()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["command"] = new JsonObject { ["id"] = "c", ["name"] = "C" },
            ["title"] = "Shortcut Item",
            ["requestedShortcut"] = JsonNode.Parse(FixtureText("keychord.json")),
        });

        var item = (CommandContextItem)JSModelMapper.ParseContextItem(element, fake.Connection);

        Assert.AreEqual(VirtualKeyModifiers.Control, item.RequestedShortcut.Modifiers);
        Assert.AreEqual(65, item.RequestedShortcut.Vkey);
        Assert.AreEqual(30, item.RequestedShortcut.ScanCode);
    }

    // A malformed shortcut yields no shortcut and does not throw.
    [TestMethod]
    public void ContextItem_MalformedRequestedShortcutYieldsNoShortcut()
    {
        using var fake = new JSFakeExtension();

        var missingVkey = ParseElement(new JsonObject
        {
            ["command"] = new JsonObject { ["id"] = "c", ["name"] = "C" },
            ["requestedShortcut"] = new JsonObject { ["modifiers"] = 1 },
        });
        var missingItem = (CommandContextItem)JSModelMapper.ParseContextItem(missingVkey, fake.Connection);
        Assert.AreEqual(0, missingItem.RequestedShortcut.Vkey);

        var wrongShape = ParseElement(new JsonObject
        {
            ["command"] = new JsonObject { ["id"] = "c", ["name"] = "C" },
            ["requestedShortcut"] = "not-an-object",
        });
        var wrongItem = (CommandContextItem)JSModelMapper.ParseContextItem(wrongShape, fake.Connection);
        Assert.AreEqual(0, wrongItem.RequestedShortcut.Vkey);
    }

    // An absent item icon falls back to the command's icon.
    [TestMethod]
    public void Icon_AbsentItemIconFallsBackToCommandIcon()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "No Icon Item",
            ["command"] = new JsonObject
            {
                ["id"] = "c",
                ["name"] = "C",
                ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = "CMDICON" } },
            },
        });

        var adapter = new JSListItemAdapter(element, fake.Connection);
        Assert.AreEqual("CMDICON", adapter.Icon.Light.Icon);
    }

    // An explicitly empty item icon stays empty and does not fall back.
    [TestMethod]
    public void Icon_ExplicitlyEmptyItemIconStaysEmpty()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Empty Icon Item",
            ["icon"] = new JsonObject
            {
                ["light"] = new JsonObject { ["icon"] = string.Empty },
                ["dark"] = new JsonObject { ["icon"] = string.Empty },
            },
            ["command"] = new JsonObject
            {
                ["id"] = "c",
                ["name"] = "C",
                ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = "CMDICON" } },
            },
        });

        var adapter = new JSListItemAdapter(element, fake.Connection);
        Assert.IsTrue(string.IsNullOrEmpty(adapter.Icon.Light.Icon));
        Assert.AreNotEqual("CMDICON", adapter.Icon.Light.Icon);
    }

    // Light and dark icon variants both match the shared fixture.
    [TestMethod]
    public void Icon_LightAndDarkVariantsFromFixture()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Themed Icon Item",
            ["icon"] = JsonNode.Parse(FixtureText("icon-light-dark.json")),
            ["command"] = new JsonObject { ["id"] = "c", ["name"] = "C" },
        });

        var adapter = new JSListItemAdapter(element, fake.Connection);
        Assert.AreEqual("\uE706", adapter.Icon.Light.Icon);
        Assert.AreEqual("\uE708", adapter.Icon.Dark.Icon);
    }

    // Showing then updating the same statusId refreshes in place without
    // creating a duplicate, and preserves the reported severity and progress.
    [TestMethod]
    public async Task Status_UpdateWithSameIdDoesNotDuplicate()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);
        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        await fake.PushNotificationAsync("host/showStatus", ParseNode(FixtureText("status-show.json")));
        await WaitForAsync(() => host.ShownCount == 1);
        Assert.AreEqual("Working", host.ShownStatuses[0].Message);
        Assert.AreEqual(MessageState.Info, host.ShownStatuses[0].State);
        Assert.IsNotNull(host.ShownStatuses[0].Progress);
        Assert.IsTrue(host.ShownStatuses[0].Progress!.IsIndeterminate);

        await fake.PushNotificationAsync("host/showStatus", ParseNode(FixtureText("status-update.json")));
        await WaitForAsync(() => host.ShownStatuses[0].Message == "Almost done");

        Assert.AreEqual(1, host.ShownCount);
        Assert.AreEqual(MessageState.Success, host.ShownStatuses[0].State);
        Assert.AreEqual(80u, host.ShownStatuses[0].Progress!.ProgressPercent);
        Assert.IsFalse(host.ShownStatuses[0].Progress!.IsIndeterminate);
    }

    // A non-info status hides by its statusId.
    [TestMethod]
    public async Task Status_NonInfoStatusHidesById()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);
        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        await fake.PushNotificationAsync("host/showStatus", ParseNode(FixtureText("status-update.json")));
        await WaitForAsync(() => host.ShownCount == 1);
        Assert.AreEqual(MessageState.Success, host.ShownStatuses[0].State);

        await fake.PushNotificationAsync("host/hideStatus", ParseNode(FixtureText("status-hide.json")));
        await WaitForAsync(() => host.HiddenCount == 1);

        Assert.AreSame(host.ShownStatuses[0], host.HiddenStatuses[0]);
    }

    // After dispose, a late notification is ignored and the active status
    // is hidden.
    [TestMethod]
    public async Task Dispose_IgnoresLateNotificationsAndHidesActiveStatus()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);
        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        await fake.PushNotificationAsync("host/showStatus", ParseNode(FixtureText("status-show.json")));
        await WaitForAsync(() => host.ShownCount == 1);

        provider.Dispose();
        await WaitForAsync(() => host.HiddenCount == 1);
        Assert.AreSame(host.ShownStatuses[0], host.HiddenStatuses[0]);

        // A notification that arrives after dispose is dropped: the handler has
        // been detached and the disposed guard ignores it.
        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["statusId"] = "status-late",
                ["message"] = new JsonObject { ["message"] = "Too late", ["state"] = 0 },
            });

        await Task.Delay(300);
        Assert.AreEqual(1, host.ShownCount);
    }

    // Frozen and non-frozen providers return their actual value.
    [TestMethod]
    public void Frozen_ReflectsProviderMetadata()
    {
        using var frozenFake = new JSFakeExtension();
        var frozenProvider = new JSCommandProviderProxy(frozenFake.Connection, "test.ext", "Test Extension", null, Fixture("provider-metadata-frozen.json"));
        Assert.IsTrue(frozenProvider.Frozen);

        using var liveFake = new JSFakeExtension();
        var liveProvider = new JSCommandProviderProxy(liveFake.Connection, "test.ext", "Test Extension", null, Fixture("provider-metadata-unfrozen.json"));
        Assert.IsFalse(liveProvider.Frozen);

        using var defaultFake = new JSFakeExtension();
        var defaultProvider = new JSCommandProviderProxy(defaultFake.Connection, "test.ext", "Test Extension");
        Assert.IsTrue(defaultProvider.Frozen);
    }

    // A page with accentColor returns the parsed color; a page without
    // stays NoColor.
    [TestMethod]
    public void AccentColor_SurfacesParsedColorAndDefaultsToNoColor()
    {
        using var fake = new JSFakeExtension();

        var withAccent = new JSContentPageProxy("page-list", fake.Connection, Fixture("page-list.json"));
        var accent = withAccent.AccentColor;
        Assert.IsTrue(accent.HasValue);
        Assert.AreEqual(16, accent.Color.R);
        Assert.AreEqual(124, accent.Color.G);
        Assert.AreEqual(16, accent.Color.B);
        Assert.AreEqual(255, accent.Color.A);

        var withoutAccent = new JSContentPageProxy("plain", fake.Connection, ParseElement(new JsonObject { ["id"] = "plain", ["name"] = "Plain" }));
        Assert.IsFalse(withoutAccent.AccentColor.HasValue);
    }

    // A tag with fractional or out-of-range color components must not throw
    // from the WinRT-visible Tags getter. Invalid components fall back to defaults,
    // while the rest of the item metadata stays intact.
    [TestMethod]
    public void Tags_OutOfRangeOrFractionalColorComponentsDefaultInsteadOfThrowing()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Item With Bad Tag Color",
            ["subtitle"] = "Still Here",
            ["tags"] = new JsonArray
            {
                new JsonObject
                {
                    ["text"] = "over",
                    ["foreground"] = new JsonObject
                    {
                        // 256 overflows a byte and 1.5 is fractional. JsonElement.GetByte
                        // would throw, so both must fall back to defaults.
                        ["r"] = 256,
                        ["g"] = 1.5,
                        ["b"] = 12,
                    },
                },
                new JsonObject { ["text"] = "clean" },
            },
        });

        var adapter = new JSListItemAdapter(element, fake.Connection);

        var tags = adapter.Tags;
        Assert.AreEqual(2, tags.Length);
        Assert.AreEqual("over", tags[0].Text);
        Assert.AreEqual("clean", tags[1].Text);

        var foreground = tags[0].Foreground;
        Assert.IsTrue(foreground.HasValue);
        Assert.AreEqual(0, foreground.Color.R);
        Assert.AreEqual(0, foreground.Color.G);
        Assert.AreEqual(12, foreground.Color.B);

        Assert.AreEqual("Item With Bad Tag Color", adapter.Title);
        Assert.AreEqual("Still Here", adapter.Subtitle);
    }

    [TestMethod]
    public void CommandItem_UsesCanonicalTitleAndSubtitle()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Canonical title",
            ["subtitle"] = "Canonical subtitle",
            ["displayName"] = "Ignored title",
            ["description"] = "Ignored subtitle",
            ["command"] = new JsonObject { ["id"] = "command", ["name"] = "Command" },
        });

        var item = new JSCommandItemAdapter(element, fake.Connection);

        Assert.AreEqual("Canonical title", item.Title);
        Assert.AreEqual("Canonical subtitle", item.Subtitle);
    }

    [TestMethod]
    public void CommandItem_IgnoresTitleAndSubtitleAliases()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["displayName"] = "Ignored title",
            ["description"] = "Ignored subtitle",
            ["command"] = new JsonObject { ["id"] = "command", ["name"] = "Command" },
        });

        var item = new JSCommandItemAdapter(element, fake.Connection);

        Assert.AreEqual(string.Empty, item.Title);
        Assert.AreEqual(string.Empty, item.Subtitle);
    }

    [TestMethod]
    public void CommandResult_IgnoresPascalCaseWireKeys()
    {
        var result = JSCommandResultParser.ParseCommandResult(
            ParseElement(new JsonObject { ["Kind"] = (int)CommandResultKind.KeepOpen }),
            null);

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
    }

    [TestMethod]
    public void CommandItem_CachesCommandInstance()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Item",
            ["command"] = new JsonObject { ["id"] = "command", ["name"] = "Command" },
        });
        var item = new JSCommandItemAdapter(element, fake.Connection);

        Assert.AreSame(item.Command, item.Command);
    }

    private static async Task<string> SubmitAndReadFormId(JSFormContentProxy form, TaskCompletionSource<string> captured)
    {
        await Task.Run(() => form.SubmitForm("{}", "{}"));
        return await captured.Task.WaitAsync(Timeout);
    }

    private static TaskCompletionSource<string> ResetCapture(JSFakeExtension fake)
    {
        var captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("form/submit", element =>
        {
            captured.TrySetResult(element.GetProperty("formId").GetString() ?? string.Empty);
            return JsonNode.Parse("""{ "kind": 4 }""");
        });
        return captured;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Condition was not met within the timeout.");
    }

    private static JSCommandProviderProxy CreateProvider(JSFakeExtension fake) =>
        new(fake.Connection, "test.ext", "Test Extension");

    private static string FixtureText(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wire-fixtures", name));

    private static JsonElement Fixture(string name)
    {
        using var document = JsonDocument.Parse(FixtureText(name));
        return document.RootElement.Clone();
    }

    private static JsonElement ParseElement(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonNode ParseNode(string json) => JsonNode.Parse(json)!;

    /// <summary>
    /// Records status and log calls from a provider host so tests can assert status
    /// identity, in-place updates, and hiding during Dispose.
    /// </summary>
    private sealed partial class RecordingExtensionHost : IExtensionHost
    {
        private readonly object _lock = new();

        public List<IStatusMessage> ShownStatuses { get; } = new();

        public List<IStatusMessage> HiddenStatuses { get; } = new();

        public List<ILogMessage> LoggedMessages { get; } = new();

        public int ShownCount
        {
            get
            {
                lock (_lock)
                {
                    return ShownStatuses.Count;
                }
            }
        }

        public int HiddenCount
        {
            get
            {
                lock (_lock)
                {
                    return HiddenStatuses.Count;
                }
            }
        }

        public global::Windows.Foundation.IAsyncAction ShowStatus(IStatusMessage message, StatusContext context)
        {
            lock (_lock)
            {
                ShownStatuses.Add(message);
            }

            return Task.CompletedTask.AsAsyncAction();
        }

        public global::Windows.Foundation.IAsyncAction HideStatus(IStatusMessage message)
        {
            lock (_lock)
            {
                HiddenStatuses.Add(message);
            }

            return Task.CompletedTask.AsAsyncAction();
        }

        public global::Windows.Foundation.IAsyncAction LogMessage(ILogMessage message)
        {
            lock (_lock)
            {
                LoggedMessages.Add(message);
            }

            return Task.CompletedTask.AsAsyncAction();
        }
    }
}
