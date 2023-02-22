// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Contract
{
    /// <summary>
    /// Snapshot of variant assignments.
    /// </summary>
    public interface IVariantAssignmentResponse : IDisposable
    {
        ///// <summary>
        ///// Gets the serial number of variant assignment configuration snapshot used when assigning variants.
        ///// </summary>
        long DataVersion { get; }

        ///// <summary>
        ///// Get a hash of the response suitable for caching.
        ///// </summary>
        // string Thumbprint { get; }

        /// <summary>
        /// Gets the variants assigned based on request parameters and a variant configuration snapshot.
        /// </summary>
        IReadOnlyCollection<IAssignedVariant> AssignedVariants { get; }

        /// <summary>
        /// Gets feature variables assigned by variants in this response.
        /// </summary>
        /// <param name="prefix">(Optional) Filter feature variables where <see cref="IFeatureVariable.KeySegments"/> contains the <paramref name="prefix"/>.</param>
        /// <returns>Range of matching feature variables.</returns>
        IReadOnlyList<IFeatureVariable> GetFeatureVariables(IReadOnlyList<string> prefix);

        // this actually part of the interface but gets the job done
        IReadOnlyList<IFeatureVariable> GetFeatureVariables();

        // this actually part of the interface but gets the job done
        string GetAssignmentContext();

        /// <summary>
        /// Gets a single feature variable assigned by variants in this response.
        /// </summary>
        /// <param name="path">Exact feature variable path.</param>
        /// <returns>Matching feature variable or null.</returns>
        IFeatureVariable GetFeatureVariable(IReadOnlyList<string> path);
    }
}
