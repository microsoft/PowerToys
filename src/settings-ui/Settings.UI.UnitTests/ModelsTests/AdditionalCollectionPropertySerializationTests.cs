// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace CommonLibTest
{
    [TestClass]
    public class AdditionalCollectionPropertySerializationTests
    {
        [TestMethod]
        public void AdditionalSettingsCollectionsDeserializeWithInitOnlySetters()
        {
            const string mouseWithoutBordersJson = """
                {
                  "MachineMatrixString": ["left", "right"]
                }
                """;
            const string pasteAiJson = """
                {
                  "providers": [
                    {
                      "id": "provider-1",
                      "service-type": "OpenAI"
                    }
                  ]
                }
                """;
            const string pluginOptionJson = """
                {
                  "ComboBoxItems": [
                    {
                      "Key": "First",
                      "Value": "1"
                    }
                  ]
                }
                """;

            var mouseWithoutBorders = JsonSerializer.Deserialize<MouseWithoutBordersProperties>(mouseWithoutBordersJson);
            var pasteAi = JsonSerializer.Deserialize<PasteAIConfiguration>(pasteAiJson);
            var pluginOption = JsonSerializer.Deserialize<PluginAdditionalOption>(pluginOptionJson);

            Assert.IsNotNull(mouseWithoutBorders);
            Assert.HasCount(2, mouseWithoutBorders.MachineMatrixString);
            Assert.IsNotNull(pasteAi);
            Assert.HasCount(1, pasteAi.Providers);
            Assert.AreEqual("provider-1", pasteAi.Providers[0].Id);
            Assert.IsNotNull(pluginOption);
            Assert.HasCount(1, pluginOption.ComboBoxItems);
            Assert.AreEqual("First", pluginOption.ComboBoxItems[0].Key);
        }

        [TestMethod]
        public void MouseWithoutBordersCollectionRemainsMutableWhenJsonValueIsNull()
        {
            const string json = """
                {
                  "MachineMatrixString": null
                }
                """;

            var properties = JsonSerializer.Deserialize<MouseWithoutBordersProperties>(json);

            Assert.IsNotNull(properties);
            Assert.IsNotNull(properties.MachineMatrixString);
            Assert.IsEmpty(properties.MachineMatrixString);

            properties.MachineMatrixString.Add("machine-1");

            Assert.HasCount(1, properties.MachineMatrixString);
            Assert.AreEqual("machine-1", properties.MachineMatrixString[0]);
        }

        [TestMethod]
        public void PowerDisplayCollectionsDeserializeWithInitOnlySetters()
        {
            const string json = """
                {
                  "monitors": [
                    {
                      "vcpCodesFormatted": [
                        {
                          "code": "0x14",
                          "valueList": [
                            {
                              "value": "0x05",
                              "name": "6500K"
                            }
                          ]
                        }
                      ]
                    }
                  ],
                  "excluded_from_sync_monitor_ids": ["monitor-1"],
                  "custom_vcp_mappings": [
                    {
                      "vcpCode": 20,
                      "value": 5,
                      "customName": "Warm"
                    }
                  ]
                }
                """;

            var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

            Assert.IsNotNull(properties);
            Assert.HasCount(1, properties.Monitors);
            Assert.HasCount(1, properties.Monitors[0].VcpCodesFormatted);
            Assert.HasCount(1, properties.Monitors[0].VcpCodesFormatted[0].ValueList);
            Assert.HasCount(1, properties.ExcludedFromSyncMonitorIds);
            Assert.HasCount(1, properties.CustomVcpMappings);
        }

        [TestMethod]
        public void PowerDisplayCollectionsRemainMutableWhenJsonValuesAreNull()
        {
            const string json = """
                {
                  "monitors": null,
                  "excluded_from_sync_monitor_ids": null,
                  "custom_vcp_mappings": null
                }
                """;

            var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

            Assert.IsNotNull(properties);
            Assert.IsNotNull(properties.Monitors);
            Assert.IsNotNull(properties.ExcludedFromSyncMonitorIds);
            Assert.IsNotNull(properties.CustomVcpMappings);
            Assert.IsEmpty(properties.Monitors);
            Assert.IsEmpty(properties.ExcludedFromSyncMonitorIds);
            Assert.IsEmpty(properties.CustomVcpMappings);

            properties.Monitors.Add(new MonitorInfo());
            properties.ExcludedFromSyncMonitorIds.Add("monitor-1");
            properties.CustomVcpMappings.Add(new CustomVcpValueMapping());

            Assert.HasCount(1, properties.Monitors);
            Assert.HasCount(1, properties.ExcludedFromSyncMonitorIds);
            Assert.HasCount(1, properties.CustomVcpMappings);
        }

        [TestMethod]
        public void PluginMultilineAliasCanBeInitialized()
        {
            var option = new PluginAdditionalOption
            {
                TextValueAsMultilineList = ["first", "second"],
            };

            Assert.HasCount(2, option.TextValueAsMultilineList);
            Assert.AreEqual("first", option.TextValueAsMultilineList[0]);
            Assert.AreEqual("second", option.TextValueAsMultilineList[1]);
        }
    }
}
