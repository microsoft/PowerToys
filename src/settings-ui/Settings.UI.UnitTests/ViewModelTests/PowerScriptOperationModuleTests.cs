// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.UI.ViewModels;

namespace ViewModelTests;

[TestClass]
public sealed class PowerScriptOperationModuleTests
{
    [TestMethod]
    public void TemplateMetadataUsesExplicitModuleIdentityAndDisplayName()
    {
        var template = JsonSerializer.Deserialize<PowerScriptOperationTemplate>(
            """{"id":"powertoys:filelocksmith.query","moduleId":"filelocksmith","group":"File Locksmith","commandId":"filelocksmith.query"}""");

        Assert.AreEqual("filelocksmith", template.ModuleId);
        Assert.AreEqual("File Locksmith", template.Group);
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(template));
        Assert.AreEqual("filelocksmith", serialized.RootElement.GetProperty("moduleId").GetString());
        Assert.AreEqual("File Locksmith", serialized.RootElement.GetProperty("group").GetString());
    }

    [TestMethod]
    public void EachModuleContainsOnlyItsOwnOperations()
    {
        var query = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        var layouts = Template("fancyzones", "FancyZones", "fancyzones.get-layouts");
        var monitors = Template("fancyzones", "FancyZones", "fancyzones.get-monitors");

        var modules = PowerScriptOperationModule.FromTemplates(new[] { query, layouts, monitors });

        Assert.AreEqual(2, modules.Count);
        Assert.AreEqual("filelocksmith", modules[0].Id);
        Assert.AreEqual("File Locksmith", modules[0].Name);
        Assert.AreEqual(1, modules[0].Operations.Count);
        Assert.AreSame(query, modules[0].Operations[0]);
        Assert.AreEqual("fancyzones", modules[1].Id);
        Assert.AreEqual("FancyZones", modules[1].Name);
        Assert.AreEqual(2, modules[1].Operations.Count);
        Assert.AreSame(layouts, modules[1].Operations[0]);
        Assert.AreSame(monitors, modules[1].Operations[1]);
    }

    [TestMethod]
    public void ModuleAndOperationOrderFollowFirstAppearanceRatherThanSorting()
    {
        var monitors = Template("fancyzones", "FancyZones", "fancyzones.get-monitors");
        var query = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        var layouts = Template("fancyzones", "FancyZones", "fancyzones.get-layouts");

        var modules = PowerScriptOperationModule.FromTemplates(new[] { monitors, query, layouts });

        Assert.AreEqual("fancyzones", modules[0].Id);
        Assert.AreSame(monitors, modules[0].Operations[0]);
        Assert.AreSame(layouts, modules[0].Operations[1]);
        Assert.AreEqual("filelocksmith", modules[1].Id);
        Assert.AreSame(query, modules[1].Operations[0]);
    }

    [TestMethod]
    public void MatchingDisplayNamesDoNotMergeDifferentModuleIdentities()
    {
        var first = Template("first", "Utilities", "first.query");
        var second = Template("second", "Utilities", "second.query");

        var modules = PowerScriptOperationModule.FromTemplates(new[] { first, second });

        Assert.AreEqual(2, modules.Count);
        Assert.AreSame(first, modules[0].Operations[0]);
        Assert.AreSame(second, modules[1].Operations[0]);
    }

    [TestMethod]
    public void ModuleIdentityComparisonMatchesCaseInsensitiveOperationCatalogue()
    {
        var layouts = Template("fancyzones", "FancyZones", "fancyzones.get-layouts");
        var monitors = Template("FANCYZONES", "FancyZones", "fancyzones.get-monitors");

        var modules = PowerScriptOperationModule.FromTemplates(new[] { layouts, monitors });

        Assert.AreEqual(1, modules.Count);
        Assert.AreEqual("fancyzones", modules[0].Id);
        Assert.AreEqual(2, modules[0].Operations.Count);
    }

    [TestMethod]
    public void ChangingTheSourceCollectionDoesNotChangeGroupedMembership()
    {
        var query = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        var templates = new List<PowerScriptOperationTemplate> { query };

        var modules = PowerScriptOperationModule.FromTemplates(templates);
        templates.Clear();

        Assert.AreEqual(1, modules.Count);
        Assert.AreSame(query, modules[0].Operations[0]);
    }

    [TestMethod]
    public void EmptyCatalogueHasNoModules()
    {
        Assert.AreEqual(0, PowerScriptOperationModule.FromTemplates(Array.Empty<PowerScriptOperationTemplate>()).Count);
    }

    [TestMethod]
    public void NullCatalogueIsRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => PowerScriptOperationModule.FromTemplates(null));
    }

    [TestMethod]
    public void NullCatalogueEntryIsRejected()
    {
        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new PowerScriptOperationTemplate[] { null }));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t")]
    public void MissingModuleIdentityIsRejected(string moduleId)
    {
        var template = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        template.ModuleId = moduleId;

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { template }));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t")]
    public void MissingModuleDisplayNameIsRejected(string group)
    {
        var template = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        template.Group = group;

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { template }));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t")]
    public void MissingTemplateIdentityIsRejected(string id)
    {
        var template = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        template.Id = id;

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { template }));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t")]
    public void MissingOperationIdentityIsRejected(string commandId)
    {
        var template = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        template.CommandId = commandId;

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { template }));
    }

    [TestMethod]
    public void OneModuleCannotHaveConflictingDisplayNames()
    {
        var layouts = Template("fancyzones", "FancyZones", "fancyzones.get-layouts");
        var monitors = Template("fancyzones", "A different display name", "fancyzones.get-monitors");

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { layouts, monitors }));
    }

    [TestMethod]
    public void DuplicateTemplateIdentitiesAreRejectedAcrossModules()
    {
        var query = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        var layouts = Template("fancyzones", "FancyZones", "fancyzones.get-layouts");
        layouts.Id = query.Id.ToUpperInvariant();

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { query, layouts }));
    }

    [TestMethod]
    public void DuplicateOperationsAreRejectedAcrossModules()
    {
        var query = Template("filelocksmith", "File Locksmith", "filelocksmith.query");
        var layouts = Template("fancyzones", "FancyZones", "fancyzones.get-layouts");
        layouts.CommandId = query.CommandId.ToUpperInvariant();

        Assert.ThrowsException<ArgumentException>(() => PowerScriptOperationModule.FromTemplates(new[] { query, layouts }));
    }

    private static PowerScriptOperationTemplate Template(string moduleId, string group, string commandId) => new()
    {
        Id = "powertoys:" + commandId,
        Name = commandId,
        ModuleId = moduleId,
        Group = group,
        CommandId = commandId,
    };
}
