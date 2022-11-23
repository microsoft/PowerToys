// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.VariantAssignment.Client;
using Microsoft.VariantAssignment.Contract;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
    using Microsoft.PowerToys.Telemetry;

    public class VariantService
    {
        public void VariantAssignmentProvider_Initialize()
        {
            var vaSettings = new VariantAssignmentClientSettings
            {
                Endpoint = new Uri("https://default.exp-tas.com/exptas77/a7a397e7-6fbe-4f21-a4e9-3f542e4b000e-exppowertoys/api/v1/tas"),
                EnableCaching = true,
                ResponseCacheTime = TimeSpan.FromMinutes(5),
            };

            var vaClient = vaSettings.GetTreatmentAssignmentServiceClient();
            var vaRequest = GetVariantAssignmentRequest();

            try
            {
                var task = vaClient.GetVariantAssignmentsAsync(vaRequest);
                var result = task.Result;
                var allFeatureFlags = result.GetFeatureVariables();

                var assignmentContext = result.GetAssignmentContext();
                var featureNameSpace = allFeatureFlags[0].KeySegments[0];
                var featureFlag = allFeatureFlags[0].KeySegments[1];
                FeatureFlagValue = allFeatureFlags[0].GetStringValue();
                PowerToysTelemetry.Log.WriteEvent(new OobeVariantAssignmentEvent() { AssignmentContext = assignmentContext });
            }
            catch (Exception)
            {
                FeatureFlagValue = "current";
            }
        }

        public string FeatureFlagValue { get; set; }

        private static IVariantAssignmentRequest GetVariantAssignmentRequest()
        {
            string clientID = string.Empty;

            string workingDirectory = Environment.CurrentDirectory;
            var exeDir = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
            string settingsPath = @"src\settings-ui\Settings.UI.UnitTests\BackwardsCompatibility\TestFiles\v0.22.0\Microsoft\PowerToys\settings.json";
            string jsonFilePath = Path.Combine(exeDir, settingsPath);

            string json = File.ReadAllText(jsonFilePath);
            var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var jsonClientID = jsonDictionary["clientid"].ToString();

            if (jsonClientID == string.Empty)
            {
                jsonDictionary["clientid"] = Guid.NewGuid().ToString();
                string output = JsonSerializer.Serialize(jsonDictionary);
                File.WriteAllText(jsonFilePath, output);
            }

            clientID = jsonDictionary["clientid"].ToString();

            return new VariantAssignmentRequest
            {
                Parameters =
                {
                    // TBD: Adding traffic filters to target specific audiences.
                    { "clientid", clientID },
                },
            };
        }
    }
}
