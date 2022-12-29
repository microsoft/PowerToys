// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The goal of this class is to just mock out the Microsoft.VariantAssignment close sourced objects
namespace Microsoft.VariantAssignment.Contract
{
    /// <summary>
    /// Mutable implementation of <see cref="IVariantAssignmentResponse"/> for (de)serialization.
    /// </summary>
    internal class VariantAssignmentServiceResponse : IVariantAssignmentResponse, IDisposable
    {
        public virtual IReadOnlyCollection<IAssignedVariant> AssignedVariants { get; set; } = Array.Empty<IAssignedVariant>();

        public IFeatureVariable GetFeatureVariable(IReadOnlyList<string> path)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<IFeatureVariable> GetFeatureVariables(IReadOnlyList<string> prefix)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implements the <a href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose">dispose pattern</a>.
        /// </summary>
        /// <param name="disposing">True when invoked by <see cref="IDisposable.Dispose"/>, and false if invoked by the object finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IReadOnlyList<IFeatureVariable> GetFeatureVariables()
        {
            throw new NotImplementedException();
        }

        public string GetAssignmentContext()
        {
            return string.Empty;
        }
    }
}
