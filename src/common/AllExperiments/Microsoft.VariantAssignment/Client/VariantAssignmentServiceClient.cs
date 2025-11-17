// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VariantAssignment.Contract;

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Client
{
    internal sealed partial class VariantAssignmentServiceClient<TServerResponse> : IVariantAssignmentProvider, IDisposable
        where TServerResponse : VariantAssignmentServiceResponse
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task<IVariantAssignmentResponse> GetVariantAssignmentsAsync(IVariantAssignmentRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(EmptyVariantAssignmentResponse.Instance);
        }
    }
}
