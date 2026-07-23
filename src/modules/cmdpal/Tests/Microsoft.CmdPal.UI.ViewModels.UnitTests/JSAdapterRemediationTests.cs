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
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Covers the phase-3 adapter remediation items (form identity, toast
/// continuation, page routing, pagination, settings metadata, context
/// shortcuts, icon fallback, status identity, provider dispose, frozen and
/// accent color). The parser assertions consume the shared TS SDK wire
/// fixtures so the C# adapters stay byte-compatible with the SDK.
/// </summary>
[TestClass]
public partial class JSAdapterRemediationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    // p3-01: a page with multiple forms submits each one with its own formId.
    [TestMethod]
    public async Task Form_MultipleFormsSubmitWithTheirOwnFormId()
    {
        using var fake = new JSFakeExtension();
        var captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("form/submit", element =>
        {
            captured.TrySetResult(element.GetProperty("formId").GetString() ?? string.Empty);
            return JsonNode.Parse("""{ "Kind": 4 }""");
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

    // p3-01: a form nested inside tree content submits with its own formId.
    [TestMethod]
    public async Task Form_NestedFormSubmitsWithItsOwnFormId()
    {
        using var fake = new JSFakeExtension();
        var captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequest("form/submit", element =>
        {
            captured.TrySetResult(element.GetProperty("formId").GetString() ?? string.Empty);
            return JsonNode.Parse("""{ "Kind": 4 }""");
        });

        var tree = Fixture("content-tree-nested-form.json");
        var nestedForm = tree.GetProperty("children")[0].Clone();
        var proxy = new JSFormContentProxy("page-1", nestedForm, fake.Connection);

        var formId = await SubmitAndReadFormId(proxy, captured);
        Assert.AreEqual("child-form", formId);
    }

    // p3-02: a toast result preserves its nested continuation result.
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

    // p3-03: two references to the same pageId both receive items-changed.
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

    // p3-04: HasMoreItems flips true to false at the last page and no further
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

    // p3-05: the settings page exposes the full serialized metadata, not just id.
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

    // p3-06: a valid requested shortcut maps to a KeyChord.
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

    // p3-06: a malformed shortcut yields no shortcut and does not throw.
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

    // p3-07: an absent item icon falls back to the command's icon.
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

    // p3-07: an explicitly empty item icon stays empty and does not fall back.
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

    // p3-07: light and dark icon variants both round-trip from the shared fixture.
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

    // p3-08: showing then updating the same statusId refreshes in place without
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

    // p3-08: a non-info status hides by its statusId.
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

    // p3-09: after dispose, a late notification is ignored and the active status
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
                ["message"] = new JsonObject { ["Message"] = "Too late", ["State"] = 0 },
            });

        await Task.Delay(300);
        Assert.AreEqual(1, host.ShownCount);
    }

    // p3-10: frozen and non-frozen providers surface their actual value.
    [TestMethod]
    public void Frozen_ReflectsProviderMetadata()
    {
        using var frozenFake = new JSFakeExtension();
        var frozenProvider = new JSCommandProviderProxy(frozenFake.Connection, Manifest(), Fixture("provider-metadata-frozen.json"));
        Assert.IsTrue(frozenProvider.Frozen);

        using var liveFake = new JSFakeExtension();
        var liveProvider = new JSCommandProviderProxy(liveFake.Connection, Manifest(), Fixture("provider-metadata-unfrozen.json"));
        Assert.IsFalse(liveProvider.Frozen);

        using var defaultFake = new JSFakeExtension();
        var defaultProvider = new JSCommandProviderProxy(defaultFake.Connection, Manifest());
        Assert.IsTrue(defaultProvider.Frozen);
    }

    // p3-11: a page with accentColor surfaces the parsed color; a page without
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
            return JsonNode.Parse("""{ "Kind": 4 }""");
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
        new(fake.Connection, Manifest());

    private static JSExtensionManifest Manifest() =>
        new() { Name = "test.ext", DisplayName = "Test Extension" };

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
    /// Records the status and log calls a provider makes on its host so tests can
    /// assert status identity, update-in-place, and hide-on-dispose behavior.
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
