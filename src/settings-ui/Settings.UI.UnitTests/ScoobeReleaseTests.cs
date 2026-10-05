// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests
{
    [TestClass]
    public class ScoobeReleaseTests
    {
        [TestMethod]
        public void CreateReleaseGroupsHidesPrereleasesByDefault()
        {
            var groups = ScoobeWindow.CreateReleaseGroups(CreateReleases(), false);

            Assert.AreEqual(2, groups.Count);
            Assert.IsTrue(groups.SelectMany(group => group).All(release => !release.IsPrerelease));
        }

        [TestMethod]
        public void CreateReleaseGroupsSeparatesPrereleasesWhenEnabled()
        {
            var groups = ScoobeWindow.CreateReleaseGroups(CreateReleases(), true);

            Assert.AreEqual(3, groups.Count);
            Assert.IsTrue(groups[0].All(release => release.IsPrerelease));
            Assert.IsTrue(groups.Skip(1).SelectMany(group => group).All(release => !release.IsPrerelease));
        }

        [TestMethod]
        public void UpdatingSettingsReadsPrereleaseState()
        {
            var settings = JsonSerializer.Deserialize<UpdatingSettings>("""{"state":2,"isPrerelease":true}""");

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.IsPrerelease);
        }

        private static IList<PowerToysReleaseInfo> CreateReleases()
        {
            return
            [
                new PowerToysReleaseInfo
                {
                    TagName = "v0.100.2607.27001-preview",
                    IsPrerelease = true,
                    PublishedDate = new DateTimeOffset(2026, 7, 27, 0, 0, 0, TimeSpan.Zero),
                },
                new PowerToysReleaseInfo
                {
                    TagName = "v0.100.2",
                    PublishedDate = new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero),
                },
                new PowerToysReleaseInfo
                {
                    TagName = "v0.99.1",
                    PublishedDate = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero),
                },
            ];
        }
    }
}
