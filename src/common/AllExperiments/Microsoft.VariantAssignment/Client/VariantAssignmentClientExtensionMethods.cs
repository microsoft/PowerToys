// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VariantAssignment.Contract;

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Client
{
#pragma warning disable SA1200 // Using directives should be placed correctly
    using TreatmentAssignmentServiceClient = VariantAssignmentServiceClient<TreatmentAssignmentServiceResponse>;
#pragma warning restore SA1200 // Using directives should be placed correctly

    public static class VariantAssignmentClientExtensionMethods
    {
        public static IVariantAssignmentProvider GetTreatmentAssignmentServiceClient(this VariantAssignmentClientSettings settings)
        {
            return new TreatmentAssignmentServiceClient();
        }
    }
}
