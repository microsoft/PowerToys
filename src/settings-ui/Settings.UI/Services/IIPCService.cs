// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

using Windows.Data.Json;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Interface for Inter-Process Communication with the PowerToys Runner.
    /// Abstracts the static IPC callbacks to enable dependency injection and testability.
    /// </summary>
    public interface IIPCService
    {
        /// <summary>
        /// Gets a value indicating whether IPC is connected to the Runner.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Sends a default IPC message to the Runner.
        /// </summary>
        /// <param name="message">The JSON message to send.</param>
        /// <returns>Result code from the IPC call.</returns>
        int SendMessage(string message);

        /// <summary>
        /// Sends a request to restart as administrator.
        /// </summary>
        /// <param name="message">The JSON message to send.</param>
        /// <returns>Result code from the IPC call.</returns>
        int SendRestartAsAdminMessage(string message);

        /// <summary>
        /// Sends a request to check for updates.
        /// </summary>
        /// <param name="message">The JSON message to send.</param>
        /// <returns>Result code from the IPC call.</returns>
        int SendCheckForUpdatesMessage(string message);

        /// <summary>
        /// Sends a message asynchronously.
        /// </summary>
        /// <param name="message">The JSON message to send.</param>
        /// <returns>A task representing the async operation with result code.</returns>
        Task<int> SendMessageAsync(string message);

        /// <summary>
        /// Registers a callback for IPC responses.
        /// </summary>
        /// <param name="callback">The callback to invoke when a response is received.</param>
        void RegisterResponseCallback(Action<JsonObject> callback);

        /// <summary>
        /// Unregisters a previously registered callback.
        /// </summary>
        /// <param name="callback">The callback to unregister.</param>
        void UnregisterResponseCallback(Action<JsonObject> callback);
    }
}
