// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.ViewModels
{
    /// <summary>
    /// Represents the current state of a ViewModel
    /// </summary>
    public enum ViewModelState
    {
        /// <summary>
        /// Initial state - ViewModel is being initialized
        /// </summary>
        Initializing,

        /// <summary>
        /// Loading state - data is being reloaded or refreshed
        /// </summary>
        Loading,

        /// <summary>
        /// Ready state - ViewModel is ready for user interaction
        /// </summary>
        Ready,

        /// <summary>
        /// Error state - ViewModel encountered an error
        /// </summary>
        Error
    }
}
