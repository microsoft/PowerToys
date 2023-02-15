// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Contract
{
    public interface IVariantAssignmentProvider : IDisposable
    {
        /// <summary>
        /// Computes variant assignments based on <paramref name="request"/> data.
        /// </summary>
        /// <param name="request">Variant assignment parameters.</param>
        /// <param name="ct">Propagates notification that operations should be canceled.</param>
        /// <returns>An awaitable task that returns a <see cref="IVariantAssignmentResponse"/>.</returns>
        Task<IVariantAssignmentResponse> GetVariantAssignmentsAsync(IVariantAssignmentRequest request, CancellationToken ct = default);
    }
}
