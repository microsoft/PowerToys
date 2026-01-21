// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// Interface for ViewModels that require async initialization after construction.
    /// This enables separating heavy loading logic from constructors to improve page navigation performance.
    /// </summary>
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Gets a value indicating whether the ViewModel is currently loading.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Gets a value indicating whether the ViewModel has been initialized.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initializes the ViewModel asynchronously. This method should be called
        /// after navigation to the page, not in the constructor.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel initialization.</param>
        /// <returns>A task representing the async operation.</returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
