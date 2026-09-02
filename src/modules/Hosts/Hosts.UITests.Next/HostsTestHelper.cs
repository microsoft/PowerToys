// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;

namespace Hosts.UITests
{
    internal static class HostsTestHelper
    {
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
    }
}
