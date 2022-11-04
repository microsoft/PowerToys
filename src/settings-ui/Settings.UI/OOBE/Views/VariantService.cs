// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.Extensions.Logging;
using Microsoft.VariantAssignment.Client;
using Microsoft.VariantAssignment.Contract;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class VariantService
    {
        // private IReadOnlyList<IFeatureVariable> featureVariables;

        // This is the call which will fetch the assignment response and store the result into a globally accessible variable
        // for the rest of our code to access.
        public async Task VariantAssignmentProvider_Initialize()
        {
            // 1. Load configuration, e.g. bind from configuration, inject as IOptions<T>, etc.
            var vaSettings = new VariantAssignmentClientSettings
            {
                Endpoint = new Uri("https://default.exp-tas.com/exptas77/a7a397e7-6fbe-4f21-a4e9-3f542e4b000e-exppowertoys/api/v1/tas"),
                EnableCaching = true,
                ResponseCacheTime = TimeSpan.FromMinutes(5),
            };

            // 2. Initialize a TAS client, dispose when finished.
            // A TAS client is used as an endpoint client to query, refetch, and cache data from the Experimentation service.
            using var httpHandler = new TestResponseHandler(); // test handler to avoid network calls
            using var vaClient = httpHandler.GetTreatmentAssignmentServiceClient(vaSettings, disposeHandler: false);

            // 3. Obtain variant assignments.
            // The VariantAssignmentRequest has parameters that detail the current user. What UserID are they? Market? Build?
            var vaRequest = GetVariantAssignmentRequest();
            using var variantAssignments = await vaClient.GetVariantAssignmentsAsync(vaRequest).ConfigureAwait(false);

            // 4. Use variant assignments.
            FeatureVariables = variantAssignments.GetFeatureVariables();
        }

        public IReadOnlyList<IFeatureVariable> FeatureVariables { get; set; }

        // The response will be used to describe the user. Ideally ,this will get the exact data of the user that will then
        // help determine what experiments this user can be eligible for. I think this is pulling from telemetry of the user?
        private static IVariantAssignmentRequest GetVariantAssignmentRequest()
        {
            // VariantAssignmentRequest is ambiguous because the test project references both on-box and remote-client libraries
            // both of which implement the VariantAssignmentRequest independently (for technical reasons).
            // Consumers of the package would typically reference only one implementation and thus avoid any ambiguities.
            return new VariantAssignmentRequest
            {
                Parameters =
                {
                    // parameters may be used for traffic filtering
                    { "Application", "App1" },

                    // some parameters are designated as "assignment units" and used for randomization
                    { "UserId", Guid.NewGuid().ToString() },
                },
            };
        }
    }
}
