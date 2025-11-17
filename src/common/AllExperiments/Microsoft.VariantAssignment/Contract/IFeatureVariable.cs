// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Contract
{
    public interface IFeatureVariable
    {
        /// <summary>
        /// Gets the variable's value as a string.
        /// </summary>
        /// <returns>String value of the variable.</returns>
        string GetStringValue();
    }
}
