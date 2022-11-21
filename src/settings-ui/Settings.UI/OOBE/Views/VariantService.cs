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
    using Microsoft.Win32;

    public class VariantService
    {
        // This is the call which will fetch the assignment response and store the result into a globally accessible variable
        // for the rest of our code to access.
        public void VariantAssignmentProvider_Initialize()
        {
            // Load configuration
            var vaSettings = new VariantAssignmentClientSettings
            {
                Endpoint = new Uri("https://default.exp-tas.com/exptas77/a7a397e7-6fbe-4f21-a4e9-3f542e4b000e-exppowertoys/api/v1/tas"),
                EnableCaching = true,
                ResponseCacheTime = TimeSpan.FromMinutes(5),
            };

            // The VariantAssignmentRequest has parameters that detail the current user. What UserID are they? Market? Build?
            var vaClient = vaSettings.GetTreatmentAssignmentServiceClient();
            var vaRequest = GetVariantAssignmentRequest();
            var task = vaClient.GetVariantAssignmentsAsync(vaRequest);
            var result = task.Result;

            // Use variant assignments.
            var allFeatureFlags = result.GetFeatureVariables();
            var featureNameSpace = allFeatureFlags[0].KeySegments[0];
            var featureFlag = allFeatureFlags[0].KeySegments[1];
            FeatureFlagValue = allFeatureFlags[0].GetStringValue();

            /*var featureFlagFriendlyStrings = allFeatureFlags
            .Select(f => new
            {
                featureNamespace = f.KeySegments[0],
                featureFlag = f.KeySegments[1],
                featureFlagValue = f.GetStringValue(), // assume all the flags are string types
            })
            .Select(f => $"{f.featureNamespace}.{f.featureFlag}={f.featureFlagValue}"); */
        }

        public string FeatureFlagValue { get; set; }

        // The response will be used to describe the user. Ideally ,this will get the exact data of the user that will then
        // help determine what experiments this user can be eligible for.
        private static IVariantAssignmentRequest GetVariantAssignmentRequest()
        {
            string sqmID = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\SQMClient", "MachineID", null).ToString();
            string machineName = ComputeSha256Hash(Environment.MachineName);
            string clientID = sqmID + "_" + machineName;

            return new VariantAssignmentRequest
            {
                // Parameters are inputs used for evaluating filters, randomization units, etc.
                Parameters =
                {
                    // Parameters may be used for traffic filtering. Currently this is unknown so these are just fillers for testing purposes.
                    { "Application", "App1" },

                    // Assignment unit used for randomization. Using sqmID + machineName for a unique client ID.
                    // Guid.NewGuid().ToString() for testing
                    { "clientid", clientID },
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
