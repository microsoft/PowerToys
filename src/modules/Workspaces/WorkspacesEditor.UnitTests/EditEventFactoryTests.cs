// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor.Models;
using WorkspacesEditor.Telemetry;

namespace WorkspacesEditor.UnitTests
{
    [TestClass]
    public class EditEventFactoryTests
    {
        [TestMethod]
        public void Create_MatchesApplicationIdentityAfterPositionChanges()
        {
            Project previous = new()
            {
                Applications = [CreateApplication("same", string.Empty, false)],
            };
            Project updated = new()
            {
                Applications = [CreateApplication("SAME", "--new-window", true)],
                IsPositionChangedManually = true,
            };
            updated.Applications[0].Position = new Application.WindowPosition { X = 300, Y = 200, Width = 800, Height = 600 };

            EditEvent telemetryEvent = EditEventFactory.Create(previous, updated);

            Assert.AreEqual(0, telemetryEvent.AppsAdded);
            Assert.AreEqual(0, telemetryEvent.AppsRemoved);
            Assert.AreEqual(1, telemetryEvent.CliAdded);
            Assert.AreEqual(0, telemetryEvent.CliRemoved);
            Assert.AreEqual(1, telemetryEvent.AdminAdded);
            Assert.AreEqual(0, telemetryEvent.AdminRemoved);
            Assert.IsTrue(telemetryEvent.PixelAdjustmentsUsed);
        }

        [TestMethod]
        public void Create_CountsExclusionsAndNewApplicationsOnce()
        {
            Project previous = new()
            {
                Applications = [CreateApplication("removed", string.Empty, false), CreateApplication("changed", "--old", true)],
            };
            Application excluded = CreateApplication("removed", "--not-saved", true);
            excluded.IsIncluded = false;
            Project updated = new()
            {
                Applications = [excluded, CreateApplication("changed", string.Empty, false), CreateApplication("added", string.Empty, false)],
            };
            updated.Monitors.Add(new MonitorSetup("second", string.Empty, 2, 96, default, default));

            EditEvent telemetryEvent = EditEventFactory.Create(previous, updated);

            Assert.AreEqual(1, telemetryEvent.ScreenCountDelta);
            Assert.AreEqual(1, telemetryEvent.AppsAdded);
            Assert.AreEqual(1, telemetryEvent.AppsRemoved);
            Assert.AreEqual(0, telemetryEvent.CliAdded);
            Assert.AreEqual(1, telemetryEvent.CliRemoved);
            Assert.AreEqual(0, telemetryEvent.AdminAdded);
            Assert.AreEqual(1, telemetryEvent.AdminRemoved);
        }

        private static Application CreateApplication(string id, string arguments, bool isElevated)
        {
            return new Application
            {
                Id = id,
                AppName = "Same application name",
                CommandLineArguments = arguments,
                IsElevated = isElevated,
            };
        }
    }
}
