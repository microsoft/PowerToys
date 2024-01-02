// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;
using Microsoft.VariantAssignment.Client;
using Microsoft.VariantAssignment.Contract;
using Windows.System.Profile;

namespace AllExperiments
{
    // The dependencies required to build this project are only available in the official build pipeline and are internal to Microsoft.
    // However, this project is not required to build a test version of the application.
    public class Experiments
    {
        public enum ExperimentState
        {
            Enabled,
            Disabled,
            NotLoaded,
        }

#pragma warning disable SA1401 // Need to use LandingPageExperiment as a static property in OobeShellPage.xaml.cs
#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static ExperimentState LandingPageExperiment = ExperimentState.NotLoaded;
#pragma warning restore CA2211
#pragma warning restore SA1401

        public async Task<bool> EnableLandingPageExperimentAsync()
        {
            if (Experiments.LandingPageExperiment != ExperimentState.NotLoaded)
            {
                return Experiments.LandingPageExperiment == ExperimentState.Enabled;
            }

            Experiments varServ = new Experiments();
            await varServ.VariantAssignmentProvider_Initialize();
            var landingPageExperiment = varServ.IsExperiment;

            Experiments.LandingPageExperiment = landingPageExperiment ? ExperimentState.Enabled : ExperimentState.Disabled;

            return landingPageExperiment;
        }

        private async Task VariantAssignmentProvider_Initialize()
        {
            IsExperiment = false;
            string jsonFilePath = CreateFilePath();

            var vaSettings = new VariantAssignmentClientSettings
            {
                Endpoint = new Uri("https://default.exp-tas.com/exptas77/a7a397e7-6fbe-4f21-a4e9-3f542e4b000e-exppowertoys/api/v1/tas"),
                EnableCaching = true,
                ResponseCacheTime = TimeSpan.FromMinutes(5),
            };

            try
            {
                var vaClient = vaSettings.GetTreatmentAssignmentServiceClient();
                var vaRequest = GetVariantAssignmentRequest();
                using var variantAssignments = await vaClient.GetVariantAssignmentsAsync(vaRequest).ConfigureAwait(false);

                if (variantAssignments.AssignedVariants.Count != 0)
                {
                    var dataVersion = variantAssignments.DataVersion;
                    var featureVariables = variantAssignments.GetFeatureVariables();
                    var assignmentContext = variantAssignments.GetAssignmentContext();
                    var featureFlagValue = featureVariables[0].GetStringValue();

                    var experimentGroup = string.Empty;
                    string json = File.ReadAllText(jsonFilePath);
                    var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                    if (jsonDictionary != null)
                    {
                        if (!jsonDictionary.TryGetValue("dataversion", out object? value))
                        {
                            value = dataVersion;
                            jsonDictionary.Add("dataversion", value);
                        }

                        if (!jsonDictionary.ContainsKey("variantassignment"))
                        {
                            jsonDictionary.Add("variantassignment", featureFlagValue);
                        }
                        else
                        {
                            var jsonDataVersion = value.ToString();
                            if (jsonDataVersion != null && int.Parse(jsonDataVersion, CultureInfo.InvariantCulture) < dataVersion)
                            {
                                jsonDictionary["dataversion"] = dataVersion;
                                jsonDictionary["variantassignment"] = featureFlagValue;
                            }
                        }

                        experimentGroup = jsonDictionary["variantassignment"].ToString();

                        string output = JsonSerializer.Serialize(jsonDictionary);
                        File.WriteAllText(jsonFilePath, output);
                    }

                    if (experimentGroup == "alternate" && AssignmentUnit != string.Empty)
                    {
                        IsExperiment = true;
                    }

                    PowerToysTelemetry.Log.WriteEvent(new OobeVariantAssignmentEvent() { AssignmentContext = assignmentContext, ClientID = AssignmentUnit });
                }
            }
            catch (HttpRequestException ex)
            {
                string json = File.ReadAllText(jsonFilePath);
                var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (jsonDictionary != null)
                {
                    if (jsonDictionary.TryGetValue("variantassignment", out object? value))
                    {
                        if (value.ToString() == "alternate" && AssignmentUnit != string.Empty)
                        {
                            IsExperiment = true;
                        }
                    }
                    else
                    {
                        jsonDictionary["variantassignment"] = "current";
                    }
                }

                string output = JsonSerializer.Serialize(jsonDictionary);
                File.WriteAllText(jsonFilePath, output);

                Logger.LogError("Error getting to TAS endpoint", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error getting variant assignments for experiment", ex);
            }
        }

        public bool IsExperiment { get; set; }

        private string? AssignmentUnit { get; set; }

        private VariantAssignmentRequest GetVariantAssignmentRequest()
        {
            var jsonFilePath = CreateFilePath();
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    AssignmentUnit = Guid.NewGuid().ToString();
                    var data = new Dictionary<string, string>()
                    {
                        ["clientid"] = AssignmentUnit,
                    };
                    string jsonData = JsonSerializer.Serialize(data);
                    File.WriteAllText(jsonFilePath, jsonData);
                }
                else
                {
                    string json = File.ReadAllText(jsonFilePath);
                    var jsonDictionary = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (jsonDictionary != null)
                    {
                        AssignmentUnit = jsonDictionary["clientid"]?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error creating/getting AssignmentUnit", ex);
            }

            var attrNames = new List<string> { "FlightRing", "c:InstallLanguage" };
            var attrData = AnalyticsInfo.GetSystemPropertiesAsync(attrNames).AsTask().GetAwaiter().GetResult();

            var flightRing = string.Empty;
            var installLanguage = string.Empty;

            if (attrData.ContainsKey("FlightRing"))
            {
                flightRing = attrData["FlightRing"];
            }

            if (attrData.ContainsKey("InstallLanguage"))
            {
                installLanguage = attrData["InstallLanguage"];
            }

            return new VariantAssignmentRequest
            {
                Parameters =
                {
                    { "installLanguage", installLanguage },
                    { "flightRing", flightRing },
                    { "clientid", AssignmentUnit },
                },
            };
        }

        private string CreateFilePath()
        {
            var exeDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsPath = @"Microsoft\PowerToys\experimentation.json";
            var filePath = Path.Combine(exeDir, settingsPath);
            return filePath;
        }
    }
}
