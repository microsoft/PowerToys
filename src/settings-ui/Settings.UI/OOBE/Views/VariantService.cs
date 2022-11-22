// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.VariantAssignment.Client;
using Microsoft.VariantAssignment.Contract;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
    using Microsoft.PowerToys.Telemetry;
    using Microsoft.Win32;

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
                PowerToysTelemetry.Log.WriteEvent(new OobeVariantAssignmentEvent() { Date = DateTime.Today, FlightID = assignmentContext });
            }
            catch (Exception)
            {
                FeatureFlagValue = "current";
            }
        }

        public string FeatureFlagValue { get; set; }

        private static IVariantAssignmentRequest GetVariantAssignmentRequest()
        {
            string sqmID = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\SQMClient", "MachineID", null).ToString();
            string machineName = ComputeSha256Hash(Environment.MachineName);
            string clientID = sqmID + "_" + machineName;

            return new VariantAssignmentRequest
            {
                Parameters =
                {
                    // TBD: Adding traffic filters to target specific audiences.
                    { "clientid", Guid.NewGuid().ToString() },
                },
            };
        }

        public static string ComputeSha256Hash(string value)
        {
            using var hash = SHA256.Create();
            var byteArray = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(byteArray);
        }
    }
}
