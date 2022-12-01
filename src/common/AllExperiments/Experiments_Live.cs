// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AllExperiments
{
    using System.Text.Json;
    using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
    using Microsoft.PowerToys.Telemetry;
    using Microsoft.VariantAssignment.Client;
    using Microsoft.VariantAssignment.Contract;
    using Wox.Plugin.Logger;

#pragma warning disable SA1649 // File name should match first type name
    public class Experiments
#pragma warning restore SA1649 // File name should match first type name
    {
        public bool EnableLandingPageExperiment()
        {
            Experiments varServ = new Experiments();
            varServ.VariantAssignmentProvider_Initialize();
            var landingPageExperiment = varServ.IsExperiment;

            return landingPageExperiment;
        }

        private void VariantAssignmentProvider_Initialize()
        {
            IsExperiment = false;

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
                TimeSpan ts = TimeSpan.FromMilliseconds(500);
                if (task.Wait(ts))
                {
                    var result = task.Result;
                    var allFeatureFlags = result.GetFeatureVariables();

                    var assignmentContext = result.GetAssignmentContext();
                    var featureNameSpace = allFeatureFlags[0].KeySegments[0];
                    var featureFlag = allFeatureFlags[0].KeySegments[1];
                    var featureFlagValue = allFeatureFlags[0].GetStringValue();

                    if (featureFlagValue == "alternate")
                    {
                        IsExperiment = true;
                    }

                    PowerToysTelemetry.Log.WriteEvent(new OobeVariantAssignmentEvent() { AssignmentContext = assignmentContext, ClientID = AssignmentUnit });
                }
            }
            catch (Exception ex)
            {
                Log.Exception("Error getting variant assignments for experiment", ex, typeof(Experiments));
            }
        }

        public bool IsExperiment { get; set; }

        private string? AssignmentUnit { get; set; }

        private IVariantAssignmentRequest GetVariantAssignmentRequest()
        {
            string? clientID = string.Empty;

            string workingDirectory = Environment.CurrentDirectory;
            var exeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string settingsPath = @"AppData\Local\Microsoft\PowerToys\settings.json";
            string jsonFilePath = Path.Combine(exeDir, settingsPath);

            string json = File.ReadAllText(jsonFilePath);
            var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (jsonDictionary != null)
            {
                if (!jsonDictionary.ContainsKey("clientid"))
                {
                    jsonDictionary.Add("clientid", string.Empty);
                    jsonDictionary["clientid"] = Guid.NewGuid().ToString();
                    string output = JsonSerializer.Serialize(jsonDictionary);
                    File.WriteAllText(jsonFilePath, output);
                }

                clientID = jsonDictionary["clientid"]?.ToString();
                AssignmentUnit = clientID;
            }

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
