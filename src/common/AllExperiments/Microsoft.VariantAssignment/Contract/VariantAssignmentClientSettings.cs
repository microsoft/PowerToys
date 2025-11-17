// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

// The goal of this class is to just mock out the Microsoft.VariantAssignment close source objects
namespace Microsoft.VariantAssignment.Contract
{
    /// <summary>
    /// Configuration for variant assignment service client.
    /// </summary>
    public class VariantAssignmentClientSettings
    {
        /// <summary>
        /// Gets or sets the variant assignment service endpoint URL.
        /// </summary>
        [Required]
        public Uri? Endpoint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether gets or sets a value whether client side request caching should be enabled.
        /// </summary>
        public bool EnableCaching { get; set; }

        /// <summary>
        /// Gets or sets the maximum time a cached variant assignment response may be used without re-validating.
        /// </summary>
        public TimeSpan ResponseCacheTime { get; set; }
    }
}
