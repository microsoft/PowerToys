// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The dependencies required to build this project are only available in the official build pipeline and are internal to Microsoft.
// However, this project is not required to build a test version of the application.
namespace AllExperiments
{
    using System.IO.Pipes;
    using System.Text.Json;
    using System.Windows.Input;
    using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
    using Microsoft.PowerToys.Telemetry;
    using Microsoft.VariantAssignment.Client;
    using Microsoft.VariantAssignment.Contract;
    using Windows.System.Profile;
    using Wox.Plugin.Logger;

#pragma warning disable SA1649 // File name should match first type name. Suppressed because it needs to be the same class name as Experiments_Inert.cs
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
            var exeDir = Directory.GetParent(workingDirectory)?.Parent?.Parent?.FullName;
            string settingsPath = @"src\common\AllExperiments\settings.json";
            if (exeDir != null)
            {
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
            }

            var attrNames = new List<string> { "FlightRing" };
            var attrData = AnalyticsInfo.GetSystemPropertiesAsync(attrNames).AsTask().GetAwaiter().GetResult();

            var flightRing = string.Empty;

            foreach (KeyValuePair<string, string> attr in attrData)
            {
                flightRing = attr.Value;
            }

            return new VariantAssignmentRequest
            {
                Parameters =
                {
                    // TBD: Adding traffic filters to target region.
                    { "flightRing", flightRing },
                    { "clientid", clientID },
                },
            };
        }
    }
}
