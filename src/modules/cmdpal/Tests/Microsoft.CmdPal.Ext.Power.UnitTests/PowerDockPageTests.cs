// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Pages;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class PowerDockPageTests
{
    [TestMethod]
    public void ModeDockPage_HasScopedId()
    {
        var context = CreateContext();
        var page = CreateDockPage(context, PowerDockScope.Mode);

        Assert.AreEqual("com.microsoft.cmdpal.power.mode", page.Id);
    }

    [TestMethod]
    public void PlanDockPage_HasScopedId()
    {
        var context = CreateContext();
        var page = CreateDockPage(context, PowerDockScope.Plan);

        Assert.AreEqual("com.microsoft.cmdpal.power.plan", page.Id);
    }

    [TestMethod]
    public void ModeDockPage_HasSingleItemWithoutSeparators()
    {
        var context = CreateContext();
        var page = CreateDockPage(context, PowerDockScope.Mode);
        var items = page.GetItems();

        Assert.IsTrue(items.Length == 0 || items.Length == 1);
        Assert.IsFalse(items.Any(item => item is Separator));
    }

    [TestMethod]
    public void PlanDockPage_HasSingleItemWithoutSeparators()
    {
        var context = CreateContext();
        var page = CreateDockPage(context, PowerDockScope.Plan);
        var items = page.GetItems();

        Assert.IsTrue(items.Length == 0 || items.Length == 1);
        Assert.IsFalse(items.Any(item => item is Separator));
    }

    [TestMethod]
    public void ModePickerPage_HasExpectedItemCount()
    {
        var context = CreateContext();
        var picker = CreateModePicker(context);
        var items = picker.GetItems();

        Assert.IsTrue(items.Length == 0 || items.Length == 3);
    }

    [TestMethod]
    public void Provider_GetDockBands_IncludesComponentTitles()
    {
        using var provider = new PowerCommandsProvider();
        var bands = provider.GetDockBands();

        Assert.IsNotNull(bands);
        Assert.IsTrue(bands!.Length >= 1);

        var titles = bands.Select(b => b.Title).ToArray();
        if (bands.Length >= 2)
        {
            CollectionAssert.Contains(titles, Resources.power_mode_dock_band_title);
            CollectionAssert.Contains(titles, Resources.power_plan_dock_band_title);
        }
    }

    private sealed record TestContext(
        PowerModeService PowerModeService,
        PowerPlanService PowerPlanService,
        PowerModeDataManager DataManager,
        PowerListItemBuilder ItemBuilder,
        PowerModePickerPage ModePicker,
        PowerPlanPickerPage PlanPicker);

    private static TestContext CreateContext()
    {
        var powerModeService = new PowerModeService();
        var powerPlanService = new PowerPlanService();
        var itemBuilder = new PowerListItemBuilder(powerModeService, powerPlanService);
        PowerModePickerPage modePicker = null!;
        PowerPlanPickerPage planPicker = null!;
        var dataManager = new PowerModeDataManager(
            powerModeService,
            () =>
            {
                modePicker?.HandleLiveStateChanged();
                planPicker?.HandleLiveStateChanged();
            });

        modePicker = new PowerModePickerPage(powerModeService, dataManager, itemBuilder, () => { });
        planPicker = new PowerPlanPickerPage(powerPlanService, dataManager, itemBuilder, () => { });

        return new TestContext(
            powerModeService,
            powerPlanService,
            dataManager,
            itemBuilder,
            modePicker,
            planPicker);
    }

    private static PowerDockPage CreateDockPage(TestContext context, PowerDockScope scope) =>
        new(
            scope,
            context.PowerModeService,
            context.PowerPlanService,
            context.ModePicker,
            context.PlanPicker,
            context.DataManager);

    private static PowerModePickerPage CreateModePicker(TestContext context) => context.ModePicker;
}
