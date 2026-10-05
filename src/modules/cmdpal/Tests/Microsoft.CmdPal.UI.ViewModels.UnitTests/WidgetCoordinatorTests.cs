// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards.Templating;
using Microsoft.CmdPal.UI.ViewModels.Widgets;
using Microsoft.CmdPal.UI.Widgets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class WidgetCoordinatorTests
{
    [TestMethod]
    public async Task NewWidgetShowsDropdownWithNoSelection()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out _, out _);

        await coordinator.CreateAsync("instance", "Medium");

        var update = platform.Updates.Last();
        StringAssert.Contains(update.DataJson, "Sample extension: Sample widget");
        Assert.AreEqual("{}", update.CustomState);
        Assert.IsTrue(platform.Updates.First().IsPlaceholder);
        var card = JsonNode.Parse(update.TemplateJson)!;
        var data = JsonNode.Parse(update.DataJson)!;
        Assert.AreEqual(1, card["body"]!.AsArray().Count);
        Assert.AreEqual(1, card["actions"]!.AsArray().Count);
        Assert.IsFalse(update.TemplateJson.Contains("${icon}"));
        Assert.AreEqual("Input.ChoiceSet", card["body"]![0]!["type"]!.GetValue<string>());
        Assert.AreEqual("compact", card["body"]![0]!["style"]!.GetValue<string>());
        Assert.AreEqual("widget", card["body"]![0]!["id"]!.GetValue<string>());
        Assert.AreEqual(1, data["choices"]!.AsArray().Count);
        Assert.IsNull(data["icon"]);
        Assert.AreEqual(string.Empty, data["value"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task SelectionBindsWidgetAndCustomizationShowsCurrentSelection()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out var binding, out _);

        await coordinator.ActionAsync(
            "instance",
            "selectWidget",
            $"{{\"widget\":\"{binding.Encode()}\"}}",
            "{}");
        await coordinator.ShowCustomizationAsync("instance");

        var update = platform.Updates.Last();
        Assert.AreEqual(binding.Encode(), JsonNode.Parse(update.DataJson)!["value"]!.GetValue<string>());
        Assert.AreEqual(binding.Serialize(), update.CustomState);
    }

    [TestMethod]
    public async Task DropdownIncludesEveryEntryAndBindsSubmittedChoice()
    {
        var platform = new TestWidgetPlatform();
        var entries = Enumerable.Range(0, 25).Select(index => CreateEntry($"widget.{index}")).ToArray();
        using var coordinator = new WidgetCoordinator(new TestWidgetCatalog(entries), platform);
        await coordinator.CreateAsync("instance", "Small");

        var update = platform.Updates.Last();
        var data = JsonNode.Parse(update.DataJson)!;
        var choices = data["choices"]!.AsArray();
        Assert.AreEqual(entries.Length, choices.Count);
        Assert.AreEqual("{}", update.CustomState);
        for (var index = 0; index < entries.Length; index++)
        {
            Assert.AreEqual($"Source: {entries[index].Definition.Title}", choices[index]!["title"]!.GetValue<string>());
            Assert.AreEqual(entries[index].Binding.Encode(), choices[index]!["value"]!.GetValue<string>());
        }

        await coordinator.ActionAsync("instance", "selectWidget", new JsonObject { ["widget"] = choices[^1]!["value"]!.GetValue<string>() }.ToJsonString(), "{}");
        Assert.AreEqual(entries[^1].Binding.Serialize(), platform.Updates.Last().CustomState);
    }

    [DataTestMethod]
    [DataRow("small")]
    [DataRow("medium")]
    [DataRow("large")]
    public async Task ExpandedChooserHasOneDropdownAndOneSubmitButton(string size)
    {
        var platform = new TestWidgetPlatform();
        var first = CreateEntry(new string('W', 200));
        first = first with { Definition = first.Definition with { SourceName = new string('S', 200), Description = new string('D', 2000) } };
        using var coordinator = new WidgetCoordinator(new TestWidgetCatalog(first, CreateEntry("second")), platform);
        await coordinator.CreateAsync("instance", size);

        var update = platform.Updates.Last();
        var context = new EvaluationContext
        {
            RootJson = update.DataJson,
            HostJson = new JsonObject { ["widgetSize"] = size }.ToJsonString(),
        };
        var expanded = JsonNode.Parse(new AdaptiveCardTemplate(update.TemplateJson).Expand(context))!;
        var body = expanded["body"]!.AsArray();
        Assert.AreEqual(1, body.Count);
        var dropdown = body[0]!;
        Assert.AreEqual("Input.ChoiceSet", dropdown["type"]!.GetValue<string>());
        Assert.AreEqual("compact", dropdown["style"]!.GetValue<string>());
        Assert.IsFalse(dropdown["isMultiSelect"]!.GetValue<bool>());
        Assert.IsTrue(dropdown["isRequired"]!.GetValue<bool>());
        Assert.AreEqual(2, dropdown["choices"]!.AsArray().Count);
        Assert.AreEqual(first.Binding.Encode(), dropdown["choices"]![0]!["value"]!.GetValue<string>());
        Assert.AreEqual($"{first.Definition.SourceName}: {first.Definition.Title}", dropdown["choices"]![0]!["title"]!.GetValue<string>());

        var actions = expanded["actions"]!.AsArray();
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual("selectWidget", actions[0]!["verb"]!.GetValue<string>());
        Assert.AreEqual("Use widget", actions[0]!["title"]!.GetValue<string>());
        Assert.AreEqual("auto", actions[0]!["associatedInputs"]!.GetValue<string>());
        Assert.IsNull(actions[0]!["data"]);
    }

    [TestMethod]
    public async Task SingleEntryStillUsesDropdown()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out _, out _);
        await coordinator.CreateAsync("instance", "Small");
        var update = platform.Updates.Last();
        var context = new EvaluationContext { RootJson = update.DataJson, HostJson = "{\"widgetSize\":\"small\"}" };
        var card = JsonNode.Parse(new AdaptiveCardTemplate(update.TemplateJson).Expand(context))!;
        Assert.AreEqual(1, card["body"]![0]!["choices"]!.AsArray().Count);
        Assert.AreEqual(1, card["actions"]!.AsArray().Count);
        Assert.AreEqual("selectWidget", card["actions"]![0]!["verb"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task CustomizationAfterRestartStartsAtPersistedSelection()
    {
        var platform = new TestWidgetPlatform();
        var first = CreateEntry("first");
        var second = CreateEntry("second");
        platform.Update(new("instance", "{}", "{}", second.Binding.Serialize()));
        using var coordinator = new WidgetCoordinator(new TestWidgetCatalog(first, second), platform);

        await coordinator.ShowCustomizationAsync("instance");

        var update = platform.Updates.Last();
        Assert.AreEqual(second.Binding.Encode(), JsonNode.Parse(update.DataJson)!["value"]!.GetValue<string>());
        Assert.IsTrue(platform.Updates.All(item => item.CustomState == second.Binding.Serialize()));
    }

    [TestMethod]
    public async Task CustomizationAfterRestartSurvivesHostLifecycleCallbacks()
    {
        var platform = new TestWidgetPlatform();
        var entry = CreateEntry("saved");
        platform.Update(new("instance", "{}", "{}", entry.Binding.Serialize()));
        using var coordinator = new WidgetCoordinator(new TestWidgetCatalog(entry), platform);
        await coordinator.ShowCustomizationAsync("instance");
        var selector = platform.Updates.Last();
        var updateCount = platform.Updates.Count;

        await coordinator.CreateAsync("instance", "Small");
        await coordinator.ActivateAsync("instance", "Small");
        await coordinator.ContextChangedAsync("instance", "Large");

        Assert.AreEqual(updateCount, platform.Updates.Count);
        Assert.AreEqual(selector, platform.Updates.Last());
        StringAssert.Contains(selector.TemplateJson, "Input.ChoiceSet");
        await coordinator.ActionAsync("instance", "selectWidget", new JsonObject { ["widget"] = entry.Binding.Encode() }.ToJsonString(), selector.CustomState);
        Assert.IsFalse(platform.Updates.Last().TemplateJson.Contains("Input.ChoiceSet"));
    }

    [TestMethod]
    public async Task RefreshRemovesUnavailableChoices()
    {
        var platform = new TestWidgetPlatform();
        var first = CreateEntry("first");
        var second = CreateEntry("second");
        var catalog = new TestWidgetCatalog(first, second);
        using var coordinator = new WidgetCoordinator(catalog, platform);
        await coordinator.CreateAsync("instance", "Medium");

        catalog.Entries = [first];
        await coordinator.ActionAsync("instance", "refreshWidgets", "{}", "{}");
        var choices = JsonNode.Parse(platform.Updates.Last().DataJson)!["choices"]!.AsArray();
        Assert.AreEqual(1, choices.Count);
        Assert.AreEqual(first.Binding.Encode(), choices[0]!["value"]!.GetValue<string>());

        catalog.Entries = [];
        await coordinator.ActionAsync("instance", "refreshWidgets", "{}", "{}");
        StringAssert.Contains(platform.Updates.Last().DataJson, "No extension widgets");
    }

    [TestMethod]
    public async Task BrowsingPreservesBindingAndIgnoresLiveUpdates()
    {
        var platform = new TestWidgetPlatform();
        var first = CreateEntry("first");
        var second = CreateEntry("second");
        var widget = (WidgetContent)first.Create("instance")!;
        using var coordinator = new WidgetCoordinator(new TestWidgetCatalog(first, second), platform);
        await coordinator.ActionAsync("instance", "selectWidget", new JsonObject { ["widget"] = first.Binding.Encode() }.ToJsonString(), "{}");
        await coordinator.ShowCustomizationAsync("instance");

        var update = platform.Updates.Last();
        ((FormContent)widget.Content!).DataJson = "{\"value\":\"background\"}";
        await Task.Delay(350);
        await coordinator.ContextChangedAsync("instance", "Large");
        await coordinator.ActivateAsync("instance", "Large");

        Assert.AreEqual(update, platform.Updates.Last());
        Assert.AreEqual(first.Binding.Serialize(), update.CustomState);
        Assert.AreEqual(first.Binding.Encode(), JsonNode.Parse(update.DataJson)!["value"]!.GetValue<string>());
        await coordinator.ActionAsync("instance", "selectWidget", new JsonObject { ["widget"] = first.Binding.Encode() }.ToJsonString(), update.CustomState);
        StringAssert.Contains(platform.Updates.Last().DataJson, "background");
    }

    [TestMethod]
    public async Task MissingSelectionDoesNotChangeExistingBinding()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out var binding, out _);
        await coordinator.ActionAsync("instance", "selectWidget", new JsonObject { ["widget"] = binding.Encode() }.ToJsonString(), "{}");
        await coordinator.ShowCustomizationAsync("instance");
        await coordinator.ActionAsync("instance", "selectWidget", "{}", binding.Serialize());
        var update = platform.Updates.Last();
        Assert.AreEqual(binding.Serialize(), update.CustomState);
        Assert.AreEqual(binding.Encode(), JsonNode.Parse(update.DataJson)!["value"]!.GetValue<string>());
    }

    private static WidgetCatalogEntry CreateEntry(string id)
    {
        var widget = new WidgetContent { Id = id, Content = new TestFormContent(), AllowMultiple = true };
        return new WidgetCatalogEntry(
            new WidgetDefinition("extension", "provider", id, id, "Description", "Source", "S", widget.Icon, [], true),
            _ => widget);
    }

    [TestMethod]
    public async Task FormActionIsForwardedAndUpdatesContent()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out var binding, out var form);
        await coordinator.ActionAsync("instance", "selectWidget", $"{{\"widget\":\"{binding.Encode()}\"}}", "{}");

        await coordinator.ActionAsync("instance", "increment", "{\"input\":1}", binding.Serialize());

        Assert.AreEqual(1, form.SubmitCount);
        StringAssert.Contains(form.LastContext, "increment");
        StringAssert.Contains(platform.Updates.Last().DataJson, "updated");
    }

    [TestMethod]
    public async Task BoundWidgetSuppliesHeaderMetadataButSelectorDoesNot()
    {
        var platform = new TestWidgetPlatform();
        var entry = CreateEntry("header");
        var widget = (WidgetContent)entry.Create("instance")!;
        widget.Title = "CPU Usage";
        widget.Icon = new IconInfo("\uE9D9");
        using var coordinator = new WidgetCoordinator(new TestWidgetCatalog(entry), platform);
        await coordinator.ActionAsync("instance", "selectWidget", new JsonObject { ["widget"] = entry.Binding.Encode() }.ToJsonString(), "{}");

        Assert.AreEqual(widget.Title, platform.Updates.Last().HeaderTitle);
        Assert.AreSame(widget.Icon, platform.Updates.Last().HeaderIcon);
        await coordinator.ShowCustomizationAsync("instance");
        Assert.IsNull(platform.Updates.Last().HeaderTitle);
        Assert.IsNull(platform.Updates.Last().HeaderIcon);
        StringAssert.Contains(platform.Updates.Last().TemplateJson, "Input.ChoiceSet");
    }

    [TestMethod]
    public void HeaderCompositionPreservesCardBodyAndEncodesMetadata()
    {
        const string template = "{\"type\":\"AdaptiveCard\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"${value}\"}],\"header\":null}";
        const string title = "CPU \"Usage\"";
        const string url = "https://example.com/icon.png";
        var update = new WidgetPlatformUpdate("instance", template, "{}", "{}");
        var result = JsonNode.Parse(update.TemplateWithHeader(title, url))!;
        Assert.AreEqual(title, result["header"]!["text"]!.GetValue<string>());
        Assert.AreEqual(url, result["header"]!["iconUrl"]!.GetValue<string>());
        Assert.AreEqual("${value}", result["body"]![0]!["text"]!.GetValue<string>());
        Assert.IsNull(JsonNode.Parse(template)!["header"]);
        Assert.IsNull(JsonNode.Parse(update.TemplateWithHeader(title, null))!["header"]!["iconUrl"]);
    }

    [TestMethod]
    [TestCategory("WidgetPlatform")]
    public async Task HeaderGlyphRendersAsNonblankPng()
    {
        RequirePackageIdentity();
        var url = await WindowsWidgetPlatform.RenderIconAsync(new IconInfo("\uE9D9"));
        Assert.IsNotNull(url);
        StringAssert.StartsWith(url, "data:image/png;base64,");
        using var stream = new MemoryStream(Convert.FromBase64String(url["data:image/png;base64,".Length..]));
        using var randomAccess = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccess);
        Assert.AreEqual(32u, decoder.PixelWidth);
        Assert.AreEqual(32u, decoder.PixelHeight);
        var pixels = (await decoder.GetPixelDataAsync()).DetachPixelData();
        Assert.IsTrue(pixels.Any(value => value != 0));
        Assert.IsTrue(pixels.Distinct().Count() > 4, "Icon must contain a glyph, not just a background.");
    }

    [TestMethod]
    public async Task HeaderHttpsIconPassesThrough()
    {
        const string url = "https://example.com/icon.png";
        Assert.AreEqual(url, await WindowsWidgetPlatform.RenderIconAsync(new IconInfo(url)));
        Assert.IsNull(await WindowsWidgetPlatform.RenderIconAsync(new IconInfo("javascript:alert(1)")));
    }

    [TestMethod]
    [TestCategory("WidgetPlatform")]
    public async Task HeaderImageAndStreamRenderAsPng()
    {
        RequirePackageIdentity();
        var glyphUrl = await WindowsWidgetPlatform.RenderIconAsync(new IconInfo("\uE9D9"));
        Assert.IsNotNull(glyphUrl);
        var png = Convert.FromBase64String(glyphUrl["data:image/png;base64,".Length..]);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var streamUrl = await WindowsWidgetPlatform.RenderIconAsync(IconInfo.FromStream(stream));
        Assert.IsNotNull(streamUrl);
        StringAssert.StartsWith(streamUrl, "data:image/png;base64,");

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, png);
            var fileUrl = await WindowsWidgetPlatform.RenderIconAsync(new IconInfo(path));
            Assert.IsNotNull(fileUrl);
            StringAssert.StartsWith(fileUrl, "data:image/png;base64,");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("WidgetPlatform")]
    public void PackagedHostCanQueryCustomizationInterface()
    {
        RequirePackageIdentity();
        var clsid = new Guid("918F58B7-7E1A-4E1A-A53E-6B0D760D83A3");
        var providerId = new Guid("5c5774cc-72a0-452d-b9ed-075c0dd25eed");
        var activationResult = CoCreateInstance(in clsid, IntPtr.Zero, 4, in providerId, out var unknown);
        Assert.AreEqual(0, activationResult, $"Widget provider activation returned 0x{activationResult:X8}");
        try
        {
            foreach (var iidValue in new[] { "5c5774cc-72a0-452d-b9ed-075c0dd25eed", "38c3a963-dd93-479d-9276-04bf84ee1816" })
            {
                var iid = new Guid(iidValue);
                var result = Marshal.QueryInterface(unknown, in iid, out var pointer);
                try
                {
                    Assert.AreEqual(0, result, $"QueryInterface({iid}) returned 0x{result:X8}");
                }
                finally
                {
                    if (pointer != IntPtr.Zero)
                    {
                        Marshal.Release(pointer);
                    }
                }
            }
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr instance);

    private static void RequirePackageIdentity()
    {
        try
        {
            _ = Package.Current.Id;
        }
        catch (InvalidOperationException)
        {
            Assert.Inconclusive("Run with Invoke-CommandInDesktopPackage using the CmdPal development package.");
        }
    }

    [TestMethod]
    public async Task InvalidExtensionContentShowsErrorState()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out var binding, out var form);
        form.TemplateJson = "not json";

        await coordinator.ActionAsync("instance", "selectWidget", $"{{\"widget\":\"{binding.Encode()}\"}}", "{}");

        var update = platform.Updates.Last();
        StringAssert.Contains(update.DataJson, "Widget content error");
        StringAssert.Contains(update.DataJson, "could not render");
    }

    [TestMethod]
    public async Task FormChangesAreDebounced()
    {
        var platform = new TestWidgetPlatform();
        using var coordinator = CreateCoordinator(platform, out var binding, out var form);
        await coordinator.ActionAsync("instance", "selectWidget", $"{{\"widget\":\"{binding.Encode()}\"}}", "{}");
        var updatesBeforeChanges = platform.Updates.Count;

        form.DataJson = "{\"value\":1}";
        form.DataJson = "{\"value\":2}";
        form.DataJson = "{\"value\":3}";
        await Task.Delay(350);

        Assert.AreEqual(updatesBeforeChanges + 1, platform.Updates.Count);
        StringAssert.Contains(platform.Updates.Last().DataJson, "3");
    }

    private static WidgetCoordinator CreateCoordinator(
        TestWidgetPlatform platform,
        out WidgetBinding binding,
        out TestFormContent form)
    {
        binding = new("extension", "provider", "sample.widget");
        form = new TestFormContent();
        var widget = new WidgetContent
        {
            Id = binding.WidgetId,
            Title = "Sample widget",
            Description = "Sample widget description",
            Icon = new IconInfo("S"),
            SupportedSizes = [WidgetSize.Medium],
            Content = form,
        };
        var definition = new WidgetDefinition(
            binding.ExtensionId,
            binding.ProviderId,
            binding.WidgetId,
            widget.Title,
            widget.Description,
            "Sample extension",
            "S",
            widget.Icon,
            widget.SupportedSizes,
            widget.AllowMultiple);
        var catalog = new TestWidgetCatalog(new WidgetCatalogEntry(definition, _ => widget));
        return new WidgetCoordinator(catalog, platform);
    }

    private sealed class TestWidgetCatalog(params WidgetCatalogEntry[] entries) : IWidgetCatalog
    {
        public IReadOnlyList<WidgetCatalogEntry> Entries { get; set; } = entries;

        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public WidgetCatalogEntry? Find(WidgetBinding binding) => Entries.FirstOrDefault(candidate => candidate.Binding == binding);

        public Task<IWidgetContent?> CreateAsync(WidgetBinding binding, string instanceId, CancellationToken cancellationToken) =>
            Task.FromResult(Find(binding)?.Create(instanceId));
    }

    private sealed class TestWidgetPlatform : IWidgetPlatform
    {
        public List<WidgetPlatformUpdate> Updates { get; } = [];

        public WidgetPlatformInfo? GetWidget(string widgetId)
        {
            var update = Updates.LastOrDefault(item => item.WidgetId == widgetId);
            return update is null ? null : new(widgetId, update.CustomState);
        }

        public IReadOnlyList<WidgetPlatformInfo> GetWidgets() => Updates
            .GroupBy(update => update.WidgetId)
            .Select(group => new WidgetPlatformInfo(group.Key, group.Last().CustomState))
            .ToArray();

        public void Update(WidgetPlatformUpdate update) => Updates.Add(update);
    }

    private sealed partial class TestFormContent : FormContent
    {
        public TestFormContent()
        {
            TemplateJson = "{\"type\":\"AdaptiveCard\",\"version\":\"1.5\"}";
            DataJson = "{\"value\":\"initial\"}";
        }

        public int SubmitCount { get; private set; }

        public string LastContext { get; private set; } = string.Empty;

        public override ICommandResult SubmitForm(string inputs, string data)
        {
            SubmitCount++;
            LastContext = data;
            DataJson = "{\"value\":\"updated\"}";
            return CommandResult.KeepOpen();
        }
    }
}
