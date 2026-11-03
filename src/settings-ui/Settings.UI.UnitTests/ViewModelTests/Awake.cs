// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public class Awake
{
    [TestMethod]
    public void TrayTimesShowDefaultsWhenNoCustomTimesAreConfigured()
    {
        var viewModel = CreateViewModel(new AwakeSettings());

        CollectionAssert.AreEqual(AwakeDefaults.TrayTimeSeconds.ToList(), viewModel.TrayTimes.Select(item => item.Seconds).ToList());
        Assert.IsTrue(viewModel.TrayTimes.Any(item => item.Seconds > 2 * 3600), "Defaults should offer durations longer than 2 hours.");
    }

    [TestMethod]
    public void TrayTimesShowConfiguredCustomTimesSortedByDuration()
    {
        var settings = new AwakeSettings();
        settings.Properties.CustomTrayTimes.Add("Long", 36000);
        settings.Properties.CustomTrayTimes.Add("Short", 600);

        var viewModel = CreateViewModel(settings);

        Assert.AreEqual("Short", viewModel.TrayTimes[0].Label);
        Assert.AreEqual("Long", viewModel.TrayTimes[1].Label);
    }

    [TestMethod]
    public void AddTrayTimeSavesSortedCustomTimesAndNotifies()
    {
        var viewModel = CreateViewModel(new AwakeSettings());
        var notifications = new List<string>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        viewModel.NewTrayTimeHours = 5;
        viewModel.NewTrayTimeMinutes = 30;

        Assert.IsTrue(viewModel.AddTrayTime());

        Dictionary<string, uint> saved = viewModel.ModuleSettings.Properties.CustomTrayTimes;
        CollectionAssert.AreEqual(
            AwakeDefaults.TrayTimeSeconds.Append(19800u).OrderBy(seconds => seconds).ToList(),
            saved.Values.ToList());
        Assert.AreEqual(19800u, saved["19800s"]);
        CollectionAssert.Contains(notifications, nameof(AwakeViewModel.TrayTimes));
    }

    [TestMethod]
    [DataRow(0d, 0d)]
    [DataRow(2d, 0d)]
    [DataRow(0d, 60d)]
    [DataRow(1000d, 0d)]
    [DataRow(1.5d, 0d)]
    public void AddTrayTimeRejectsInvalidOrDuplicateDurations(double hours, double minutes)
    {
        var viewModel = CreateViewModel(new AwakeSettings());
        viewModel.NewTrayTimeHours = hours;
        viewModel.NewTrayTimeMinutes = minutes;

        Assert.IsFalse(viewModel.CanAddTrayTime);
        Assert.IsFalse(viewModel.AddTrayTime());
        Assert.IsEmpty(viewModel.ModuleSettings.Properties.CustomTrayTimes);
    }

    [TestMethod]
    public void RemoveTrayTimeSavesRemainingTimes()
    {
        var viewModel = CreateViewModel(new AwakeSettings());
        var removed = viewModel.TrayTimes.First();

        Assert.IsTrue(viewModel.RemoveTrayTime(removed));

        Assert.IsFalse(viewModel.ModuleSettings.Properties.CustomTrayTimes.ContainsValue(removed.Seconds));
        Assert.HasCount(AwakeDefaults.TrayTimeSeconds.Count - 1, viewModel.ModuleSettings.Properties.CustomTrayTimes);
    }

    [TestMethod]
    public void RemoveTrayTimeKeepsLastRemainingTime()
    {
        var settings = new AwakeSettings();
        settings.Properties.CustomTrayTimes.Add("Only", 600);
        var viewModel = CreateViewModel(settings);

        Assert.IsFalse(viewModel.CanRemoveTrayTime);
        Assert.IsFalse(viewModel.RemoveTrayTime(viewModel.TrayTimes.Single()));
        Assert.HasCount(1, viewModel.ModuleSettings.Properties.CustomTrayTimes);
    }

    [TestMethod]
    public void ResetTrayTimesClearsCustomTimesAndShowsDefaults()
    {
        var settings = new AwakeSettings();
        settings.Properties.CustomTrayTimes.Add("Custom", 600);
        var viewModel = CreateViewModel(settings);

        viewModel.ResetTrayTimes();

        Assert.IsEmpty(viewModel.ModuleSettings.Properties.CustomTrayTimes);
        CollectionAssert.AreEqual(AwakeDefaults.TrayTimeSeconds.ToList(), viewModel.TrayTimes.Select(item => item.Seconds).ToList());
    }

    private static AwakeViewModel CreateViewModel(AwakeSettings settings)
    {
        return new AwakeViewModel(seconds => seconds.ToString(CultureInfo.InvariantCulture) + "s")
        {
            IsEnabled = true,
            ModuleSettings = settings,
        };
    }
}
