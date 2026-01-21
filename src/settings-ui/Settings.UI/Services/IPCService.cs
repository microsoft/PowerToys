// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Views;

using Windows.Data.Json;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Implementation of IIPCService that wraps the static ShellPage IPC methods.
    /// This adapter allows for dependency injection while maintaining backward compatibility.
    /// </summary>
    public class IPCService : IIPCService
    {
        private readonly List<Action<JsonObject>> _responseCallbacks = new();
        private readonly object _callbackLock = new();

        /// <inheritdoc/>
        public bool IsConnected => ShellPage.DefaultSndMSGCallback != null;

        /// <inheritdoc/>
        public int SendMessage(string message)
        {
            return ShellPage.SendDefaultIPCMessage(message);
        }

        /// <inheritdoc/>
        public int SendRestartAsAdminMessage(string message)
        {
            return ShellPage.SendRestartAdminIPCMessage(message);
        }

        /// <inheritdoc/>
        public int SendCheckForUpdatesMessage(string message)
        {
            return ShellPage.SendCheckForUpdatesIPCMessage(message);
        }

        /// <inheritdoc/>
        public Task<int> SendMessageAsync(string message)
        {
            return Task.Run(() => SendMessage(message));
        }

        /// <inheritdoc/>
        public void RegisterResponseCallback(Action<JsonObject> callback)
        {
            if (callback == null)
            {
                return;
            }

            lock (_callbackLock)
            {
                if (!_responseCallbacks.Contains(callback))
                {
                    _responseCallbacks.Add(callback);

                    // Also register with ShellPage for backward compatibility
                    if (ShellPage.ShellHandler?.IPCResponseHandleList != null)
                    {
                        ShellPage.ShellHandler.IPCResponseHandleList.Add(callback);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void UnregisterResponseCallback(Action<JsonObject> callback)
        {
            if (callback == null)
            {
                return;
            }

            lock (_callbackLock)
            {
                _responseCallbacks.Remove(callback);

                // Also unregister from ShellPage
                ShellPage.ShellHandler?.IPCResponseHandleList?.Remove(callback);
            }
        }
    }
}
