// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Contract
{
    public class EmptyVariantAssignmentResponse : IVariantAssignmentResponse
    {
        /// <summary>
        /// Singleton instance of <see cref="EmptyVariantAssignmentResponse"/>.
        /// </summary>
        public static readonly IVariantAssignmentResponse Instance = new EmptyVariantAssignmentResponse();

        public EmptyVariantAssignmentResponse()
        {
        }

        public long DataVersion => 0;

        public string Thumbprint => string.Empty;

        /// <inheritdoc/>
        public IReadOnlyCollection<IAssignedVariant> AssignedVariants => Array.Empty<IAssignedVariant>();

        /// <inheritdoc/>
#pragma warning disable CS8603 // Possible null reference return.
        public IFeatureVariable GetFeatureVariable(IReadOnlyList<string> path) => null;
#pragma warning restore CS8603 // Possible null reference return.

        /// <inheritdoc/>
        public IReadOnlyList<IFeatureVariable> GetFeatureVariables(IReadOnlyList<string> prefix) => Array.Empty<IFeatureVariable>();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        string IVariantAssignmentResponse.GetAssignmentContext()
        {
            throw new NotImplementedException();
        }

        IReadOnlyList<IFeatureVariable> IVariantAssignmentResponse.GetFeatureVariables()
        {
            throw new NotImplementedException();
        }
    }
}
