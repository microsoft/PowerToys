// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards.Templating;
using Microsoft.CmdPal.UI.ViewModels.Widgets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
