// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Contract
{
    public class VariantAssignmentRequest : IVariantAssignmentRequest
    {
        private NameValueCollection _parameters = new NameValueCollection();

        /// <summary>
        /// Gets or sets mutable <see cref="IVariantAssignmentRequest.Parameters"/>.
        /// </summary>
        public NameValueCollection Parameters { get => _parameters; set => _parameters = value; }

        IReadOnlyCollection<(string Key, string Value)> IVariantAssignmentRequest.Parameters => (IReadOnlyCollection<(string Key, string Value)>)_parameters;
    }
}
