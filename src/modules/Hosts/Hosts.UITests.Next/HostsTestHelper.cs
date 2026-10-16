// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.UITests
{
    internal static class HostsTestHelper
    {
        public const string SaveErrorMessage = "The hosts file cannot be saved because the program isn't running as administrator.";

        private const string DefaultSettings = """
            {
              "properties": {
                "ShowStartupWarning": { "value": true },
                "LaunchAdministrator": { "value": true },
                "LoopbackDuplicates": { "value": false },
                "AdditionalLinesPosition": 0,
                "Encoding": 0,
                "NoLeadingSpaces": { "value": false },
                "BackupHosts": { "value": true },
                "BackupPath": "",
                "DeleteBackupsMode": 2,
                "DeleteBackupsDays": 15,
                "DeleteBackupsCount": 5
              },
              "name": "Hosts",
              "version": "1.0"
            }
            """;

        public static IDisposable PreserveSettingsAndDisableBackups()
        {
            var snapshot = SettingsConfigHelper.PreserveModuleSettings("Hosts");
            try
            {
                SettingsConfigHelper.UpdateModuleSettings(
                    "Hosts",
                    DefaultSettings,
                    settings =>
                    {
                        if (settings["properties"] is not JsonObject properties)
                        {
                            properties = new JsonObject();
                            settings["properties"] = properties;
                        }

                        properties["BackupHosts"] = new JsonObject { ["value"] = false };
                    });
                return snapshot;
            }
            catch
            {
                snapshot.Dispose();
                throw;
            }
        }

        public static ToggleSwitch FindEntryDialogActiveToggle(Session session)
        {
            var matches = session.FindAll<ToggleSwitch>(By.Name("Active"))
                .Where(toggle => string.Equals(toggle.Name, "Active", StringComparison.Ordinal))
                .ToList();
            Assert.IsTrue(matches.Count > 0, "The Add entry dialog Active toggle was not found.");

            // Existing rows also expose an Active toggle. The dialog control is the bottom-most
            // exact-name match because it follows the Address, Hosts, and Comment fields.
            return matches.MaxBy(toggle => toggle.Y)!;
        }

        public static T FindExact<T>(this Session session, string name, int timeoutMS = 5_000)
            where T : Element, new()
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
            while (true)
            {
                List<T> matches;
                try
                {
                    matches = session.FindAll<T>(By.Name(name), 0)
                        .Where(element => string.Equals(element.Name, name, StringComparison.Ordinal))
                        .ToList();
                }
                catch (AssertFailedException ex) when (
                    DateTime.UtcNow < deadline &&
                    ex.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(250);
                    continue;
                }

                Assert.IsTrue(
                    matches.Count <= 1,
                    $"Expected at most one exact {typeof(T).Name} named '{name}', found {matches.Count}.");
                if (matches.Count == 1)
                {
                    return matches[0];
                }

                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail($"Exact {typeof(T).Name} named '{name}' was not found within {timeoutMS} ms.");
                }

                Thread.Sleep(250);
            }
        }

        public static int CountExact<T>(this Session session, string name, int timeoutMS = 0)
            where T : Element, new() =>
            session.FindAll<T>(By.Name(name), timeoutMS)
                .Count(element => string.Equals(element.Name, name, StringComparison.Ordinal));

        public static bool WaitForExactCount<T>(this Session session, string name, int expectedCount, int timeoutMS = 5_000)
            where T : Element, new() =>
            session.WaitFor(() => session.CountExact<T>(name) == expectedCount, timeoutMS, pollIntervalMS: 250);
    }
}
